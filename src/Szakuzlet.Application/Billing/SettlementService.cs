using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Application.Billing;

/// <summary>
/// Automata háttér-statisztika és OEP napi egyeztető (II. Modul E). Kiváltja a külső Excel-táblák
/// manuális vezetését és a kéthetenkénti OEP-elszámolási papírválogatást: a KVL-es adatokból
/// azonnali, terméktípusonkénti darabszám-egyeztetést és átlátható kaució-elszámolást ad.
/// </summary>
public sealed class SettlementService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public SettlementService(IAppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>
    /// Kaució-elszámoló adatlap összeállítása: befizetett kaució mínusz a TB-önrész és egyéb
    /// levonások = visszajáró nettó összeg, fix megnyugtató időbeli vállalással.
    /// </summary>
    public DepositSettlement BuildDepositSettlement(decimal depositPaid, decimal tbSelfPart, decimal otherDeductions)
    {
        var refund = depositPaid - tbSelfPart - otherDeductions;
        if (refund < 0) refund = 0;
        return new DepositSettlement(
            depositPaid, tbSelfPart, otherDeductions, refund,
            "A visszajáró összeget cégünk 5 munkanapon belül banki átutalással visszatéríti az Ön számlájára.");
    }

    /// <summary>
    /// Napi OEP egyeztető: az adott nap vényes, kiküldött számláinak terméktételeit terméktípusonként
    /// összesíti (KVL darabszám). A hívó a Mankó összesítő darabszámait adja meg terméknevenként;
    /// a szolgáltatás azonnal jelzi az eltéréseket.
    /// </summary>
    public async Task<OepReconciliation> ReconcileDayAsync(
        DateOnly day, IReadOnlyDictionary<string, int>? mankoCounts = null, CancellationToken ct = default)
    {
        // A nap [00:00, 24:00) intervalluma UTC-ben (egyszerűsítés; a lezárás SentAtUtc-je alapján).
        var from = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var to = from.AddDays(1);

        var lines = await _db.Invoices
            .Where(i => i.IsPrescription && i.Status == InvoiceStatus.Kikuldve
                        && i.SentAtUtc != null && i.SentAtUtc >= from && i.SentAtUtc < to)
            .SelectMany(i => i.Lines)
            .ToListAsync(ct);

        var grouped = lines
            .GroupBy(l => l.ProductName)
            .Select(g =>
            {
                int? manko = mankoCounts is not null && mankoCounts.TryGetValue(g.Key, out var m) ? m : null;
                return new OepLine(g.Key, g.Sum(x => x.Quantity), manko);
            })
            .OrderBy(l => l.ProductName)
            .ToList();

        return new OepReconciliation(day, grouped);
    }
}
