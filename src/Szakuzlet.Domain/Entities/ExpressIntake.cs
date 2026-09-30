using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Domain.Entities;

/// <summary>
/// Online előkészített (expressz) ügymenet (III. Modul B). A beteg vagy hozzátartozó otthonról
/// tölti fel az adatokat/leletet; az előszűrő besorolja (NEAK / magán), majd a pult összekészíti,
/// és a beteg soron kívül veszi át. Variálás/maszkpróba esetén az expressz sáv megszakad.
/// </summary>
public class ExpressIntake
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public RequesterRole RequesterRole { get; set; }

    /// <summary>Hozzátartozó esetén az intézkedő neve/elérhetősége (értesítésekhez).</summary>
    public string? RequesterName { get; set; }
    public string? RequesterPhone { get; set; }
    public string? RequesterEmail { get; set; }

    public IntakeClassification Classification { get; set; }
    public ExpressIntakeStatus Status { get; set; } = ExpressIntakeStatus.Beerkezett;

    // --- Az orvosi javaslatból (OCR) átvett adatok ---
    public string? DeviceModel { get; set; }
    public string? MaskModel { get; set; }
    public decimal? PressureCmH2O { get; set; }

    /// <summary>A fizetendő végösszeg (előszűrés alapján: NEAK-önrész/kaució vagy teljes ár).</summary>
    public decimal PayableAmount { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ReadyAtUtc { get; set; }
}
