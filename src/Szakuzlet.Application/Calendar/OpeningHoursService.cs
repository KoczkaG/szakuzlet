using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Domain.Entities;

namespace Szakuzlet.Application.Calendar;

/// <summary>
/// A központi KVL Nyitvatartás és Ünnepnapi Naptár modul (II. RÉSZ 1. pont).
///
/// Ez a cég egyetlen igazságforrása a nyitvatartásról. Meghatározza, hogy egy adott
/// (helyi idejű) időpontban nyitva van-e a szaküzlet, figyelembe véve az alapértelmezett
/// heti nyitvatartást és az egyedi naptári felülírásokat (ünnep, ledolgozós nap, rövidített).
/// A módosítások kifelé (webshop, Google) szinkronizálódnak.
/// </summary>
public sealed class OpeningHoursService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly IOpeningHoursSync _sync;

    // A szaküzlet helyi időzónája. IANA azonosító (Linux/.NET is kezeli).
    private static readonly TimeZoneInfo LocalTz =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Budapest");

    public OpeningHoursService(IAppDbContext db, IClock clock, IOpeningHoursSync sync)
    {
        _db = db;
        _clock = clock;
        _sync = sync;
    }

    /// <summary>A telefonközpont időzítő-zsilipje ezt hívja: nyitva van-e MOST?</summary>
    public Task<OpeningStatus> GetStatusNowAsync(CancellationToken ct = default)
        => GetStatusAtAsync(_clock.UtcNow, ct);

    /// <summary>Nyitvatartási állapot egy adott UTC időpontra (helyi időre konvertálva vizsgálva).</summary>
    public async Task<OpeningStatus> GetStatusAtAsync(DateTimeOffset utcInstant, CancellationToken ct = default)
    {
        var local = TimeZoneInfo.ConvertTime(utcInstant, LocalTz);
        var localDate = DateOnly.FromDateTime(local.DateTime);
        var localTime = TimeOnly.FromDateTime(local.DateTime);

        var (isClosed, opens, closes, label) = await ResolveDayAsync(localDate, ct);

        if (!isClosed && opens is { } o && closes is { } c && localTime >= o && localTime < c)
            return new OpeningStatus(true, label, null);

        // Zárva – keressük a következő nyitást (max. 14 napig előre).
        var next = await FindNextOpenAsync(local, ct);
        return new OpeningStatus(false, label, next);
    }

    /// <summary>Egy adott nap tényleges nyitvatartása (alap + felülírás összevonva).</summary>
    private async Task<(bool IsClosed, TimeOnly? Opens, TimeOnly? Closes, string? Label)> ResolveDayAsync(
        DateOnly date, CancellationToken ct)
    {
        var ovr = await _db.CalendarOverrides.FirstOrDefaultAsync(o => o.Date == date, ct);
        if (ovr is not null)
            return (ovr.IsClosed, ovr.OpensAt, ovr.ClosesAt, ovr.Label);

        var bh = await _db.BusinessHours.FirstOrDefaultAsync(b => b.Day == date.DayOfWeek, ct);
        if (bh is null || bh.IsClosed)
            return (true, null, null, null);

        return (false, bh.OpensAt, bh.ClosesAt, null);
    }

    private async Task<DateTimeOffset?> FindNextOpenAsync(DateTimeOffset fromLocal, CancellationToken ct)
    {
        for (var i = 0; i <= 14; i++)
        {
            var date = DateOnly.FromDateTime(fromLocal.DateTime).AddDays(i);
            var (isClosed, opens, _, _) = await ResolveDayAsync(date, ct);
            if (isClosed || opens is null) continue;

            var candidate = new DateTimeOffset(date.ToDateTime(opens.Value),
                LocalTz.GetUtcOffset(date.ToDateTime(opens.Value)));
            if (candidate > fromLocal)
                return candidate;
        }
        return null;
    }

    // --- Adminisztráció ---

    public Task<List<BusinessHour>> GetWeeklyHoursAsync(CancellationToken ct = default)
        => _db.BusinessHours.OrderBy(b => b.Day).ToListAsync(ct);

    public Task<List<CalendarOverride>> GetUpcomingOverridesAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_clock.UtcNow, LocalTz).DateTime);
        return _db.CalendarOverrides
            .Where(o => o.Date >= today)
            .OrderBy(o => o.Date)
            .ToListAsync(ct);
    }

    /// <summary>Egyedi ünnepnapi / ledolgozós / rövidített nyitvatartás felvitele vagy frissítése.</summary>
    public async Task UpsertOverrideAsync(DateOnly date, string label, bool isClosed,
        TimeOnly? opensAt, TimeOnly? closesAt, CancellationToken ct = default)
    {
        var ovr = await _db.CalendarOverrides.FirstOrDefaultAsync(o => o.Date == date, ct);
        if (ovr is null)
        {
            ovr = new CalendarOverride { Id = Guid.NewGuid(), Date = date, CreatedAtUtc = _clock.UtcNow };
            _db.CalendarOverrides.Add(ovr);
        }
        ovr.Label = label;
        ovr.IsClosed = isClosed;
        ovr.OpensAt = isClosed ? null : opensAt;
        ovr.ClosesAt = isClosed ? null : closesAt;

        await _db.SaveChangesAsync(ct);
        await SyncOutwardAsync(ct);
    }

    public async Task DeleteOverrideAsync(Guid id, CancellationToken ct = default)
    {
        var ovr = await _db.CalendarOverrides.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (ovr is null) return;
        _db.CalendarOverrides.Remove(ovr);
        await _db.SaveChangesAsync(ct);
        await SyncOutwardAsync(ct);
    }

    /// <summary>
    /// A következő 30 nap nyitvatartásának kiküldése a külső csatornákra (webshop, Google).
    /// Bármely naptár-módosítás automatikusan meghívja – ez a láncreakciós szinkron.
    /// </summary>
    public async Task SyncOutwardAsync(CancellationToken ct = default)
    {
        var startLocal = TimeZoneInfo.ConvertTime(_clock.UtcNow, LocalTz);
        var days = new List<DayOpening>();
        for (var i = 0; i < 30; i++)
        {
            var date = DateOnly.FromDateTime(startLocal.DateTime).AddDays(i);
            var (isClosed, opens, closes, label) = await ResolveDayAsync(date, ct);
            days.Add(new DayOpening(date, isClosed, opens, closes, label));
        }
        await _sync.SyncAsync(days, ct);
    }
}
