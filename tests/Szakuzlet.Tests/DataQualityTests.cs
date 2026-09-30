using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Common;
using Szakuzlet.Application.DataQuality;
using Szakuzlet.Domain.Entities;

namespace Szakuzlet.Tests;

public class DataQualityTests
{
    private static (DataQualityService svc, FakeClock clock, FakeNotificationSender notif, TestDb db) Build()
    {
        var db = new TestDb();
        var clock = new FakeClock();
        var events = new EventRecorder(db.Context, clock);
        var notif = new FakeNotificationSender();
        var tokens = new FakeTokenGenerator();
        return (new DataQualityService(db.Context, clock, events, notif, tokens), clock, notif, db);
    }

    private static async Task<Patient> AddPatientAsync(TestDb db, FakeClock clock,
        string? email, string? mobile, string? taj)
    {
        var p = new Patient
        {
            Id = Guid.NewGuid(), FullName = "Hiány Hilda",
            Email = email, MobilePhone = mobile, TajNumber = taj,
            CreatedAtUtc = clock.UtcNow
        };
        db.Context.Patients.Add(p);
        await db.Context.SaveChangesAsync();
        return p;
    }

    [Fact]
    public async Task Check_jelzi_a_hianyzo_mezoket()
    {
        var (svc, clock, _, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock, email: "h@x.hu", mobile: null, taj: null);

        var alert = await svc.CheckAsync(p.Id);

        Assert.True(alert.HasMissing);
        Assert.Contains("Mobiltelefonszám", alert.MissingFields);
        Assert.Contains("TAJ szám", alert.MissingFields);
        Assert.DoesNotContain("E-mail cím", alert.MissingFields);
    }

    [Fact]
    public async Task Check_teljes_adatlapnal_nincs_hiany()
    {
        var (svc, clock, _, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock, "h@x.hu", "+36301234567", "123456789");

        var alert = await svc.CheckAsync(p.Id);
        Assert.False(alert.HasMissing);
    }

    [Fact]
    public async Task A_opcio_frissit_es_GDPR_igazolast_kuld()
    {
        var (svc, clock, notif, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock, "h@x.hu", mobile: null, taj: null);

        await svc.UpdateInPersonAsync(p.Id, new ContactUpdate(null, "+36309998888", "123456789"));

        var refreshed = await db.Context.Patients.SingleAsync();
        Assert.Equal("+36309998888", refreshed.MobilePhone);
        Assert.Equal("123456789", refreshed.TajNumber);
        Assert.False(refreshed.HasMissingRequiredContact);

        // GDPR igazolás + marketing-terelés kiment.
        var mail = Assert.Single(notif.Sent);
        Assert.Contains("Adatfrissítési igazolás", mail.Subject);
        Assert.Contains("megújult", mail.Body); // marketing-terelés
        // Timeline + audit.
        Assert.Contains(await db.Context.TimelineEvents.ToListAsync(), t => t.EventType == "AdatlapFrissitve");
    }

    [Fact]
    public async Task B_opcio_linket_general_es_kikuld()
    {
        var (svc, clock, notif, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock, "h@x.hu", "+36301234567", taj: null);

        var req = await svc.SendSelfServiceLinkAsync(p.Id);

        Assert.False(req.Completed);
        Assert.Contains("TAJ szám", req.MissingFields);
        // Hibrid kiküldés: van e-mail ÉS mobil -> 2 üzenet.
        Assert.Equal(2, notif.Sent.Count);
    }

    [Fact]
    public async Task B_opcio_beteg_bekuldi_az_adatokat_a_linken()
    {
        var (svc, clock, _, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock, "h@x.hu", "+36301234567", taj: null);
        var req = await svc.SendSelfServiceLinkAsync(p.Id);

        var loaded = await svc.GetByTokenAsync(req.Token);
        Assert.NotNull(loaded);

        await svc.CompleteSelfServiceAsync(req.Token, new ContactUpdate(null, null, "987654321"));

        var refreshed = await db.Context.Patients.SingleAsync();
        Assert.Equal("987654321", refreshed.TajNumber);
        var closed = await db.Context.DataCompletionRequests.SingleAsync();
        Assert.True(closed.Completed);

        // A felhasznált link már nem tölthető be.
        Assert.Null(await svc.GetByTokenAsync(req.Token));
    }

    [Fact]
    public async Task Lejart_link_nem_toltheto_be()
    {
        var (svc, clock, _, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock, "h@x.hu", "+36301234567", taj: null);
        var req = await svc.SendSelfServiceLinkAsync(p.Id);

        clock.UtcNow = req.ExpiresAtUtc.AddDays(1);

        Assert.Null(await svc.GetByTokenAsync(req.Token));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CompleteSelfServiceAsync(req.Token, new ContactUpdate(null, null, "1")));
    }
}
