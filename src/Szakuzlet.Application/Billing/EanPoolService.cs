using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Application.Common;
using Szakuzlet.Application.Notifications;
using Szakuzlet.Domain.Entities;

namespace Szakuzlet.Application.Billing;

/// <summary>
/// Virtuális EAN-kód Pool (II. Modul C). A hatóságtól digitálisan igényelt sorszámok tömbjét
/// kezeli a fizikai matricatekercsek helyett: importál, kioszt (a következő szabadat), és
/// figyeli a készletet – kritikus szint alatt riaszt a beszerzésnek.
/// </summary>
public sealed class EanPoolService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly EventRecorder _events;
    private readonly INotificationSender _notifications;

    /// <summary>Kritikus alsó készletszint, amely alatt riasztás megy a beszerzésnek.</summary>
    public const int CriticalLevel = 50;

    public EanPoolService(IAppDbContext db, IClock clock, EventRecorder events,
        INotificationSender notifications)
    {
        _db = db;
        _clock = clock;
        _events = events;
        _notifications = notifications;
    }

    /// <summary>Új digitális EAN-tömb feltöltése a hatóságtól kapott sorszámlistából.</summary>
    public async Task<int> ImportCodesAsync(IEnumerable<string> codes, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var incoming = codes
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .Distinct()
            .ToList();

        // A már létező kódokat kihagyjuk (idempotens import).
        var existing = await _db.EanCodes
            .Where(x => incoming.Contains(x.Code))
            .Select(x => x.Code)
            .ToListAsync(ct);

        var added = 0;
        foreach (var code in incoming.Where(c => !existing.Contains(c)))
        {
            _db.EanCodes.Add(new EanCode
            {
                Id = Guid.NewGuid(), Code = code, Used = false, ImportedAtUtc = now
            });
            added++;
        }

        if (added > 0)
        {
            _events.Audit("EanPool", "-", "DigitalisTombImportalva", $"{added} új sorszám.");
            await _db.SaveChangesAsync(ct);
        }
        return added;
    }

    /// <summary>A szabad (fel nem használt) kódok száma.</summary>
    public Task<int> AvailableCountAsync(CancellationToken ct = default)
        => _db.EanCodes.CountAsync(x => !x.Used, ct);

    /// <summary>
    /// A következő szabad EAN-sorszám kiosztása egy számlához. Kiosztás után ellenőrzi a
    /// készletszintet, és kritikus szint alatt riaszt a beszerzésnek.
    /// </summary>
    public async Task<string> AssignNextAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var code = await _db.EanCodes
            .Where(x => !x.Used)
            .OrderBy(x => x.ImportedAtUtc).ThenBy(x => x.Code)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Nincs szabad EAN-sorszám a poolban.");

        code.Used = true;
        code.AssignedToInvoiceId = invoiceId;
        code.AssignedAtUtc = now;

        await _db.SaveChangesAsync(ct);

        await CheckStockAsync(ct);
        return code.Code;
    }

    /// <summary>Készletellenőrzés: kritikus szint alatt automatikus riasztás a beszerzésnek.</summary>
    public async Task CheckStockAsync(CancellationToken ct = default)
    {
        var available = await AvailableCountAsync(ct);
        if (available < CriticalLevel)
        {
            _events.Audit("EanPool", "-", "KeszletRiasztas", $"Szabad sorszámok: {available}.");
            await _notifications.SendAsync(new NotificationMessage(
                NotificationKind.Email, "beszerzes@somnoshop.hu",
                "FIGYELEM: NEAK-matrica sorszámok fogytán",
                $"A szabad EAN-sorszámok száma {available} alá csökkent " +
                $"(kritikus szint: {CriticalLevel}). Új digitális tömbök igénylése szükséges!"), ct);
        }
    }
}
