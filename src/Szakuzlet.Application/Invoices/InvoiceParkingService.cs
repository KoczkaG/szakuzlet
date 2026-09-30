using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Application.Common;
using Szakuzlet.Application.Notifications;
using Szakuzlet.Domain.Entities;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Application.Invoices;

/// <summary>
/// „Zéró Hozzájárulás” és Számla-Parkoltatási Radar (I. Modul A, feladatlista 8. pont).
///
/// Ha egy beteg sem a postai, sem az e-mailes információküldéshez nem járult hozzá,
/// a számla automatikus kiküldése blokkolódik, a számla 1 hónapra „parkolva” marad,
/// és a rendszer tisztázó pulti telefonhívás-feladatot generál.
/// </summary>
public sealed class InvoiceParkingService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly EventRecorder _events;
    private readonly INotificationSender _notifications;

    private static readonly TimeSpan ParkingWindow = TimeSpan.FromDays(30);

    public InvoiceParkingService(IAppDbContext db, IClock clock, EventRecorder events,
        INotificationSender notifications)
    {
        _db = db;
        _clock = clock;
        _events = events;
        _notifications = notifications;
    }

    /// <summary>
    /// Számla kiállítása a radar logikával. Ha van postai VAGY e-mailes hozzájárulás,
    /// a számla kiküldhető; egyébként parkoltatásra kerül és pulti feladat keletkezik.
    /// </summary>
    public async Task<Invoice> IssueAsync(Guid patientId, string number, CancellationToken ct = default)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen páciens: {patientId}");

        var now = _clock.UtcNow;
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            Number = number,
            CreatedAtUtc = now
        };

        var hasSendChannel = await HasAnySendConsentAsync(patientId, ct);
        if (hasSendChannel)
        {
            invoice.Status = InvoiceStatus.Kikuldheto;
            _db.Invoices.Add(invoice);
            _events.Audit("Invoice", invoice.Id.ToString(), "SzamlaKikuldheto",
                $"Számla: {number}");
        }
        else
        {
            // Radar aktiválás: blokkolás + parkoltatás + tisztázó feladat.
            invoice.Status = InvoiceStatus.Parkoltatva;
            invoice.ParkingExpiresAtUtc = now.Add(ParkingWindow);
            _db.Invoices.Add(invoice);

            _db.Tasks.Add(new PatientTask
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                Type = PatientTaskType.ZeroHozzajarulasTisztazas,
                Status = PatientTaskStatus.Nyitott,
                Title = "Tisztázó telefonhívás – nincs küldési hozzájárulás",
                Details = $"A(z) {number} számú számla parkoltatva. Egyeztetni kell a beteggel: " +
                          "téves kitöltés, vagy személyesen jön érte.",
                InvoiceId = invoice.Id,
                CreatedAtUtc = now
            });

            _events.Audit("Invoice", invoice.Id.ToString(), "SzamlaParkoltatva",
                $"Számla: {number}; parkoltatás {invoice.ParkingExpiresAtUtc:u}-ig.");
            _events.Timeline(patientId, "SzamlaParkoltatva",
                $"A(z) {number} számú számla parkoltatva – nincs küldési hozzájárulás, egyeztetésre vár.");
        }

        await _db.SaveChangesAsync(ct);
        return invoice;
    }

    /// <summary>
    /// Telefonos egyeztetés kimenetele: TÉVES kitöltés. A kolléga az ügyfél szóbeli
    /// jóváhagyásával korrigálja a hozzájárulást, a rendszer frissítési linket küld,
    /// a számla kiküldhetővé válik, a feladat lezárul.
    /// </summary>
    public async Task ResolveAsWrongEntryAsync(Guid invoiceId, bool allowPostal, bool allowEmail,
        CancellationToken ct = default)
    {
        var invoice = await LoadParkedAsync(invoiceId, ct);
        var patient = await _db.Patients.FirstAsync(p => p.Id == invoice.PatientId, ct);
        var now = _clock.UtcNow;

        // A korrigált hozzájárulás rögzítése (a legutóbbi adatlaphoz kötve).
        var latestSheet = await _db.DataSheets
            .Where(s => s.PatientId == patient.Id)
            .OrderByDescending(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
        if (latestSheet is not null)
        {
            AddConsent(patient.Id, latestSheet.Id, ConsentChannel.PostaiKuldemeny, allowPostal, now);
            AddConsent(patient.Id, latestSheet.Id, ConsentChannel.EmailKuldemeny, allowEmail, now);
        }

        invoice.Status = InvoiceStatus.Kikuldheto;
        await CloseInvoiceTasksAsync(invoice.Id, now, ct);

        // Megerősítő frissítési link a páciensnek.
        if (!string.IsNullOrWhiteSpace(patient.Email))
            await _notifications.SendAsync(new NotificationMessage(
                NotificationKind.Email, patient.Email!,
                "Adatlapja módosításáról",
                "Tájékoztatjuk, hogy adatlapját munkatársunk az Ön kérésére frissítette. " +
                "A módosítást az alábbi linken ellenőrizheti."), ct);

        _events.Audit("Invoice", invoice.Id.ToString(), "ParkoltatasFeloldva_TevesKitoltes");
        _events.Timeline(patient.Id, "SzamlaParkoltatasFeloldva",
            "Téves kitöltés korrigálva, a számla kiküldhető.");

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Telefonos egyeztetés kimenetele: az ügyfél SZEMÉLYESEN jön érte. A számla parkolva
    /// marad, majd a helyszíni kiszolgáláskor nyomtatható. A feladat lezárul.
    /// </summary>
    public async Task ResolveAsPersonalPickupAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var invoice = await LoadParkedAsync(invoiceId, ct);
        var now = _clock.UtcNow;
        await CloseInvoiceTasksAsync(invoice.Id, now, ct);

        _events.Audit("Invoice", invoice.Id.ToString(), "Parkoltatas_SzemelyesAtvetel");
        _events.Timeline(invoice.PatientId, "SzamlaParkoltatasEgyeztetve",
            "Az ügyfél személyesen jön a számláért; parkolva marad a helyszíni nyomtatásig.");

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Időzített lezárás: a lejárt (1 hónapos) parkoltatások automatikus lezárása,
    /// függetlenül attól, sikerült-e egyeztetni. Timeline-bejegyzést helyez el.
    /// Visszaadja a lezárt számlák számát. (Ütemezett háttérfolyamat hívja.)
    /// </summary>
    public async Task<int> CloseExpiredParkingsAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var expired = await _db.Invoices
            .Where(i => i.Status == InvoiceStatus.Parkoltatva
                        && i.ParkingExpiresAtUtc != null
                        && i.ParkingExpiresAtUtc <= now)
            .ToListAsync(ct);

        foreach (var invoice in expired)
        {
            invoice.Status = InvoiceStatus.ParkoltatasLezarva;
            await CloseInvoiceTasksAsync(invoice.Id, now, ct);
            _events.Audit("Invoice", invoice.Id.ToString(), "ParkoltatasIdozitettLezaras",
                $"1 hónapos határidő lejárt {now:u}.");
            _events.Timeline(invoice.PatientId, "SzamlaParkoltatasLezarva",
                $"A(z) {invoice.Number} számú számla parkoltatása lejárt és lezárva ({now:yyyy-MM-dd}).");
        }

        if (expired.Count > 0)
            await _db.SaveChangesAsync(ct);
        return expired.Count;
    }

    // --- segédek ---

    private async Task<bool> HasAnySendConsentAsync(Guid patientId, CancellationToken ct)
    {
        // A legfrissebb, releváns hozzájárulás számít csatornánként.
        var consents = await _db.Consents
            .Where(c => c.PatientId == patientId
                        && (c.Channel == ConsentChannel.PostaiKuldemeny
                            || c.Channel == ConsentChannel.EmailKuldemeny))
            .OrderByDescending(c => c.RecordedAtUtc)
            .ToListAsync(ct);

        var postal = consents.FirstOrDefault(c => c.Channel == ConsentChannel.PostaiKuldemeny)?.Granted ?? false;
        var email = consents.FirstOrDefault(c => c.Channel == ConsentChannel.EmailKuldemeny)?.Granted ?? false;
        return postal || email;
    }

    private async Task<Invoice> LoadParkedAsync(Guid invoiceId, CancellationToken ct)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen számla: {invoiceId}");
        if (invoice.Status != InvoiceStatus.Parkoltatva)
            throw new InvalidOperationException("A számla nincs parkoltatva.");
        return invoice;
    }

    private async Task CloseInvoiceTasksAsync(Guid invoiceId, DateTimeOffset now, CancellationToken ct)
    {
        var tasks = await _db.Tasks
            .Where(t => t.InvoiceId == invoiceId && t.Status == PatientTaskStatus.Nyitott)
            .ToListAsync(ct);
        foreach (var t in tasks)
        {
            t.Status = PatientTaskStatus.Lezart;
            t.ClosedAtUtc = now;
        }
    }

    private void AddConsent(Guid patientId, Guid dataSheetId, ConsentChannel channel, bool granted, DateTimeOffset now)
    {
        _db.Consents.Add(new ConsentRecord
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            DataSheetId = dataSheetId,
            Channel = channel,
            Granted = granted,
            RecordedAtUtc = now
        });
    }
}
