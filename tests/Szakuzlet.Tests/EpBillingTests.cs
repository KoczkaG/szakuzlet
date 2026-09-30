using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Billing;
using Szakuzlet.Application.Common;
using Szakuzlet.Domain.Entities;
using Szakuzlet.Domain.Enums;
using Szakuzlet.Infrastructure.Services;

namespace Szakuzlet.Tests;

public class EpBillingTests
{
    private static (BillingService svc, FakeClock clock, TestDb db) Build()
    {
        var db = new TestDb();
        var clock = new FakeClock();
        var (billing, _, _, _, _) = BillingTestFactory.Create(db, clock);
        return (billing, clock, db);
    }

    private static async Task<(Patient p, HealthFund normal, HealthFund strict)> SeedAsync(
        TestDb db, FakeClock clock)
    {
        var p = new Patient
        {
            Id = Guid.NewGuid(), FullName = "Kovács Lajosné",
            PostalCode = "1145", City = "Budapest", AddressLine = "Lakatos utca 22.",
            CreatedAtUtc = clock.UtcNow
        };
        var normal = new HealthFund { Id = Guid.NewGuid(), Name = "OTP Egészségpénztár", StrictBilling = false, IsActive = true };
        var strict = new HealthFund
        {
            Id = Guid.NewGuid(), Name = "Prémium Egészségpénztár", StrictBilling = true,
            OfficialAddress = "1051 Budapest, Pénztár utca 5.", TaxNumber = "18000000-2-41", IsActive = true
        };
        db.Context.Patients.Add(p);
        db.Context.HealthFunds.AddRange(normal, strict);
        await db.Context.SaveChangesAsync();
        return (p, normal, strict);
    }

    [Fact]
    public void Nevosszefuzes_a_szigoru_hierarchikus_sorrendet_adja()
    {
        var name = BillingService.ComposeEpName("Kovács Lajosné", "OTP Egészségpénztár",
            beneficiary: "Kovács Lajos", memberId: "555111");
        Assert.Equal(
            "Kovács Lajosné / OTP Egészségpénztár (Kedvezményezett: Kovács Lajos / Tagi azonosító: 555111",
            name);
    }

    [Fact]
    public void Nevosszefuzes_kedvezmenyezett_nelkul()
    {
        var name = BillingService.ComposeEpName("Kovács Lajosné", "OTP Egészségpénztár", null, "555111");
        Assert.Equal("Kovács Lajosné / OTP Egészségpénztár (Tagi azonosító: 555111", name);
    }

    [Fact]
    public async Task Konnyitett_EP_a_beteg_lakcimere_allit_es_osszefuzi_a_nevet()
    {
        var (svc, clock, db) = Build();
        using var _db = db;
        var (p, normal, _) = await SeedAsync(db, clock);
        var inv = await svc.CreateDraftAsync(p.Id, new[] { ((string?)"MASZK", "Orrmaszk M", 1) });

        var isStrict = await svc.SwitchToEpAsync(inv.Id, new EpBilling(normal.Id, "555111", "Kovács Lajos"));

        Assert.False(isStrict);
        var refreshed = await db.Context.Invoices.Include(i => i.BillingParty).SingleAsync(i => i.Id == inv.Id);
        Assert.Equal(BillingCategory.Egeszsegpenztar, refreshed.Category);
        Assert.Contains("OTP Egészségpénztár", refreshed.BillingParty!.Name);
        Assert.Contains("Kedvezményezett: Kovács Lajos", refreshed.BillingParty.Name);
        // Könnyített: a beteg lakcíme, nincs adószám.
        Assert.Null(refreshed.BillingParty.TaxNumber);
        Assert.Contains("Lakatos utca 22.", refreshed.BillingParty.Address);
    }

    [Fact]
    public async Task Szigoru_EP_riasztast_ad_es_beemeli_a_szekhelyet_es_adoszamot()
    {
        var (svc, clock, db) = Build();
        using var _db = db;
        var (p, _, strict) = await SeedAsync(db, clock);
        var inv = await svc.CreateDraftAsync(p.Id, new[] { ((string?)"CPAP", "CPAP készülék", 1) });

        var isStrict = await svc.SwitchToEpAsync(inv.Id, new EpBilling(strict.Id, "999888", null));

        Assert.True(isStrict); // a pult ekkor kapja a figyelmeztetést
        var refreshed = await db.Context.Invoices.Include(i => i.BillingParty).SingleAsync(i => i.Id == inv.Id);
        Assert.Equal(BillingCategory.BelfoldiAdoalany, refreshed.Category);
        Assert.Equal("18000000-2-41", refreshed.BillingParty!.TaxNumber);
        Assert.Equal("1051 Budapest, Pénztár utca 5.", refreshed.BillingParty.Address);

        // A beteg profilja érintetlen.
        Assert.Equal("Lakatos utca 22.", (await db.Context.Patients.SingleAsync()).AddressLine);
    }
}
