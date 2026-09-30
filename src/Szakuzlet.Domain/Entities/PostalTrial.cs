using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Domain.Entities;

/// <summary>
/// Postai úton indított próbakezelés (III. Modul C). Végigköveti az életciklust: igény →
/// előre utalás (banki szinkron) → digitális szerződés-aláírás (raktári zár) → kézbesítés →
/// 8 napos maszkcsere és kontroll-követés → lezárás.
/// </summary>
public class PostalTrial
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    /// <summary>Egyedi rendelésszám / banki közlemény az utalás párosításához.</summary>
    public string OrderNumber { get; set; } = string.Empty;

    public PostalTrialStatus Status { get; set; } = PostalTrialStatus.Igenyelve;

    public string? DeviceModel { get; set; }
    public string? MaskModel { get; set; }

    /// <summary>A fizetendő (előre utalandó) összeg.</summary>
    public decimal PayableAmount { get; set; }

    /// <summary>A kapcsolódó szerződés (digitális aláírás után).</summary>
    public Guid? ContractId { get; set; }

    /// <summary>A kapcsolódó csomag (feladás után).</summary>
    public Guid? ShipmentId { get; set; }

    // --- Kézbesítés / próbaidőszak ---
    public DateTimeOffset? DeliveredAtUtc { get; set; }

    /// <summary>A kötelező 2 havi próbaidőszak lejárati dátuma (a kézbesítéstől számítva).</summary>
    public DateOnly? TrialDeadline { get; set; }

    /// <summary>8 napos maszkcsere-garancia lejárata (a kézbesítéstől).</summary>
    public DateOnly? MaskSwapDeadline { get; set; }

    /// <summary>Kórházi kontroll időpontja (ha ismert).</summary>
    public DateOnly? ControlDate { get; set; }

    /// <summary>Elküldtük-e már a lezárás előtti „Hahó” emlékeztetőt.</summary>
    public bool ReminderSent { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
