using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.CallCenter;
using Szakuzlet.Application.Common;
using Szakuzlet.Domain.Entities;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Tests;

public class OutboundCallTests
{
    private static (OutboundCallService svc, FakeClock clock, TestDb db) Build()
    {
        var db = new TestDb();
        var clock = new FakeClock();
        var events = new EventRecorder(db.Context, clock);
        return (new OutboundCallService(db.Context, clock, events), clock, db);
    }

    private static async Task<Patient> AddPatientAsync(TestDb db, FakeClock clock, string? mobile)
    {
        var p = new Patient
        {
            Id = Guid.NewGuid(), FullName = "Beteg Béla", MobilePhone = mobile,
            CreatedAtUtc = clock.UtcNow
        };
        db.Context.Patients.Add(p);
        await db.Context.SaveChangesAsync();
        return p;
    }

    [Fact]
    public async Task Torzsmobil_es_kapcsolattarto_is_hivhato_szamkent_jelenik_meg()
    {
        var (svc, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock, "+36301234567");
        await svc.AddPhoneAsync(p.Id, PhoneKind.Kapcsolattarto, "+36209998888",
            "Beteg Béláné", legallyApproved: true);

        var numbers = await svc.GetDialableNumbersAsync(p.Id);

        Assert.Equal(2, numbers.Count);
        Assert.Contains(numbers, n => n.Kind == PhoneKind.BetegMobil && n.LegallyApproved);
        Assert.Contains(numbers, n => n.Kind == PhoneKind.Kapcsolattarto && n.ContactName == "Beteg Béláné");
    }

    [Fact]
    public async Task Nem_jovahagyott_kapcsolattarto_jelolese_megmarad()
    {
        var (svc, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock, null);
        await svc.AddPhoneAsync(p.Id, PhoneKind.Hozzatartozo, "+36301112222",
            "Ismerős", legallyApproved: false);

        var numbers = await svc.GetDialableNumbersAsync(p.Id);
        Assert.Single(numbers);
        Assert.False(numbers[0].LegallyApproved);
    }

    [Fact]
    public async Task Hivas_inditasa_adatlap_kontextust_es_GDPR_sablont_ad()
    {
        var (svc, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock, "+36301234567");

        var started = await svc.StartCallAsync(p.Id, "+36301234567", PhoneKind.BetegMobil);

        Assert.Equal(p.Id, started.PatientId);
        Assert.Equal("Beteg Béla", started.PatientName);
        Assert.Contains("minőségbiztosítási", started.GdprPrompt);
        Assert.Contains("óvatosságát", started.SuspicionScript);

        // Timeline + audit keletkezett.
        Assert.Contains(await db.Context.TimelineEvents.ToListAsync(), t => t.EventType == "KimenoHivas");
        Assert.Contains(await db.Context.AuditLog.ToListAsync(), a => a.Action == "KimenoHivasInditva");
    }

    [Fact]
    public async Task Rogzites_leallitasa_torli_a_savot_es_audit_trailt_general()
    {
        var (svc, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock, "+36301234567");
        var started = await svc.StartCallAsync(p.Id, "+36301234567", PhoneKind.BetegMobil);

        await svc.StopRecordingAsync(started.CallId, "Nagy Béla");

        var call = await db.Context.Calls.SingleAsync();
        Assert.Equal(RecordingState.LeallitvaEsTorolve, call.Recording);
        Assert.Null(call.RecordingReference);

        var audit = await db.Context.AuditLog
            .SingleAsync(a => a.Action == "Hangrogzites_LeallitvaEsTorolve");
        Assert.Equal("Nagy Béla", audit.Actor);
        Assert.Contains("kimenő hívásnál", audit.Details);
    }

    [Fact]
    public async Task Lezaraskor_a_hangfajl_hivatkozas_rogzul_ha_rogzitettunk()
    {
        var (svc, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock, "+36301234567");
        var started = await svc.StartCallAsync(p.Id, "+36301234567", PhoneKind.BetegMobil);

        await svc.EndCallAsync(started.CallId, answered: true, recordingReference: "rec-123");

        var call = await db.Context.Calls.SingleAsync();
        Assert.True(call.Answered);
        Assert.Equal("rec-123", call.RecordingReference);
        Assert.NotNull(call.EndedAtUtc);
    }

    [Fact]
    public async Task Leallitott_rogzites_utan_a_lezaras_nem_ir_vissza_hangfajlt()
    {
        var (svc, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock, "+36301234567");
        var started = await svc.StartCallAsync(p.Id, "+36301234567", PhoneKind.BetegMobil);
        await svc.StopRecordingAsync(started.CallId, "PULTOS");

        await svc.EndCallAsync(started.CallId, answered: true, recordingReference: "rec-should-be-ignored");

        var call = await db.Context.Calls.SingleAsync();
        Assert.Equal(RecordingState.LeallitvaEsTorolve, call.Recording);
        Assert.Null(call.RecordingReference); // nem írjuk vissza, mert a rögzítés le lett tiltva
    }
}
