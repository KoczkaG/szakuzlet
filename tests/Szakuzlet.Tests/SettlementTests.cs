using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Billing;
using Szakuzlet.Domain.Entities;

namespace Szakuzlet.Tests;

public class SettlementTests
{
    [Fact]
    public void Kaucio_elszamolo_helyesen_szamol()
    {
        using var db = new TestDb();
        var clock = new FakeClock();
        var svc = new SettlementService(db.Context, clock);

        var s = svc.BuildDepositSettlement(depositPaid: 100_000, tbSelfPart: 30_000, otherDeductions: 5_000);

        Assert.Equal(65_000, s.Refund);
        Assert.Contains("5 munkanapon belül", s.CommitmentText);
    }

    [Fact]
    public void Kaucio_elszamolo_nem_ad_negativ_visszajarot()
    {
        using var db = new TestDb();
        var clock = new FakeClock();
        var svc = new SettlementService(db.Context, clock);

        var s = svc.BuildDepositSettlement(50_000, 60_000, 0);
        Assert.Equal(0, s.Refund);
    }

    [Fact]
    public async Task OEP_egyezteto_termektipusonkent_osszesit_es_eltrest_jelez()
    {
        using var db = new TestDb();
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero) };
        var (billing, eanPool, _, _, _) = BillingTestFactory.Create(db, clock);
        await eanPool.ImportCodesAsync(new[] { "EAN-1", "EAN-2", "EAN-3" });

        var p = new Patient { Id = Guid.NewGuid(), FullName = "OEP Ottó", Email = "o@x.hu", CreatedAtUtc = clock.UtcNow };
        db.Context.Patients.Add(p);
        await db.Context.SaveChangesAsync();

        // 2 vényes CPAP + 1 vényes maszk, mind kiküldve (aznap).
        foreach (var (name, qty) in new[] { ("CPAP készülék", 1), ("CPAP készülék", 1), ("Orrmaszk M", 1) })
        {
            var inv = await billing.CreateDraftAsync(p.Id, new[] { ((string?)null, name, qty) });
            await billing.MarkPrescriptionAsync(inv.Id, true);
            await billing.FinalizeAsync(inv.Id);
        }

        var settlement = new SettlementService(db.Context, clock);
        var day = new DateOnly(2026, 6, 10);

        // Mankó szerint 2 CPAP (egyezik) és 2 maszk (a KVL szerint csak 1 -> eltérés).
        var manko = new Dictionary<string, int> { ["CPAP készülék"] = 2, ["Orrmaszk M"] = 2 };
        var result = await settlement.ReconcileDayAsync(day, manko);

        var cpap = result.Lines.Single(l => l.ProductName == "CPAP készülék");
        var mask = result.Lines.Single(l => l.ProductName == "Orrmaszk M");
        Assert.Equal(2, cpap.KvlCount);
        Assert.False(cpap.HasDiscrepancy);
        Assert.Equal(1, mask.KvlCount);
        Assert.True(mask.HasDiscrepancy);
        Assert.True(result.AnyDiscrepancy);
    }

    [Fact]
    public async Task Kihordasi_ido_kovetes_a_finalizalaskor_beallitja_az_eladasi_datumot()
    {
        using var db = new TestDb();
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero) };
        var (billing, _, _, _, _) = BillingTestFactory.Create(db, clock);

        var p = new Patient { Id = Guid.NewGuid(), FullName = "Kihordás Kál", Email = "k@x.hu", CreatedAtUtc = clock.UtcNow };
        db.Context.Patients.Add(p);
        await db.Context.SaveChangesAsync();

        var inv = await billing.CreateDraftAsync(p.Id, new[] { ((string?)"MASZK", "Orrmaszk", 1) });
        await billing.FinalizeAsync(inv.Id);

        var refreshed = await db.Context.Patients.SingleAsync();
        Assert.Equal(new DateOnly(2026, 6, 10), refreshed.LastPurchaseDate);
    }
}
