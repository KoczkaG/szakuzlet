using Microsoft.EntityFrameworkCore;
using Szakuzlet.Domain.Entities;

namespace Szakuzlet.Tests;

public class EanPoolTests
{
    private static async Task<Patient> AddPatientAsync(TestDb db, FakeClock clock, string? email)
    {
        var p = new Patient
        {
            Id = Guid.NewGuid(), FullName = "Vényes Viktor", Email = email,
            CreatedAtUtc = clock.UtcNow
        };
        db.Context.Patients.Add(p);
        await db.Context.SaveChangesAsync();
        return p;
    }

    [Fact]
    public async Task Import_idempotens_es_a_szabad_szamot_noveli()
    {
        using var db = new TestDb();
        var clock = new FakeClock();
        var (_, eanPool, _, _, _) = BillingTestFactory.Create(db, clock);

        var added1 = await eanPool.ImportCodesAsync(new[] { "EAN-1", "EAN-2", "EAN-2" }); // duplikátum kiszűrve
        var added2 = await eanPool.ImportCodesAsync(new[] { "EAN-2", "EAN-3" });          // EAN-2 már létezik

        Assert.Equal(2, added1);
        Assert.Equal(1, added2);
        Assert.Equal(3, await eanPool.AvailableCountAsync());
    }

    [Fact]
    public async Task Kritikus_szint_alatt_riaszt_a_beszerzesnek()
    {
        using var db = new TestDb();
        var clock = new FakeClock();
        var (_, eanPool, notif, _, _) = BillingTestFactory.Create(db, clock);

        // Kevés kód (a kritikus szint alatt), majd ellenőrzés.
        await eanPool.ImportCodesAsync(new[] { "E-1", "E-2" });
        await eanPool.CheckStockAsync();

        Assert.Contains(notif.Sent, m => m.Subject.Contains("sorszámok fogytán"));
    }

    [Fact]
    public async Task Venyes_finalizalas_EAN_kodot_eget_ra_es_e_szamlat_allit_ki()
    {
        using var db = new TestDb();
        var clock = new FakeClock();
        var (billing, eanPool, _, einv, _) = BillingTestFactory.Create(db, clock);
        var p = await AddPatientAsync(db, clock, "v@x.hu");
        await eanPool.ImportCodesAsync(new[] { "EAN-1001", "EAN-1002" });

        var inv = await billing.CreateDraftAsync(p.Id, new[] { ((string?)"CPAP", "CPAP készülék", 1) });
        await billing.MarkPrescriptionAsync(inv.Id, true);

        var printNeeded = await billing.FinalizeAsync(inv.Id);

        var refreshed = await db.Context.Invoices.SingleAsync(i => i.Id == inv.Id);
        Assert.False(printNeeded);                       // van e-mail -> digitális
        Assert.NotNull(refreshed.AssignedEanCode);       // EAN ráégetve
        Assert.StartsWith("EINV-", refreshed.EInvoiceReference); // e-számla kiállítva
        Assert.Single(einv.Issued);
        // A kiosztott kód "used".
        Assert.Equal(1, await eanPool.AvailableCountAsync());
    }

    [Fact]
    public async Task Email_nelkuli_ugyfel_eseten_helyi_nyomtatas_szukseges()
    {
        using var db = new TestDb();
        var clock = new FakeClock();
        var (billing, _, _, _, _) = BillingTestFactory.Create(db, clock);
        var p = await AddPatientAsync(db, clock, email: null);

        var inv = await billing.CreateDraftAsync(p.Id, new[] { ((string?)null, "Szűrő", 1) });
        var printNeeded = await billing.FinalizeAsync(inv.Id);

        Assert.True(printNeeded);
    }

    [Fact]
    public async Task Nincs_szabad_kod_eseten_a_venyes_finalizalas_hibat_dob()
    {
        using var db = new TestDb();
        var clock = new FakeClock();
        var (billing, _, _, _, _) = BillingTestFactory.Create(db, clock);
        var p = await AddPatientAsync(db, clock, "v@x.hu");

        var inv = await billing.CreateDraftAsync(p.Id, new[] { ((string?)"CPAP", "CPAP", 1) });
        await billing.MarkPrescriptionAsync(inv.Id, true);

        // Nincs importált EAN -> a kiosztás hibázik.
        await Assert.ThrowsAsync<InvalidOperationException>(() => billing.FinalizeAsync(inv.Id));
    }
}
