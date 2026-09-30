using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Application.CallCenter;

/// <summary>A hívásvégi jegyzet-ablak beviteli adatai.</summary>
public record CallNoteInput(
    CallTopic Topics,
    Guid? ReferralSourceId,
    string Summary,
    bool FollowUpRequired);
