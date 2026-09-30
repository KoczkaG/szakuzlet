using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Common;
using Szakuzlet.Application.Express;
using Szakuzlet.Domain.Entities;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Tests;

public class ExpressIntakeTests
{
    private static (ExpressIntakeService svc, FakeNotificationSender notif, FakeClock clock, TestDb db) Build()
    {
        var db = new TestDb();
        var clock = new FakeClock();
        var events = new EventRecorder(db.Context, clock);
        var notif = new FakeNotificationSender();
        return (new ExpressIntakeService(db.Context, clock, events, notif), notif, clock, db);
    }

    private static async Task<Patient> AddPatientAsync(TestDb db, FakeClock clock, string? email = "b@x.hu")
    {
        var p = new Patient
        {
            Id = Guid.NewGuid(), FullName = "Expressz Emil", Email = email,
            MobilePhone = "+36301112222", CreatedAtUtc = clock.UtcNow
        };
        db.Context.Patients.Add(p);
        await db.Context.SaveChangesAsync();
        return p;
    }

    private static ExpressIntakeInput Input(bool priv, bool tb, RequesterRole role = RequesterRole.Beteg,
        string? rEmail = null) => new(
        role, role == RequesterRole.Hozzatartozo ? "Rokon Róbert" : null,
        role == RequesterRole.Hozzatartozo ? "+36309998888" : null, rEmail,
        "AirSense 11", "Orrmaszk M", 9.0m, priv, tb, 50_000m);

    [Fact]
    public async Task Eloszuro_NEAK_tamogatott_ha_van_TB_es_nem_magan()
    {
        var (svc, _, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock);

        var intake = await svc.SubmitAsync(p.Id, Input(priv: false, tb: true));

        Assert.Equal(IntakeClassification.NeakTamogatottProbakezeles, intake.Classification);
    }

    [Theory]
    [InlineData(true, true)]   // magán -> teljes ár akkor is, ha van TB
    [InlineData(false, false)] // nincs TB -> teljes ár
    public async Task Eloszuro_teljes_ar_ha_magan_vagy_nincs_TB(bool priv, bool tb)
    {
        var (svc, _, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock);

        var intake = await svc.SubmitAsync(p.Id, Input(priv, tb));

        Assert.Equal(IntakeClassification.MaganellatasTeljesAr, intake.Classification);
    }

    [Fact]
    public async Task Osszekeszites_ertesitest_kuld_a_fizetendo_osszeggel()
    {
        var (svc, notif, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock);
        var intake = await svc.SubmitAsync(p.Id, Input(false, true));

        await svc.MarkReadyAsync(intake.Id);

        var refreshed = await db.Context.ExpressIntakes.SingleAsync();
        Assert.Equal(ExpressIntakeStatus.ExpresszKiszolgalasraVar, refreshed.Status);
        Assert.Contains(notif.Sent, m => m.Subject.Contains("átvehető") && m.Body.Contains("50"));
    }

    [Fact]
    public async Task Hozzatartozo_eseten_az_o_elerhetosegere_megy_az_ertesites()
    {
        var (svc, notif, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock, email: "beteg@x.hu");
        var intake = await svc.SubmitAsync(p.Id,
            Input(false, true, RequesterRole.Hozzatartozo, rEmail: "rokon@x.hu"));

        await svc.MarkReadyAsync(intake.Id);

        Assert.Contains(notif.Sent, m => m.Recipient == "rokon@x.hu");
        Assert.DoesNotContain(notif.Sent, m => m.Recipient == "beteg@x.hu");
    }

    [Fact]
    public async Task Keszlethiany_varakoztato_ertesitest_kuld()
    {
        var (svc, notif, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock);
        var intake = await svc.SubmitAsync(p.Id, Input(false, true));

        await svc.MarkOutOfStockAsync(intake.Id);

        Assert.Equal(ExpressIntakeStatus.BeszerzesreVar,
            (await db.Context.ExpressIntakes.SingleAsync()).Status);
        Assert.Contains(notif.Sent, m => m.Body.Contains("NE induljon el"));
    }

    [Fact]
    public async Task Varialas_eseten_az_expressz_megszakad_es_normal_sorba_kerul()
    {
        var (svc, _, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock);
        var intake = await svc.SubmitAsync(p.Id, Input(false, true));
        await svc.MarkReadyAsync(intake.Id);

        await svc.BreakToNormalQueueAsync(intake.Id, "Helyszíni maszkpróbát kért");

        var refreshed = await db.Context.ExpressIntakes.SingleAsync();
        Assert.Equal(ExpressIntakeStatus.NormalSorbaKerult, refreshed.Status);
        Assert.Contains(await db.Context.TimelineEvents.ToListAsync(), t => t.EventType == "ExpresszMegszakadt");
    }
}
