using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Common;
using Szakuzlet.Application.Telemedicine;
using Szakuzlet.Domain.Entities;

namespace Szakuzlet.Tests;

public class TelemedicineTests
{
    private static (TelemedicineService svc, FakeClock clock, TestDb db) Build()
    {
        var db = new TestDb();
        var clock = new FakeClock();
        var events = new EventRecorder(db.Context, clock);
        return (new TelemedicineService(db.Context, clock, events), clock, db);
    }

    private static async Task<Patient> AddPatientAsync(TestDb db, FakeClock clock)
    {
        var p = new Patient { Id = Guid.NewGuid(), FullName = "Tele Tibor", CreatedAtUtc = clock.UtcNow };
        db.Context.Patients.Add(p);
        await db.Context.SaveChangesAsync();
        return p;
    }

    private static async Task SeedVideosAsync(TestDb db)
    {
        db.Context.EducationVideos.AddRange(
            new EducationVideo { Id = Guid.NewGuid(), ProductModel = "AirSense 11", Title = "Setup", Url = "u1", IsGeneric = false, IsActive = true },
            new EducationVideo { Id = Guid.NewGuid(), ProductModel = "Orrmaszk M", Title = "Fit", Url = "u2", IsGeneric = false, IsActive = true },
            new EducationVideo { Id = Guid.NewGuid(), ProductModel = "DreamStation 2", Title = "Other", Url = "u3", IsGeneric = false, IsActive = true },
            new EducationVideo { Id = Guid.NewGuid(), ProductModel = "*", Title = "Szűrő", Url = "u4", IsGeneric = true, IsActive = true });
        await db.Context.SaveChangesAsync();
    }

    [Fact]
    public async Task Termekre_szabott_es_altalanos_videokat_valogat_ossze()
    {
        var (svc, _, db) = Build();
        using var _db = db;
        await SeedVideosAsync(db);

        var videos = await svc.GetVideosForProductsAsync(new[] { "AirSense 11", "Orrmaszk M" });

        // AirSense + Orrmaszk + általános szűrő = 3; a DreamStation 2 NEM.
        Assert.Equal(3, videos.Count);
        Assert.Contains(videos, v => v.Title == "Setup");
        Assert.Contains(videos, v => v.Title == "Fit");
        Assert.Contains(videos, v => v.IsGeneric);
        Assert.DoesNotContain(videos, v => v.Title == "Other");
    }

    [Fact]
    public async Task Videok_leokezese_idobelyeges_igazolast_rogzit()
    {
        var (svc, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock);

        var ack = await svc.AcknowledgeVideosAsync(p.Id, null, new[] { "AirSense 11", "Orrmaszk M" }, "5.6.7.8");

        Assert.True(ack.VideosAcknowledged);
        Assert.False(ack.RoutineUserWaiver);
        Assert.Equal(clock.UtcNow, ack.AcknowledgedAtUtc);
        Assert.Equal("5.6.7.8", ack.FromIp);
        Assert.Contains(await db.Context.TimelineEvents.ToListAsync(), t => t.EventType == "OktatasIgazolva");
        Assert.Contains(await db.Context.AuditLog.ToListAsync(), a => a.Action == "OktatovideoLeokezve");
    }

    [Fact]
    public async Task Rutinos_beteg_lemondhat_a_betanitasrol()
    {
        var (svc, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock);

        var ack = await svc.WaiveAsRoutineUserAsync(p.Id, null, "1.1.1.1");

        Assert.True(ack.RoutineUserWaiver);
        Assert.False(ack.VideosAcknowledged);
        Assert.Contains(await db.Context.AuditLog.ToListAsync(), a => a.Action == "BetanitasLemondva_Rutinos");
    }
}
