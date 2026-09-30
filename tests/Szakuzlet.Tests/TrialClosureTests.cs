using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Banking;
using Szakuzlet.Application.Billing;
using Szakuzlet.Application.Common;
using Szakuzlet.Application.Contracts;
using Szakuzlet.Domain.Entities;
using Szakuzlet.Domain.Enums;
using Szakuzlet.Infrastructure.Banking;

namespace Szakuzlet.Tests;

public class TrialClosureTests
{
    private static (TrialClosureService svc, MockPayoutExporter payout, FakeClock clock, TestDb db) Build()
    {
        var db = new TestDb();
        var clock = new FakeClock();
        var events = new EventRecorder(db.Context, clock);
        var settlement = new SettlementService(db.Context, clock);
        var payout = new MockPayoutExporter(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MockPayoutExporter>.Instance);
        return (new TrialClosureService(db.Context, clock, events, settlement, payout), payout, clock, db);
    }

    private static async Task<Contract> AddContractAsync(TestDb db, FakeClock clock, decimal deposit = 100_000m)
    {
        var p = new Patient
        {
            Id = Guid.NewGuid(), FullName = "Záró Zoltán", PostalCode = "1145", City = "Budapest",
            AddressLine = "Fő utca 1.", CreatedAtUtc = clock.UtcNow
        };
        var c = new Contract
        {
            Id = Guid.NewGuid(), PatientId = p.Id, Number = $"SZ-T-{Guid.NewGuid():N}".Substring(0, 20),
            DeviceModel = "AirSense 11", DeviceSerialNumber = "SN-1", DepositAmount = deposit,
            Status = ContractStatus.ProbaFut, CreatedAtUtc = clock.UtcNow
        };
        db.Context.Patients.Add(p);
        db.Context.Contracts.Add(c);
        await db.Context.SaveChangesAsync();
        return c;
    }

    [Fact]
    public async Task Sikeres_lezaras_TB_kulonbseggel_es_SEPA_exporttal()
    {
        var (svc, payout, clock, db) = Build();
        using var _db = db;
        var c = await AddContractAsync(db, clock);

        var s = await svc.CloseSuccessfulAsync(c.Id, tbSelfPart: 30_000, otherDeductions: 0,
            bank: new RefundBankDetails("Záró Zoltán", "HU42117730161111101800000000", "OTP"));

        Assert.Equal(70_000, s.Refund);
        Assert.Equal(ContractStatus.Lezarva, (await db.Context.Contracts.SingleAsync()).Status);
        Assert.Single(payout.Sepa);
        Assert.Equal(70_000, payout.Sepa.First().Amount);
    }

    [Fact]
    public async Task Bankszamla_nelkul_postai_csekk_megy()
    {
        var (svc, payout, clock, db) = Build();
        using var _db = db;
        var c = await AddContractAsync(db, clock);

        await svc.CloseSuccessfulAsync(c.Id, tbSelfPart: 20_000, otherDeductions: 0, bank: null);

        Assert.Empty(payout.Sepa);
        Assert.Single(payout.Postal);
        Assert.Equal(80_000, payout.Postal.First().Amount);
    }

    [Fact]
    public async Task Hosszabbitas_nyitva_hagyja_a_szerzodest()
    {
        var (svc, _, clock, db) = Build();
        using var _db = db;
        var c = await AddContractAsync(db, clock);

        await svc.ExtendTrialAsync(c.Id, new DateOnly(2026, 9, 1));

        var refreshed = await db.Context.Contracts.SingleAsync();
        Assert.Equal(ContractStatus.ProbaFut, refreshed.Status); // nem lezárva
    }

    [Fact]
    public async Task Elutasitas_meltanyossagi_felulbiralassal_es_karantennal()
    {
        var (svc, _, clock, db) = Build();
        using var _db = db;
        var c = await AddContractAsync(db, clock);

        // Bérleti 20e + tisztítás 5e = 25e levonás, de 15e-t elengedünk -> 10e levonás.
        var s = await svc.CloseRejectedAsync(c.Id, rentalFee: 20_000, cleaningFee: 5_000,
            waiverAmount: 15_000, waiverReason: "Kórházba került", bank: null);

        Assert.Equal(90_000, s.Refund); // 100e − 10e
        Assert.Equal(ContractStatus.Lezarva, (await db.Context.Contracts.SingleAsync()).Status);
        // Karantén-esemény a Timeline-on.
        Assert.Contains(await db.Context.TimelineEvents.ToListAsync(), t => t.EventType == "EszkozKarantenba");
        // Méltányossági audit.
        Assert.Contains(await db.Context.AuditLog.ToListAsync(), a => a.Action == "MeltanyossagiFelulbiralas");
    }

    [Fact]
    public async Task Mar_lezart_szerzodes_ujra_nem_zarhato()
    {
        var (svc, _, clock, db) = Build();
        using var _db = db;
        var c = await AddContractAsync(db, clock);
        await svc.CloseSuccessfulAsync(c.Id, 10_000, 0, null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CloseSuccessfulAsync(c.Id, 10_000, 0, null));
    }
}
