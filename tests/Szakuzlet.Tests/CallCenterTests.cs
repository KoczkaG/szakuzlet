using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.CallCenter;
using Szakuzlet.Application.Calendar;
using Szakuzlet.Application.Common;
using Szakuzlet.Domain.Entities;
using Szakuzlet.Domain.Enums;
using Szakuzlet.Infrastructure.Kvl;
using Szakuzlet.Infrastructure.Persistence;

namespace Szakuzlet.Tests;

public class CallCenterTests
{
    private static async Task<(CallCenterService svc, FakeClock clock, TestDb db)> BuildAsync()
    {
        var db = new TestDb();
        await DataSeeder.SeedAsync(db.Context);
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero) };
        var events = new EventRecorder(db.Context, clock);
        var hours = new OpeningHoursService(db.Context, clock, new FakeOpeningHoursSync());
        var svc = new CallCenterService(db.Context, clock, events, hours, new MockKvlClient());
        return (svc, clock, db);
    }

    private static async Task<Patient> AddPatientAsync(TestDb db, FakeClock clock, string phone)
    {
        var p = new Patient
        {
            Id = Guid.NewGuid(), FullName = "Hívó Henrik", MobilePhone = phone,
            IsLegacy = true, CreatedAtUtc = clock.UtcNow
        };
        db.Context.Patients.Add(p);
        await db.Context.SaveChangesAsync();
        return p;
    }

    [Fact]
    public async Task Bejovo_hivas_CRM_talalattal_es_nyitvatartason_belul()
    {
        var (svc, clock, db) = await BuildAsync();
        using var _db = db;
        var p = await AddPatientAsync(db, clock, "+36301234567");

        var result = await svc.HandleIncomingAsync("+36301234567", "3. Szerviz");

        Assert.True(result.WithinOpeningHours);
        Assert.True(result.CrmMatch);
        Assert.Equal(p.Id, result.PatientId);
        Assert.True(result.IsLegacyPatient);
        // Timeline-esemény keletkezett a beteg adatlapján.
        Assert.Contains(await db.Context.TimelineEvents.ToListAsync(),
            t => t.EventType == "BejovoHivas");
    }

    [Fact]
    public async Task Bejovo_hivas_normalizalt_telefonszammal_is_talal()
    {
        var (svc, clock, db) = await BuildAsync();
        using var _db = db;
        await AddPatientAsync(db, clock, "+36301234567");

        // Belföldi 06-os formátum ugyanarra a számra.
        var result = await svc.HandleIncomingAsync("06 30 123 4567");

        Assert.True(result.CrmMatch);
    }

    [Fact]
    public async Task Ismeretlen_szam_nincs_CRM_talalat()
    {
        var (svc, _, db) = await BuildAsync();
        using var _db = db;

        var result = await svc.HandleIncomingAsync("+36209999999");

        Assert.False(result.CrmMatch);
        Assert.Null(result.PatientId);
    }

    [Fact]
    public async Task Hangrogzites_leallitasa_modosithatatlan_audit_trailt_general()
    {
        var (svc, _, db) = await BuildAsync();
        using var _db = db;
        var result = await svc.HandleIncomingAsync("+36201112222");

        await svc.ApplyRecordingDecisionAsync(result.CallId,
            new RecordingDecision(RecordingState.LeallitvaEsTorolve, "Kiss Anna",
                "Ügyfél tiltása miatt felvétel megszakítva és törölve"));

        var call = await db.Context.Calls.SingleAsync();
        Assert.Equal(RecordingState.LeallitvaEsTorolve, call.Recording);
        Assert.Null(call.RecordingReference);

        var audit = await db.Context.AuditLog
            .Where(a => a.EntityType == "CallRecord" && a.Action.StartsWith("Hangrogzites_"))
            .SingleAsync();
        Assert.Equal("Kiss Anna", audit.Actor);
        Assert.Contains("megszakítva", audit.Details);
    }

    [Fact]
    public async Task Nem_fogadott_hivas_visszahivasi_listara_kerul_majd_onurul()
    {
        var (svc, clock, db) = await BuildAsync();
        using var _db = db;
        var r1 = await svc.HandleIncomingAsync("+36301234567");

        // A hívás nem fogadott -> lezárás -> visszahívási igény.
        await svc.EndCallAsync(r1.CallId, answered: false, recordingReference: null);
        Assert.Single(await db.Context.Callbacks.Where(c => c.Status == CallbackStatus.Nyitott).ToListAsync());

        // Később ugyanaz a szám hív, és most fogadjuk -> az önürítő lezárja a nyitott igényt.
        var r2 = await svc.HandleIncomingAsync("+36301234567");
        await svc.MarkAnsweredAsync(r2.CallId);

        Assert.Empty(await db.Context.Callbacks.Where(c => c.Status == CallbackStatus.Nyitott).ToListAsync());
    }

    [Fact]
    public async Task Munkaidon_kivuli_visszahivas_a_kovetkezo_nyitasra_idozit()
    {
        var (svc, clock, db) = await BuildAsync();
        using var _db = db;
        // Vasárnap – zárva.
        clock.UtcNow = new DateTimeOffset(2026, 6, 7, 12, 0, 0, TimeSpan.Zero);

        var cb = await svc.RequestAfterHoursCallbackAsync("+36305556666");

        Assert.Equal(CallbackKind.MunkaidonKivul, cb.Kind);
        Assert.True(cb.DueAtUtc > clock.UtcNow); // a következő nyitásra időzítve
    }
}
