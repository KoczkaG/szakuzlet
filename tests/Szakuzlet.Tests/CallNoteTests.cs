using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.CallCenter;
using Szakuzlet.Application.Common;
using Szakuzlet.Domain.Entities;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Tests;

public class CallNoteTests
{
    private static async Task<(CallNoteService notes, CallStatisticsService stats, FakeClock clock, TestDb db)>
        BuildAsync()
    {
        var db = new TestDb();
        var clock = new FakeClock();
        var events = new EventRecorder(db.Context, clock);
        return (new CallNoteService(db.Context, clock, events),
                new CallStatisticsService(db.Context), clock, db);
    }

    private static async Task<(Patient p, CallRecord call)> AddCallAsync(
        TestDb db, FakeClock clock, bool withPatient = true)
    {
        Patient? p = null;
        if (withPatient)
        {
            p = new Patient { Id = Guid.NewGuid(), FullName = "Jegyzet József", CreatedAtUtc = clock.UtcNow };
            db.Context.Patients.Add(p);
        }
        var call = new CallRecord
        {
            Id = Guid.NewGuid(), Direction = CallDirection.Bejovo, PhoneNumber = "+36301234567",
            PatientId = p?.Id, StartedAtUtc = clock.UtcNow, Answered = true
        };
        db.Context.Calls.Add(call);
        await db.Context.SaveChangesAsync();
        return (p!, call);
    }

    [Fact]
    public async Task Jegyzet_mentese_timeline_be_ir_es_feladatot_general_ha_kell()
    {
        var (notes, _, clock, db) = await BuildAsync();
        using var _db = db;
        var (p, call) = await AddCallAsync(db, clock);

        await notes.SaveAsync(call.Id, new CallNoteInput(
            CallTopic.Venybevaltas | CallTopic.MaszkbeallitasTerapiasKerdes,
            ReferralSourceId: null,
            Summary: "Vény beváltva, maszkméret egyeztetve.",
            FollowUpRequired: true));

        var note = await db.Context.CallNotes.SingleAsync();
        Assert.True(note.Topics.HasFlag(CallTopic.Venybevaltas));
        Assert.True(note.Topics.HasFlag(CallTopic.MaszkbeallitasTerapiasKerdes));

        // Timeline-bejegyzés.
        Assert.Contains(await db.Context.TimelineEvents.ToListAsync(),
            t => t.EventType == "HivasvegiJegyzet");

        // Automata feladat (follow-up).
        var task = await db.Context.Tasks.SingleAsync();
        Assert.Equal(PatientTaskType.HivasvegiVisszahivas, task.Type);
        Assert.Equal(PatientTaskStatus.Nyitott, task.Status);
        Assert.Equal(p.Id, task.PatientId);
    }

    [Fact]
    public async Task Follow_up_nelkul_nincs_feladat()
    {
        var (notes, _, clock, db) = await BuildAsync();
        using var _db = db;
        var (_, call) = await AddCallAsync(db, clock);

        await notes.SaveAsync(call.Id, new CallNoteInput(
            CallTopic.RendelesLeadas, null, "Rendelés rögzítve.", FollowUpRequired: false));

        Assert.Empty(await db.Context.Tasks.ToListAsync());
    }

    [Fact]
    public async Task Ures_osszefoglalo_nem_mentheto()
    {
        var (notes, _, clock, db) = await BuildAsync();
        using var _db = db;
        var (_, call) = await AddCallAsync(db, clock);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => notes.SaveAsync(call.Id, new CallNoteInput(CallTopic.None, null, "   ", false)));
    }

    [Fact]
    public async Task Ketszeri_jegyzet_ugyanahhoz_a_hivashoz_tiltott()
    {
        var (notes, _, clock, db) = await BuildAsync();
        using var _db = db;
        var (_, call) = await AddCallAsync(db, clock);

        await notes.SaveAsync(call.Id, new CallNoteInput(CallTopic.RendelesLeadas, null, "Első.", false));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => notes.SaveAsync(call.Id, new CallNoteInput(CallTopic.RendelesLeadas, null, "Második.", false)));
    }

    [Fact]
    public async Task Ismeretlen_hivonal_follow_up_eseten_sincs_beteghez_kotott_feladat()
    {
        var (notes, _, clock, db) = await BuildAsync();
        using var _db = db;
        var (_, call) = await AddCallAsync(db, clock, withPatient: false);

        await notes.SaveAsync(call.Id, new CallNoteInput(
            CallTopic.UjUgyfelInfo, null, "Új érdeklődő.", FollowUpRequired: true));

        // Jegyzet létrejött, de beteghez kötött feladat nem (nincs beteg).
        Assert.Single(await db.Context.CallNotes.ToListAsync());
        Assert.Empty(await db.Context.Tasks.ToListAsync());
    }

    [Fact]
    public async Task Statisztika_temakor_megoszlast_referralt_es_panaszt_szamol()
    {
        var (notes, stats, clock, db) = await BuildAsync();
        using var _db = db;

        var referral = new ReferralSource { Id = Guid.NewGuid(), Name = "Városi Alváslabor", IsActive = true };
        db.Context.ReferralSources.Add(referral);
        await db.Context.SaveChangesAsync();

        // 3 jegyzet: 2 vénybeváltás (egyik panasz is), 1 rendelés + referral.
        var (_, c1) = await AddCallAsync(db, clock);
        var (_, c2) = await AddCallAsync(db, clock);
        var (_, c3) = await AddCallAsync(db, clock);
        await notes.SaveAsync(c1.Id, new CallNoteInput(CallTopic.Venybevaltas, referral.Id, "a", false));
        await notes.SaveAsync(c2.Id, new CallNoteInput(
            CallTopic.Venybevaltas | CallTopic.Panaszkezeles, null, "b", false));
        await notes.SaveAsync(c3.Id, new CallNoteInput(CallTopic.RendelesLeadas, referral.Id, "c", false));

        var from = clock.UtcNow.AddDays(-1);
        var to = clock.UtcNow.AddDays(1);
        var result = await stats.GetAsync(from, to);

        Assert.Equal(3, result.TotalNotes);
        var veny = result.TopicBreakdown.Single(t => t.Topic == CallTopic.Venybevaltas);
        Assert.Equal(2, veny.Count);
        Assert.Equal(66.7, veny.Percentage);
        Assert.Equal(1, result.ComplaintCount);
        // Referral rangsor: a labor 2 beteggel az élen.
        Assert.Equal(referral.Id, result.ReferralRanking[0].ReferralSourceId);
        Assert.Equal(2, result.ReferralRanking[0].Count);
    }
}
