using Szakuzlet.Application.Abstractions;
using Szakuzlet.Domain.Entities;

namespace Szakuzlet.Application.Common;

/// <summary>
/// Közös segéd az audit-napló és az ügyfél-idővonal (Timeline) bejegyzések rögzítéséhez.
/// Nem hív SaveChanges-t; a hívó tranzakciója menti.
/// </summary>
public sealed class EventRecorder
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public EventRecorder(IAppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public void Audit(string entityType, string entityId, string action,
        string? details = null, string actor = "RENDSZER", string? ip = null)
    {
        _db.AuditLog.Add(new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Details = details,
            Actor = actor,
            IpAddress = ip,
            TimestampUtc = _clock.UtcNow
        });
    }

    public void Timeline(Guid patientId, string eventType, string summary)
    {
        _db.TimelineEvents.Add(new TimelineEvent
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            EventType = eventType,
            Summary = summary,
            OccurredAtUtc = _clock.UtcNow
        });
    }
}
