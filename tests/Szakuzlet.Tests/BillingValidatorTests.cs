using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Billing;
using Szakuzlet.Domain.Entities;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Tests;

public class BillingValidatorTests
{
    [Theory]
    [InlineData("270123456789", true)]
    [InlineData("990123456789", false)]
    [InlineData("", false)]
    public void Venykod_27_tel_kezdodik(string code, bool ok)
        => Assert.Equal(ok, BillingValidator.ValidatePrescriptionCode(code).Ok);

    [Theory]
    [InlineData("21000111", true)]
    [InlineData("31000111", false)]
    public void Matricakod_21_gyel_kezdodik(string code, bool ok)
        => Assert.Equal(ok, BillingValidator.ValidateStickerCode(code).Ok);

    [Theory]
    [InlineData("99123", true)]
    [InlineData("12345", false)]
    public void Orvoskod_99_cel_kezdodik(string code, bool ok)
        => Assert.Equal(ok, BillingValidator.ValidateDoctorCode(code).Ok);

    [Fact]
    public void Duplikalt_kod_kiszurve()
    {
        Assert.False(BillingValidator.ValidateNoDuplicate(new[] { "27A", "21B", "27A" }).Ok);
        Assert.True(BillingValidator.ValidateNoDuplicate(new[] { "27A", "21B" }).Ok);
    }

    [Fact]
    public void Postakoltseg_eseten_keszpenz_es_kartya_tiltott()
    {
        Assert.False(BillingValidator.ValidatePaymentConsistency(PaymentMethod.Keszpenz, hasPostageLine: true).Ok);
        Assert.False(BillingValidator.ValidatePaymentConsistency(PaymentMethod.Bankkartya, hasPostageLine: true).Ok);
        Assert.True(BillingValidator.ValidatePaymentConsistency(PaymentMethod.Atutalas, hasPostageLine: true).Ok);
        Assert.True(BillingValidator.ValidatePaymentConsistency(PaymentMethod.PostaiUtanvet, hasPostageLine: true).Ok);
    }

    [Fact]
    public void Postai_utanvetnel_kotelezo_a_postakoltseg()
    {
        var r = BillingValidator.ValidatePaymentConsistency(PaymentMethod.PostaiUtanvet, hasPostageLine: false);
        Assert.False(r.Ok);
        Assert.Contains("postaköltséget kötelező", r.Error);
    }

    [Fact]
    public async Task Sikertelen_kartyas_fizetes_eseten_nincs_szamla()
    {
        using var db = new TestDb();
        var clock = new FakeClock();
        var (billing, _, _, einv, terminal) = BillingTestFactory.Create(db, clock);
        terminal.Limit = 100_000; // 100 ezer feletti összeget elutasít

        var p = new Patient { Id = Guid.NewGuid(), FullName = "Kauciós Károly", Email = "k@x.hu", CreatedAtUtc = clock.UtcNow };
        db.Context.Patients.Add(p);
        await db.Context.SaveChangesAsync();

        var inv = await billing.CreateDraftAsync(p.Id, new[] { ((string?)"KAUCIO", "Kaució", 1) });
        var dbInv = await db.Context.Invoices.SingleAsync(i => i.Id == inv.Id);
        dbInv.PaymentMethod = PaymentMethod.Bankkartya;
        await db.Context.SaveChangesAsync();

        // 150 ezres kaució -> limit felett -> a fizetés elutasítva, számla NEM jön létre.
        await Assert.ThrowsAsync<InvalidOperationException>(() => billing.FinalizeAsync(inv.Id, cardAmount: 150_000));

        var refreshed = await db.Context.Invoices.SingleAsync(i => i.Id == inv.Id);
        Assert.Equal(InvoiceStatus.Kikuldheto, refreshed.Status); // nem véglegesült
        Assert.Null(refreshed.EInvoiceReference);                 // nincs e-számla
        Assert.Empty(einv.Issued);
    }

    [Fact]
    public async Task Sikeres_kartyas_fizetes_eseten_kiallitodik_a_szamla()
    {
        using var db = new TestDb();
        var clock = new FakeClock();
        var (billing, _, _, einv, terminal) = BillingTestFactory.Create(db, clock);
        terminal.Limit = 200_000;

        var p = new Patient { Id = Guid.NewGuid(), FullName = "Kauciós Károly", Email = "k@x.hu", CreatedAtUtc = clock.UtcNow };
        db.Context.Patients.Add(p);
        await db.Context.SaveChangesAsync();
        var inv = await billing.CreateDraftAsync(p.Id, new[] { ((string?)"KAUCIO", "Kaució", 1) });
        var dbInv = await db.Context.Invoices.SingleAsync(i => i.Id == inv.Id);
        dbInv.PaymentMethod = PaymentMethod.Bankkartya;
        await db.Context.SaveChangesAsync();

        await billing.FinalizeAsync(inv.Id, cardAmount: 150_000);

        var refreshed = await db.Context.Invoices.SingleAsync(i => i.Id == inv.Id);
        Assert.Equal(InvoiceStatus.Kikuldve, refreshed.Status);
        Assert.NotNull(refreshed.EInvoiceReference);
    }
}
