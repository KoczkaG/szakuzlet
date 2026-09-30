using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Calendar;
using Szakuzlet.Domain.Entities;
using Szakuzlet.Infrastructure.Persistence;

namespace Szakuzlet.Tests;

public class OpeningHoursTests
{
    private static async Task<(OpeningHoursService svc, FakeClock clock, FakeOpeningHoursSync sync, TestDb db)>
        BuildAsync()
    {
        var db = new TestDb();
        await DataSeeder.SeedAsync(db.Context);
        var clock = new FakeClock();
        var sync = new FakeOpeningHoursSync();
        return (new OpeningHoursService(db.Context, clock, sync), clock, sync, db);
    }

    [Fact]
    public async Task Nyitva_hetkoznap_delben()
    {
        var (svc, clock, _, db) = await BuildAsync();
        using var _db = db;
        // 2026-06-01 hétfő, 10:00 helyi (Budapest nyáron UTC+2) => 08:00 UTC.
        clock.UtcNow = new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);

        var status = await svc.GetStatusNowAsync();

        Assert.True(status.IsOpen);
    }

    [Fact]
    public async Task Zarva_hetkoznap_zaras_utan()
    {
        var (svc, clock, _, db) = await BuildAsync();
        using var _db = db;
        // Hétfő 17:30 helyi (zárás 17:00 után) => 15:30 UTC nyáron.
        clock.UtcNow = new DateTimeOffset(2026, 6, 1, 15, 30, 0, TimeSpan.Zero);

        var status = await svc.GetStatusNowAsync();

        Assert.False(status.IsOpen);
        Assert.NotNull(status.NextOpenAtLocal); // következő nyitás (kedd 8:00)
    }

    [Fact]
    public async Task Zarva_hetvegen()
    {
        var (svc, clock, _, db) = await BuildAsync();
        using var _db = db;
        // 2026-06-06 szombat, 10:00 helyi => 08:00 UTC.
        clock.UtcNow = new DateTimeOffset(2026, 6, 6, 8, 0, 0, TimeSpan.Zero);

        var status = await svc.GetStatusNowAsync();

        Assert.False(status.IsOpen);
    }

    [Fact]
    public async Task Unnepnapi_zarvatartas_felulirja_a_nyitvatartast()
    {
        var (svc, clock, sync, db) = await BuildAsync();
        using var _db = db;

        // Felviszünk egy zárt ünnepnapot egy hétköznapra (2026-06-08 hétfő).
        await svc.UpsertOverrideAsync(new DateOnly(2026, 6, 8), "Pünkösdhétfő",
            isClosed: true, opensAt: null, closesAt: null);

        // A módosítás kifelé szinkronizált.
        Assert.True(sync.SyncCount >= 1);

        // Az adott hétköznap délben mégis zárva.
        clock.UtcNow = new DateTimeOffset(2026, 6, 8, 8, 0, 0, TimeSpan.Zero);
        var status = await svc.GetStatusNowAsync();

        Assert.False(status.IsOpen);
        Assert.Equal("Pünkösdhétfő", status.OverrideLabel);
    }

    [Fact]
    public async Task Ledolgozos_szombat_felulirja_a_zart_napot()
    {
        var (svc, clock, _, db) = await BuildAsync();
        using var _db = db;

        // Ledolgozós szombat: 2026-06-13, 8-14 nyitva.
        await svc.UpsertOverrideAsync(new DateOnly(2026, 6, 13), "Ledolgozós szombat",
            isClosed: false, opensAt: new TimeOnly(8, 0), closesAt: new TimeOnly(14, 0));

        // Szombat 10:00 helyi => 08:00 UTC.
        clock.UtcNow = new DateTimeOffset(2026, 6, 13, 8, 0, 0, TimeSpan.Zero);
        var status = await svc.GetStatusNowAsync();

        Assert.True(status.IsOpen);
        Assert.Equal("Ledolgozós szombat", status.OverrideLabel);
    }
}
