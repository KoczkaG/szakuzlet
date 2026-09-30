using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Application.Common;
using Szakuzlet.Domain.Entities;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Application.CallCenter;

/// <summary>
/// Szoftverből indított kimenő hívások (Click-to-Call) és jogi védelem (I. Modul C).
///
/// A pultból/szervizből indított hívások integrálása: a beteg minden regisztrált száma mellett
/// hívásindító, a hívás indításakor felugró adatlap-kontextus, automatikus hangrögzítés a hívás
/// végén a Timeline-hoz linkelve, valamint a GDPR-figyelmeztetés és a leállítási logika
/// módosíthatatlan Audit Trail-lel.
/// </summary>
public sealed class OutboundCallService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly EventRecorder _events;

    public OutboundCallService(IAppDbContext db, IClock clock, EventRecorder events)
    {
        _db = db;
        _clock = clock;
        _events = events;
    }

    /// <summary>
    /// A beteg összes hívható száma (beteg mobil, házi szám, és jogilag jóváhagyott
    /// kapcsolattartó/hozzátartozó/megbízott). Ezek mellett jelenik meg a Click-to-Call ikon.
    /// A régi, egymezős törzsmobil is beolvad a listába, ha nincs külön PatientPhone rekord.
    /// </summary>
    public async Task<IReadOnlyList<DialableNumber>> GetDialableNumbersAsync(
        Guid patientId, CancellationToken ct = default)
    {
        var patient = await _db.Patients
            .Include(p => p.Phones)
            .FirstOrDefaultAsync(p => p.Id == patientId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen páciens: {patientId}");

        var list = patient.Phones
            .Select(ph => new DialableNumber(ph.Id, ph.Kind, ph.Number, ph.ContactName, ph.LegallyApproved))
            .ToList();

        // Visszafelé kompatibilitás: a törzsadat mobilját is felkínáljuk, ha nincs külön rekordja.
        if (!string.IsNullOrWhiteSpace(patient.MobilePhone)
            && !list.Any(d => Same(d.Number, patient.MobilePhone!)))
        {
            list.Insert(0, new DialableNumber(Guid.Empty, PhoneKind.BetegMobil,
                patient.MobilePhone!, null, LegallyApproved: true));
        }

        return list;
    }

    /// <summary>
    /// Kimenő hívás indítása egy adott számra. Rögzíti a hívást (rögzítés alapból bekapcsolva),
    /// Timeline-eseményt tesz a beteg adatlapjára, és visszaadja a felugró adatlap-kontextust
    /// a GDPR sablonnal és a gyanakvás-érvkészlettel.
    /// </summary>
    public async Task<OutboundCallStarted> StartCallAsync(
        Guid patientId, string number, PhoneKind kind,
        Guid? serviceWorksheetId = null, CancellationToken ct = default)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen páciens: {patientId}");

        var now = _clock.UtcNow;
        var call = new CallRecord
        {
            Id = Guid.NewGuid(),
            Direction = CallDirection.Kimeno,
            PhoneNumber = number,
            PatientId = patient.Id,
            DialedPhoneKind = kind,
            ServiceWorksheetId = serviceWorksheetId,
            Recording = RecordingState.Rogzitve,
            StartedAtUtc = now,
            Answered = false
        };
        _db.Calls.Add(call);

        _events.Audit("CallRecord", call.Id.ToString(), "KimenoHivasInditva",
            $"Szám: {number} ({kind})", "KEZELO");
        _events.Timeline(patient.Id, "KimenoHivas",
            $"Kimenő hívás indítva a(z) {number} számra ({kind}).");

        await _db.SaveChangesAsync(ct);

        return new OutboundCallStarted(
            call.Id, patient.Id, patient.FullName, number, kind,
            CallScripts.GdprPrompt, CallScripts.SuspicionScript);
    }

    /// <summary>
    /// A GDPR-leállítási logika: a hívott fél nem járul hozzá a rögzítéshez. A felvétel sávja
    /// törlődik, és módosíthatatlan Audit Trail rögzül (gombnyomás ideje, kezelő, ok).
    /// </summary>
    public async Task StopRecordingAsync(Guid callId, string actor, CancellationToken ct = default)
    {
        var call = await _db.Calls.FirstOrDefaultAsync(c => c.Id == callId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen hívás: {callId}");

        call.Recording = RecordingState.LeallitvaEsTorolve;
        call.RecordingReference = null;

        _events.Audit("CallRecord", call.Id.ToString(), "Hangrogzites_LeallitvaEsTorolve",
            "Ügyfél tiltása miatt felvétel kimenő hívásnál megszakítva és törölve.", actor);
        if (call.PatientId is { } pid)
            _events.Timeline(pid, "HangrogzitesDontes",
                "Kimenő hívás: a hívott fél tiltotta a rögzítést, a felvétel leállítva és törölve.");

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// A kimenő hívás lezárása. Ha rögzítettük, a hangfájl hivatkozása a hívásrekordhoz
    /// (és így a beteg Idővonalához / szerviz-munkalaphoz) linkelődik.
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

        _events.Audit("CallRecord", call.Id.ToString(), "KimenoHivasLezarva",
            $"Fogadva: {answered}");

        await _db.SaveChangesAsync(ct);
    }

    // --- Telefonszám-kezelés a beteg adatlapján ---

    /// <summary>Új telefonszám hozzáadása a beteghez (pl. jogilag jóváhagyott kapcsolattartó).</summary>
    public async Task<PatientPhone> AddPhoneAsync(Guid patientId, PhoneKind kind, string number,
        string? contactName, bool legallyApproved, CancellationToken ct = default)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen páciens: {patientId}");

        var phone = new PatientPhone
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Kind = kind,
            Number = number.Trim(),
            ContactName = string.IsNullOrWhiteSpace(contactName) ? null : contactName.Trim(),
            // A beteg saját száma mindig jóváhagyott; kapcsolattartónál külön jelölés.
            LegallyApproved = kind is PhoneKind.BetegMobil or PhoneKind.HaziSzam || legallyApproved
        };
        _db.Phones.Add(phone);
        await _db.SaveChangesAsync(ct);
        return phone;
    }

    private static bool Same(string a, string b)
    {
        string norm(string s) => new(s.Where(char.IsDigit).ToArray());
        return norm(a) == norm(b);
    }
}
