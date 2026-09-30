using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Application.CallCenter;

/// <summary>Egy témakör előfordulása és aránya.</summary>
public record TopicCount(CallTopic Topic, string Label, int Count, double Percentage);

/// <summary>Egy küldő intézmény / alváslabor rangsor-tétele.</summary>
public record ReferralCount(Guid ReferralSourceId, string Name, string? DoctorName, int Count);

/// <summary>A vezetői hívásstatisztikai kimutatás egy adott időszakra.</summary>
public record CallStatistics(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int TotalNotes,
    IReadOnlyList<TopicCount> TopicBreakdown,
    IReadOnlyList<ReferralCount> ReferralRanking,
    int ComplaintCount);

/// <summary>
/// Vezetői hívásstatisztikai Dashboard (I. Modul E, 4. pont). A hívásvégi jegyzetek adataiból
/// összesíti a hívásokok megoszlását, a küldő alváslaborok/orvosok rangsorát és a panaszok számát.
/// </summary>
public sealed class CallStatisticsService
{
    private readonly IAppDbContext _db;

    public CallStatisticsService(IAppDbContext db) => _db = db;

    private static readonly (CallTopic Topic, string Label)[] AllTopics =
    {
        (CallTopic.UjUgyfelInfo, "Új ügyfél információ"),
        (CallTopic.RendelesLeadas, "Rendelés leadás"),
        (CallTopic.Venybevaltas, "Vénybeváltás"),
        (CallTopic.MaszkbeallitasTerapiasKerdes, "Maszkbeállítás / terápiás kérdés"),
        (CallTopic.SzamlazasPenzugy, "Számlázás / pénzügy"),
        (CallTopic.SzervizGarancia, "Szerviz / garancia"),
        (CallTopic.Panaszkezeles, "Panaszkezelés"),
    };

    public async Task<CallStatistics> GetAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc,
        CancellationToken ct = default)
    {
        var notes = await _db.CallNotes
            .Where(n => n.CreatedAtUtc >= fromUtc && n.CreatedAtUtc < toUtc)
            .Include(n => n.ReferralSource)
            .ToListAsync(ct);

        var total = notes.Count;

        // Témakörök megoszlása (egy jegyzet több témakört is jelölhet).
        var topicBreakdown = AllTopics.Select(t =>
        {
            var count = notes.Count(n => n.Topics.HasFlag(t.Topic));
            var pct = total == 0 ? 0 : Math.Round(count * 100.0 / total, 1);
            return new TopicCount(t.Topic, t.Label, count, pct);
        })
        .OrderByDescending(t => t.Count)
        .ToList();

        // Küldő alváslaborok / orvosok rangsora.
        var referralRanking = notes
            .Where(n => n.ReferralSource is not null)
            .GroupBy(n => n.ReferralSource!)
            .Select(g => new ReferralCount(g.Key.Id, g.Key.Name, g.Key.DoctorName, g.Count()))
            .OrderByDescending(r => r.Count)
            .ToList();

        var complaints = notes.Count(n => n.Topics.HasFlag(CallTopic.Panaszkezeles));

        return new CallStatistics(fromUtc, toUtc, total, topicBreakdown, referralRanking, complaints);
    }
}
