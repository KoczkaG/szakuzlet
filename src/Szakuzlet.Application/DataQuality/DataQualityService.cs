using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Application.Common;
using Szakuzlet.Application.Notifications;
using Szakuzlet.Domain.Entities;

namespace Szakuzlet.Application.DataQuality;

/// <summary>
/// Automata „ADATLAP HIÁNYOS” riasztási protokoll (I. Modul F). Adatlap-megnyitáskor
/// ellenőrzi a kötelező kontaktmezőket (e-mail, mobil, TAJ). Hiány esetén kétféle kezelési út:
///   „A” – helyszíni/telefonos frissítés + automata GDPR igazolás,
///   „B” – önkiszolgáló adatpótló link (SMS/e-mail).
/// Mindkettő végén webshopos marketing-terelés.
/// </summary>
public sealed class DataQualityService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly EventRecorder _events;
    private readonly INotificationSender _notifications;
    private readonly ITokenGenerator _tokens;

    private static readonly TimeSpan LinkLifetime = TimeSpan.FromDays(14);

    public DataQualityService(IAppDbContext db, IClock clock, EventRecorder events,
        INotificationSender notifications, ITokenGenerator tokens)
    {
        _db = db;
        _clock = clock;
        _events = events;
        _notifications = notifications;
        _tokens = tokens;
    }

    /// <summary>
    /// Feltételes riasztás: az adatlap megnyitásakor ellenőrzi a kötelező mezőket.
    /// Visszaadja, hogy van-e hiány, és pontosan mely mezők hiányoznak.
    /// </summary>
    public async Task<MissingContactAlert> CheckAsync(Guid patientId, CancellationToken ct = default)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen páciens: {patientId}");
        return Evaluate(patient);
    }

    private static MissingContactAlert Evaluate(Patient patient)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(patient.Email)) missing.Add("E-mail cím");
        if (string.IsNullOrWhiteSpace(patient.MobilePhone)) missing.Add("Mobiltelefonszám");
        if (string.IsNullOrWhiteSpace(patient.TajNumber)) missing.Add("TAJ szám");
        return new MissingContactAlert(missing.Count > 0, missing);
    }

    /// <summary>
    /// „A” opció: a pultos élőszóban egyeztetett adatokkal frissíti a profilt. Mentés után
    /// automata GDPR adatfrissítési igazolás megy ki (marketing-tereléssel).
    /// </summary>
    public async Task UpdateInPersonAsync(Guid patientId, ContactUpdate update, CancellationToken ct = default)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen páciens: {patientId}");

        var now = _clock.UtcNow;
        if (!string.IsNullOrWhiteSpace(update.Email)) patient.Email = update.Email.Trim();
        if (!string.IsNullOrWhiteSpace(update.MobilePhone)) patient.MobilePhone = update.MobilePhone.Trim();
        if (!string.IsNullOrWhiteSpace(update.TajNumber)) patient.TajNumber = update.TajNumber.Trim();
        patient.UpdatedAtUtc = now;

        _events.Audit("Patient", patient.Id.ToString(), "AdatfrissitesHelyszini",
            "Kapcsolattartási adat frissítve az ügyfél szóbeli jóváhagyásával.", "KEZELO");
        _events.Timeline(patient.Id, "AdatlapFrissitve",
            "Hiányzó kapcsolattartási adat pótolva (helyszíni/telefonos egyeztetés).");

        // Automata GDPR adatfrissítési igazolás + marketing-terelés (ha van e-mail).
        if (!string.IsNullOrWhiteSpace(patient.Email))
        {
            await _notifications.SendAsync(new NotificationMessage(
                NotificationKind.Email, patient.Email!,
                "Adatfrissítési igazolás",
                DataQualityMessages.GdprConfirmationBody + "\n\n" + DataQualityMessages.MarketingClosing), ct);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// „B” opció: önkiszolgáló adatpótló link generálása és kiküldése (SMS és/vagy e-mail).
    /// A linket a beteg tölti ki. Ha nincs elérhetőség, a link generálódik, de kiküldés nélkül
    /// (ilyenkor a pultnak kell a beteggel egyeztetnie).
    /// </summary>
    public async Task<DataCompletionRequest> SendSelfServiceLinkAsync(Guid patientId, CancellationToken ct = default)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen páciens: {patientId}");

        var now = _clock.UtcNow;
        var alert = Evaluate(patient);

        var request = new DataCompletionRequest
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Token = _tokens.CreateToken(),
            MissingFields = alert.MissingFieldsText,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(LinkLifetime)
        };
        _db.DataCompletionRequests.Add(request);

        _events.Audit("DataCompletionRequest", request.Id.ToString(), "OnkiszolgaloLinkKikuldve",
            $"Hiányzó: {alert.MissingFieldsText}");

        // Hibrid kiküldés: e-mail és/vagy SMS, amelyik elérhető.
        if (!string.IsNullOrWhiteSpace(patient.Email))
            await _notifications.SendAsync(new NotificationMessage(
                NotificationKind.Email, patient.Email!, "Hiányzó adatok pótlása",
                DataQualityMessages.SelfServicePrompt), ct);
        if (!string.IsNullOrWhiteSpace(patient.MobilePhone))
            await _notifications.SendAsync(new NotificationMessage(
                NotificationKind.Sms, patient.MobilePhone!, "Hiányzó adatok pótlása",
                DataQualityMessages.SelfServicePrompt), ct);

        await _db.SaveChangesAsync(ct);
        return request;
    }

    /// <summary>Az önkiszolgáló kérés betöltése token alapján (a linkre kattintva). Null, ha érvénytelen/lejárt.</summary>
    public async Task<DataCompletionRequest?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        var req = await _db.DataCompletionRequests
            .Include(r => r.Patient)
            .FirstOrDefaultAsync(r => r.Token == token, ct);
        if (req is null || req.Completed || req.ExpiresAtUtc < _clock.UtcNow) return null;
        return req;
    }

    /// <summary>
    /// A beteg beküldi a hiányzó adatokat a linken keresztül. A profil frissül,
    /// a kérés lezárul. A sikeres lezáró képernyőn jelenik meg a marketing-terelés.
    /// </summary>
    public async Task CompleteSelfServiceAsync(string token, ContactUpdate update, CancellationToken ct = default)
    {
        var req = await _db.DataCompletionRequests
            .Include(r => r.Patient)
            .FirstOrDefaultAsync(r => r.Token == token, ct)
            ?? throw new InvalidOperationException("Érvénytelen adatpótló token.");
        if (req.Completed)
            throw new InvalidOperationException("Ez a link már felhasználásra került.");
        if (req.ExpiresAtUtc < _clock.UtcNow)
            throw new InvalidOperationException("A link lejárt.");

        var now = _clock.UtcNow;
        var patient = req.Patient;
        if (!string.IsNullOrWhiteSpace(update.Email)) patient.Email = update.Email.Trim();
        if (!string.IsNullOrWhiteSpace(update.MobilePhone)) patient.MobilePhone = update.MobilePhone.Trim();
        if (!string.IsNullOrWhiteSpace(update.TajNumber)) patient.TajNumber = update.TajNumber.Trim();
        patient.UpdatedAtUtc = now;

        req.Completed = true;
        req.CompletedAtUtc = now;

        _events.Audit("DataCompletionRequest", req.Id.ToString(), "OnkiszolgaloAdatpotlasBekuldve",
            actor: "PACIENS");
        _events.Timeline(patient.Id, "AdatlapFrissitve",
            "Hiányzó kapcsolattartási adat pótolva (önkiszolgáló link).");

        await _db.SaveChangesAsync(ct);
    }
}
