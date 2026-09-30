using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Common;
using Szakuzlet.Application.Contracts;
using Szakuzlet.Domain.Entities;
using Szakuzlet.Domain.Enums;
using Szakuzlet.Infrastructure.Documents;
using Szakuzlet.Infrastructure.Signing;

namespace Szakuzlet.Tests;

public class ContractTests
{
    private static (ContractService svc, MockSignatureService sign, FakeClock clock, TestDb db) Build()
    {
        var db = new TestDb();
        var clock = new FakeClock();
        var events = new EventRecorder(db.Context, clock);
        var (billing, _, _, _, _) = BillingTestFactory.Create(db, clock);
        var docs = new MockDocumentGenerator();
        var sign = new MockSignatureService(clock);
        var svc = new ContractService(db.Context, clock, events, docs, sign, billing);
        return (svc, sign, clock, db);
    }

    private static async Task<Patient> AddPatientAsync(TestDb db, FakeClock clock)
    {
        var p = new Patient
        {
            Id = Guid.NewGuid(), FullName = "Szerződő Szilárd",
            MobilePhone = "+36301239999", Email = "sz@x.hu", CreatedAtUtc = clock.UtcNow
        };
        db.Context.Patients.Add(p);
        await db.Context.SaveChangesAsync();
        return p;
    }

    private static ContractDraftInput Draft() => new(
        ServiceChannel.SzemelyesPult, "AirSense 11", "SN-CPAP-001", "Orrmaszk M", 9.5m, 50_000m);

    [Fact]
    public async Task Elokeszites_szamlaz_baktriumszurot_kauciot_es_general_pdf_t()
    {
        var (svc, _, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock);

        var summary = await svc.PrepareAsync(p.Id, Draft());

        Assert.StartsWith("SZ-", summary.Number);
        Assert.Equal(50_000m, summary.DepositAmount);
        Assert.NotNull(summary.PdfReference);

        // Baktériumszűrő számla keletkezett (2 db).
        var line = await db.Context.InvoiceLines.SingleAsync();
        Assert.Equal("Baktériumszűrő", line.ProductName);
        Assert.Equal(2, line.Quantity);

        var contract = await db.Context.Contracts.SingleAsync();
        Assert.Equal(ContractStatus.Elokeszitve, contract.Status);
        Assert.Contains(await db.Context.TimelineEvents.ToListAsync(), t => t.EventType == "SzerzodesElokeszitve");
    }

    [Fact]
    public async Task SMS_kodos_alairas_sikeres_eseten_a_szerzodes_alairt_lesz()
    {
        var (svc, sign, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock);
        var summary = await svc.PrepareAsync(p.Id, Draft());

        var challenge = await svc.StartSmsSignatureAsync(summary.ContractId, p.MobilePhone!);
        var code = sign.PeekCode(challenge.ChallengeId)!; // mock: kiolvasható

        var ok = await svc.CompleteSmsSignatureAsync(summary.ContractId, challenge.ChallengeId, code, "1.2.3.4");

        Assert.True(ok);
        var contract = await db.Context.Contracts.SingleAsync();
        Assert.Equal(ContractStatus.Alairva, contract.Status);
        Assert.Equal(SignatureMethod.SmsKod, contract.SignatureMethod);
        Assert.Equal("1.2.3.4", contract.SignedFromIp);
        Assert.NotNull(contract.SignedAtUtc);
    }

    [Fact]
    public async Task Rossz_SMS_kod_eseten_a_szerzodes_nem_alairt()
    {
        var (svc, sign, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock);
        var summary = await svc.PrepareAsync(p.Id, Draft());
        var challenge = await svc.StartSmsSignatureAsync(summary.ContractId, p.MobilePhone!);

        var ok = await svc.CompleteSmsSignatureAsync(summary.ContractId, challenge.ChallengeId, "0000", null);

        Assert.False(ok);
        Assert.Equal(ContractStatus.Elokeszitve, (await db.Context.Contracts.SingleAsync()).Status);
    }

    [Fact]
    public async Task Papir_szkennelt_hibrid_alairas()
    {
        var (svc, _, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock);
        var summary = await svc.PrepareAsync(p.Id, Draft());

        await svc.SignOnPaperAsync(summary.ContractId, "scan/123.pdf", "Pultos Panna");

        var contract = await db.Context.Contracts.SingleAsync();
        Assert.Equal(ContractStatus.Alairva, contract.Status);
        Assert.Equal(SignatureMethod.PapirSzkennelt, contract.SignatureMethod);
        Assert.Equal("scan/123.pdf", contract.ScannedReference);
    }

    [Fact]
    public async Task Mar_alairt_szerzodes_ujra_nem_irhato_ala()
    {
        var (svc, _, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock);
        var summary = await svc.PrepareAsync(p.Id, Draft());
        await svc.SignOnPaperAsync(summary.ContractId, "scan/1.pdf", null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SignOnPaperAsync(summary.ContractId, "scan/2.pdf", null));
    }

    [Fact]
    public async Task OCR_kiolvassa_az_ambulans_lap_adatait()
    {
        var ocr = new MockOcrService();
        var raw = "name=Kovács Lajosné\ntaj=123456789\ndevice=AirSense 11\nmask=Orrmaszk M\npressure=9.5\nissued=2026-05-01";

        var data = await ocr.ExtractAmbulanceSheetAsync(raw);

        Assert.Equal("Kovács Lajosné", data.PatientName);
        Assert.Equal("AirSense 11", data.DeviceModel);
        Assert.Equal(9.5m, data.PressureCmH2O);
        Assert.Equal(new DateOnly(2026, 5, 1), data.IssuedOn);
    }
}
