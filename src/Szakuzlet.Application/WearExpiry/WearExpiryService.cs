using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Application.Common;
using Szakuzlet.Application.Notifications;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Application.WearExpiry;

/// <summary>
/// Kihordási idő automatizmus (I. Modul A, feladatlista 9. pont).
///
/// A vásárlási dátum és a termék kihordási ideje alapján automatikus értesítést küld,
/// amikor a beteg jogosulttá válik az új, TB-támogatott eszközre. Csak azoknak, akik a
/// kihordási idő tájékoztatáshoz hozzájárultak. Ütemezett háttérfolyamat hívja.
/// </summary>
public sealed class WearExpiryService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly EventRecorder _events;
    private readonly INotificationSender _notifications;

    public WearExpiryService(IAppDbContext db, IClock clock, EventRecorder events,
        INotificationSender notifications)
    {
        _db = db;
        _clock = clock;
        _events = events;
        _notifications = notifications;
    }

    /// <summary>
    /// Végigmegy a jogosulttá vált betegeken, és értesítőt küld. Visszaadja a kiküldött értesítők számát.
    /// </summary>
    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);

        var candidates = await _db.Patients
            .Where(p => !p.WearExpiryNotified
                        && p.LastPurchaseDate != null
                        && p.ProductWearMonths != null)
            .ToListAsync(ct);

        var sent = 0;
        foreach (var p in candidates)
        {
            var expiry = p.LastPurchaseDate!.Value.AddMonths(p.ProductWearMonths!.Value);
            if (expiry > today) continue; // még nem járt le

            // Csak hozzájárulás esetén (a legfrissebb releváns válasz alapján).
            var consented = await LatestConsentGrantedAsync(p.Id, ConsentChannel.KihordasiIdoTajekoztatas, ct);
            if (!consented) continue;

            var channel = !string.IsNullOrWhiteSpace(p.MobilePhone)
                ? (NotificationKind.Sms, p.MobilePhone!)
                : !string.IsNullOrWhiteSpace(p.Email)
                    ? (NotificationKind.Email, p.Email!)
                    : ((NotificationKind?)null, (string?)null);

            if (channel.Item1 is null) continue; // nincs elérhetőség

            await _notifications.SendAsync(new NotificationMessage(
                channel.Item1.Value, channel.Item2!,
                "Lejárt a kihordási idő – új eszköz igényelhető",
                "Tájékoztatjuk, hogy eszköze kihordási ideje lejárt, így jogosulttá vált az új, " +
                "TB-támogatott eszközre. Kérjük, keresse fel szaküzletünket vagy webáruházunkat."), ct);

            p.WearExpiryNotified = true;
            _events.Audit("Patient", p.Id.ToString(), "KihordasiIdoErtesitoKikuldve",
                $"Lejárat: {expiry:yyyy-MM-dd}");
            _events.Timeline(p.Id, "KihordasiIdoErtesito",
                $"Kihordási idő lejárt ({expiry:yyyy-MM-dd}), értesítő kiküldve az új eszközről.");
            sent++;
        }

        if (sent > 0)
            await _db.SaveChangesAsync(ct);
        return sent;
    }

    private async Task<bool> LatestConsentGrantedAsync(Guid patientId, ConsentChannel channel, CancellationToken ct)
    {
        var latest = await _db.Consents
            .Where(c => c.PatientId == patientId && c.Channel == channel)
            .OrderByDescending(c => c.RecordedAtUtc)
            .FirstOrDefaultAsync(ct);
        return latest?.Granted ?? false;
    }
}
