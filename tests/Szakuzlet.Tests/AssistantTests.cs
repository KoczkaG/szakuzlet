using Szakuzlet.Application.Assistant;
using Szakuzlet.Application.Banking;
using Szakuzlet.Application.Billing;
using Szakuzlet.Application.Common;
using Szakuzlet.Application.Contracts;
using Szakuzlet.Domain.Entities;
using Szakuzlet.Domain.Enums;
using Szakuzlet.Infrastructure.Banking;

namespace Szakuzlet.Tests;

public class AssistantTests
{
    private static async Task<Patient> AddPatientAsync(TestDb db, FakeClock clock)
    {
        var p = new Patient { Id = Guid.NewGuid(), FullName = "Asszisztens Aladár", CreatedAtUtc = clock.UtcNow };
        db.Context.Patients.Add(p);
        await db.Context.SaveChangesAsync();
        return p;
    }

    [Fact]
    public async Task Workflow_attekintes_szamolja_a_folyamatban_levo_ugyeket()
    {
        using var db = new TestDb();
        var clock = new FakeClock();
        var p = await AddPatientAsync(db, clock);

        db.Context.ExpressIntakes.Add(new ExpressIntake
        {
            Id = Guid.NewGuid(), PatientId = p.Id, Status = ExpressIntakeStatus.ExpresszKiszolgalasraVar,
            CreatedAtUtc = clock.UtcNow
        });
        db.Context.PostalTrials.Add(new PostalTrial
        {
            Id = Guid.NewGuid(), PatientId = p.Id, OrderNumber = "P-1",
            Status = PostalTrialStatus.AlairasraVar, CreatedAtUtc = clock.UtcNow
        });
        db.Context.Contracts.Add(new Contract
        {
            Id = Guid.NewGuid(), PatientId = p.Id, Number = "SZ-X-1", DeviceModel = "D", DeviceSerialNumber = "SN",
            Status = ContractStatus.Elokeszitve, CreatedAtUtc = clock.UtcNow
        });
        await db.Context.SaveChangesAsync();

        var svc = new AssistantService(db.Context);
        var wf = await svc.GetWorkflowAsync();

        Assert.Equal(1, wf.ExpressWaiting);
        Assert.Equal(1, wf.PostalAwaitingSignature);
        Assert.Equal(1, wf.ContractsAwaitingSignature);
        Assert.Equal(0, wf.PostalAwaitingPayment);
    }

    [Fact]
    public async Task Vezetoi_riport_szamolja_a_lezarasi_kimeneteleket_es_aranyt()
    {
        using var db = new TestDb();
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero) };
        var events = new EventRecorder(db.Context, clock);
        var settlement = new SettlementService(db.Context, clock);
        var payout = new MockPayoutExporter(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MockPayoutExporter>.Instance);
        var closure = new TrialClosureService(db.Context, clock, events, settlement, payout);

        var p = await AddPatientAsync(db, clock);

        // 3 szerződés: 2 sikeres lezárás, 1 elutasítás.
        for (var i = 0; i < 3; i++)
        {
            var c = new Contract
            {
                Id = Guid.NewGuid(), PatientId = p.Id, Number = $"SZ-R-{i}",
                DeviceModel = "AirSense 11", DeviceSerialNumber = $"SN-{i}", DepositAmount = 100_000m,
                Status = ContractStatus.ProbaFut, CreatedAtUtc = clock.UtcNow
            };
            db.Context.Contracts.Add(c);
            await db.Context.SaveChangesAsync();

            if (i < 2)
                await closure.CloseSuccessfulAsync(c.Id, 30_000, 0, null);
            else
                await closure.CloseRejectedAsync(c.Id, 10_000, 0, 0, null, null);
        }

        var svc = new AssistantService(db.Context);
        var report = await svc.GetTrialReportAsync(
            clock.UtcNow.AddDays(-1), clock.UtcNow.AddDays(1));

        Assert.Equal(3, report.Started);
        Assert.Equal(3, report.Closed);      // 2 sikeres + 1 elutasítás
        Assert.Equal(1, report.Rejected);
        Assert.Equal(33.3, report.RejectionRate);
    }
}
