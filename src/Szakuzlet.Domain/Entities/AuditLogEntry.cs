namespace Szakuzlet.Domain.Entities;

/// <summary>
/// Módosíthatatlan (append-only) audit napló bejegyzés. A GDPR- és jogi megfelelőség
/// alapja: minden érdemi adatkezelési műveletet visszakövethetően rögzít.
/// Az alkalmazás soha nem módosítja vagy törli a bejegyzéseket.
/// </summary>
public class AuditLogEntry
{
    public Guid Id { get; set; }

    /// <summary>Az érintett entitás típusa (pl. "DataSheet", "Patient", "ConsentRecord").</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Az érintett entitás azonosítója.</summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>A művelet megnevezése (pl. "AdatlapVeglegesitve", "AdatpotlasMentve").</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Szabad szöveges leírás / ok (pl. jogalap).</summary>
    public string? Details { get; set; }

    /// <summary>A műveletet végző (kezelő neve vagy "PACIENS"/"RENDSZER").</summary>
    public string Actor { get; set; } = "RENDSZER";

    public string? IpAddress { get; set; }

    public DateTimeOffset TimestampUtc { get; set; }
}
