using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Billing;
using Szakuzlet.Application.Common;
using Szakuzlet.Domain.Entities;
using Szakuzlet.Domain.Enums;
using Szakuzlet.Infrastructure.Services;

namespace Szakuzlet.Tests;

public class BillingTests
{
    private static (BillingService svc, FakeClock clock, TestDb db) Build()
    {
        var db = new TestDb();
        var clock = new FakeClock();
        var (billing, _, _, _, _) = BillingTestFactory.Create(db, clock);
        return (billing, clock, db);
    }

    private static async Task<Patient> AddPatientAsync(TestDb db, FakeClock clock)
    {
        var p = new Patient
        {
            Id = Guid.NewGuid(), FullName = "Számla Sándor",
            Email = "sz@x.hu", MobilePhone = "+36301234567", TajNumber = "123456789",
            PostalCode = "1145", City = "Budapest", AddressLine = "Fő utca 1.",
            CreatedAtUtc = clock.UtcNow
        };
        db.Context.Patients.Add(p);
        await db.Context.SaveChangesAsync();
        return p;
    }

    [Fact]
    public async Task Uj_szamla_egyedi_sorszamot_es_tetelt_kap()
    {
        var (svc, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock);

        var inv1 = await svc.CreateDraftAsync(p.Id, new[] { ((string?)"BAKT", "Baktériumszűrő", 2) });
        var inv2 = await svc.CreateDraftAsync(p.Id, new[] { ((string?)"MASZK", "Orrmaszk M", 1) });

        Assert.StartsWith("SZ-", inv1.Number);
        Assert.NotEqual(inv1.Number, inv2.Number); // egyedi, monoton
        Assert.Equal(2, (await db.Context.InvoiceLines.CountAsync()));
        Assert.Equal(BillingCategory.Maganszemely, inv1.Category);
    }

    [Fact]
    public async Task Ceges_valtas_nem_torli_a_beteg_torzsadatait()
    {
        var (svc, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock);
        var inv = await svc.CreateDraftAsync(p.Id, new[] { ((string?)null, "CPAP készülék", 1) });

        await svc.SwitchToCompanyAsync(inv.Id,
            new CompanyBilling("Alvás Kft.", "12345678-2-42", "1051 Budapest, Céges út 3."));

        // A számlán céges adónem + elkülönített vevő-adatok.
        var refreshedInvoice = await db.Context.Invoices.Include(i => i.BillingParty)
            .SingleAsync(i => i.Id == inv.Id);
        Assert.Equal(BillingCategory.BelfoldiAdoalany, refreshedInvoice.Category);
        Assert.Equal("Alvás Kft.", refreshedInvoice.BillingParty!.Name);
        Assert.Equal("12345678-2-42", refreshedInvoice.BillingParty.TaxNumber);

        // A beteg magánszemélyes törzsadata VÁLTOZATLAN (a kritikus elvárás).
        var refreshedPatient = await db.Context.Patients.SingleAsync(x => x.Id == p.Id);
        Assert.Equal("Számla Sándor", refreshedPatient.FullName);
        Assert.Equal("1145", refreshedPatient.PostalCode);
        Assert.Equal("Fő utca 1.", refreshedPatient.AddressLine);
        Assert.Equal("sz@x.hu", refreshedPatient.Email);
    }

    [Fact]
    public async Task Visszavaltas_maganszemelyre_torli_a_ceges_blokkot_de_a_beteget_nem()
    {
        var (svc, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock);
        var inv = await svc.CreateDraftAsync(p.Id, new[] { ((string?)null, "Párásító", 1) });
        await svc.SwitchToCompanyAsync(inv.Id, new CompanyBilling("X Kft.", "11111111-1-11", "Cím"));

        await svc.SwitchToPrivateAsync(inv.Id);

        var refreshed = await db.Context.Invoices.Include(i => i.BillingParty).SingleAsync(i => i.Id == inv.Id);
        Assert.Equal(BillingCategory.Maganszemely, refreshed.Category);
        Assert.Null(refreshed.BillingParty);
        // A beteg érintetlen.
        Assert.Equal("Számla Sándor", (await db.Context.Patients.SingleAsync()).FullName);
    }

    [Fact]
    public async Task Kikuldott_szamla_adoneme_nem_modosithato()
    {
        var (svc, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock);
        var inv = await svc.CreateDraftAsync(p.Id, new[] { ((string?)null, "Szűrő", 1) });

        // Szimuláljuk a kiküldött állapotot.
        var dbInv = await db.Context.Invoices.SingleAsync(i => i.Id == inv.Id);
        dbInv.Status = InvoiceStatus.Kikuldve;
        await db.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SwitchToCompanyAsync(inv.Id, new CompanyBilling("Y", "2", "Z")));
    }
}
