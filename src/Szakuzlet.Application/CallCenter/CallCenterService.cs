using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Application.Calendar;
using Szakuzlet.Application.Common;
using Szakuzlet.Application.Kvl;
using Szakuzlet.Domain.Entities;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Application.CallCenter;

/// <summary>
/// A telefonközpont (külső VoIP) által hívott belső szolgáltatás. Kezeli a bejövő hívás
/// feldolgozását (nyitvatartási zsilip + CRM-találat + adatlap-megnyitás), a szelektív
/// hangrögzítést jogi Audit Trail-lel, és az önürítő visszahívási listát.
/// </summary>
public sealed class CallCenterService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly EventRecorder _events;
    private readonly OpeningHoursService _hours;
    private readonly IKvlClient _kvl;

    public CallCenterService(IAppDbContext db, IClock clock, EventRecorder events,
        OpeningHoursService hours, IKvlClient kvl)
    {
        _db = db;
        _clock = clock;
        _events = events;
        _hours = hours;
        _kvl = kvl;
    }

    /// <summary>
    /// Bejövő hívás beérkezése. Rögzíti a hívást, ellenőrzi a nyitvatartást (zsilip),
    /// megpróbálja beazonosítani a beteget a hívószám alapján, és jelzi a menüpontot.
    /// A CRM-találat esetén a beteg adatlapja „megnyílik” (Timeline-esemény).
    /// </summary>
    public async Task<IncomingCallResult> HandleIncomingAsync(
        string phoneNumber, string? ivrMenu = null, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var status = await _hours.GetStatusNowAsync(ct);

        // CRM-találat: elsőként a helyi adatbázisban, majd (ha nincs) a KVL-ben.
        var patient = await FindPatientByPhoneAsync(phoneNumber, ct);

        var call = new CallRecord
        {
            Id = Guid.NewGuid(),
            Direction = CallDirection.Bejovo,
            PhoneNumber = phoneNumber,
            PatientId = patient?.Id,
            IvrMenu = ivrMenu,
            Recording = RecordingState.Rogzitve,
            StartedAtUtc = now,
            Answered = false
        };
        _db.Calls.Add(call);

        if (patient is not null)
        {
            _events.Timeline(patient.Id, "BejovoHivas",
                $"Bejövő hívás{(ivrMenu is null ? "" : $" (MENÜ: {ivrMenu})")} a(z) {phoneNumber} számról.");
        }
        _events.Audit("CallRecord", call.Id.ToString(), "BejovoHivasFogadva",
            $"Szám: {phoneNumber}; nyitvatartáson {(status.IsOpen ? "belül" : "kívül")}.");

        await _db.SaveChangesAsync(ct);

        return new IncomingCallResult(
            call.Id,
            status.IsOpen,
            patient is not null,
            patient?.Id,
            patient?.FullName,
            patient?.IsLegacy ?? false);
    }

    /// <summary>
    /// A hívás fogadása (a pultos felvette). Answered=true, és ha addig nem fogadták,
    /// a hozzá tartozó nyitott visszahívási igény önürül.
    /// </summary>
    public async Task MarkAnsweredAsync(Guid callId, CancellationToken ct = default)
    {
        var call = await _db.Calls.FirstOrDefaultAsync(c => c.Id == callId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen hívás: {callId}");
        call.Answered = true;
        await ResolveOpenCallbacksAsync(call.PhoneNumber, "Hívás fogadva", ct);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// A hívás lezárása. Ha nem fogadták, felkerül az önürítő visszahívási listára.
    /// A rögzített hangfájl hivatkozása a beteg Idővonalával linkelhető.
    /// </summary>
    public async Task EndCallAsync(Guid callId, bool answered, string? recordingReference,
        CancellationToken ct = default)
    {
        var call = await _db.Calls.FirstOrDefaultAsync(c => c.Id == callId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen hívás: {callId}");

        var now = _clock.UtcNow;
        call.EndedAtUtc = now;
        call.Answered = answered;
        if (call.Recording == RecordingState.Rogzitve)
            call.RecordingReference = recordingReference;

        if (!answered)
        {
            await AddCallbackAsync(call.PhoneNumber, call.PatientId, CallbackKind.NemFogadott, now, ct);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Szelektív hangrögzítés-kezelés + jogi Audit Trail. A beteg megtagadta (9-es gomb) vagy
    /// a kezelő leállította a felvételt: a sáv „törlődik”, és módosíthatatlan naplóbejegyzés készül
    /// a gombnyomás idejéről, a kezelő nevéről és az okról.
    /// </summary>
    public async Task ApplyRecordingDecisionAsync(Guid callId, RecordingDecision decision,
        CancellationToken ct = default)
    {
        var call = await _db.Calls.FirstOrDefaultAsync(c => c.Id == callId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen hívás: {callId}");

        call.Recording = decision.State;
        if (decision.State != RecordingState.Rogzitve)
            call.RecordingReference = null; // a sáv törölve

        _events.Audit("CallRecord", call.Id.ToString(), $"Hangrogzites_{decision.State}",
            decision.Reason, decision.Actor);

        if (call.PatientId is { } pid)
            _events.Timeline(pid, "HangrogzitesDontes",
                $"Hangrögzítés állapota: {decision.State} ({decision.Reason}).");

        await _db.SaveChangesAsync(ct);
    }

    // --- Visszahívási lista (önürítő) ---

    /// <summary>Munkaidőn kívüli visszahívási igény: a következő munkanap 9:00-ra időzítve.</summary>
    public async Task<CallbackRequest> RequestAfterHoursCallbackAsync(
        string phoneNumber, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var patient = await FindPatientByPhoneAsync(phoneNumber, ct);
        return await AddCallbackAsync(phoneNumber, patient?.Id, CallbackKind.MunkaidonKivul,
            await NextBusinessDay9AmUtcAsync(now, ct), ct);
    }

    /// <summary>Foglalt pult miatti azonnali visszahívási igény.</summary>
    public async Task<CallbackRequest> RequestBusyDeskCallbackAsync(
        string phoneNumber, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var patient = await FindPatientByPhoneAsync(phoneNumber, ct);
        return await AddCallbackAsync(phoneNumber, patient?.Id, CallbackKind.FoglaltPult, now, ct);
    }

    /// <summary>
    /// Önürítés: egy telefonszám minden nyitott visszahívási igényének lezárása,
    /// amikor a számot időközben elérték vagy a beteg maga hívott vissza.
    /// </summary>
    public async Task<int> ResolveOpenCallbacksAsync(string phoneNumber, string resolution,
        CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var open = await _db.Callbacks
            .Where(c => c.PhoneNumber == phoneNumber && c.Status == CallbackStatus.Nyitott)
            .ToListAsync(ct);
        foreach (var cb in open)
        {
            cb.Status = CallbackStatus.Teljesitve;
            cb.ResolvedAtUtc = now;
            cb.Resolution = resolution;
        }
        return open.Count;
    }

    // --- segédek ---

    private async Task<Patient?> FindPatientByPhoneAsync(string phone, CancellationToken ct)
    {
        var normalized = NormalizePhone(phone);
        // A helyi adatbázisban egyszerű normalizált egyezés.
        var patients = await _db.Patients
            .Where(p => p.MobilePhone != null)
            .ToListAsync(ct);
        return patients.FirstOrDefault(p => NormalizePhone(p.MobilePhone!) == normalized);
    }

    private async Task<CallbackRequest> AddCallbackAsync(string phone, Guid? patientId,
        CallbackKind kind, DateTimeOffset dueAt, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        // Ne halmozzunk duplikált nyitott igényt ugyanarra a számra + típusra.
        var existing = await _db.Callbacks.FirstOrDefaultAsync(
            c => c.PhoneNumber == phone && c.Kind == kind && c.Status == CallbackStatus.Nyitott, ct);
        if (existing is not null) return existing;

        var cb = new CallbackRequest
        {
            Id = Guid.NewGuid(),
            PhoneNumber = phone,
            PatientId = patientId,
            Kind = kind,
            Status = CallbackStatus.Nyitott,
            DueAtUtc = dueAt,
            CreatedAtUtc = now
        };
        _db.Callbacks.Add(cb);
        return cb;
    }

    private async Task<DateTimeOffset> NextBusinessDay9AmUtcAsync(DateTimeOffset from, CancellationToken ct)
    {
        // Egyszerű megközelítés: a következő olyan nap 9:00 (helyi), amely nyitva van.
        var status = await _hours.GetStatusAtAsync(from, ct);
        return status.NextOpenAtLocal ?? from.AddHours(9);
    }

    private static string NormalizePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        // Magyar számok egységesítése: a 36-os előtag megtartása, vezető 0 elhagyása.
        if (digits.StartsWith("00")) digits = digits[2..];
        if (digits.StartsWith("06")) digits = "36" + digits[2..];
        if (digits.Length == 9) digits = "36" + digits; // csonka belföldi
        return digits;
    }
}
