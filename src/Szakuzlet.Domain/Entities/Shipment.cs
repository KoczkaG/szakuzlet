using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Domain.Entities;

/// <summary>
/// Egy kiküldött csomag és annak élő futárszolgálati státusza. A státusz a futár-API
/// (GLS/MPL) valós idejű visszajelzése alapján frissül, és a Timeline-on jelenik meg.
/// </summary>
public class Shipment
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public Courier Courier { get; set; }

    /// <summary>A futárcég csomagazonosítója (tracking szám).</summary>
    public string TrackingNumber { get; set; } = string.Empty;

    public ShipmentStatus Status { get; set; } = ShipmentStatus.Feladva;

    /// <summary>Az átvevő személy neve (Átvéve státusznál).</summary>
    public string? ReceivedBy { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>A legutóbbi státuszfrissítés időpontja.</summary>
    public DateTimeOffset StatusUpdatedAtUtc { get; set; }
}
