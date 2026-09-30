using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Domain.Entities;

/// <summary>
/// Belső pulti feladat (a Feladatkezelő eleme). Automatizmusok generálják,
/// pl. a „Zéró hozzájárulás” radar egy tisztázó telefonhívást.
/// </summary>
public class PatientTask
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public PatientTaskType Type { get; set; }
    public PatientTaskStatus Status { get; set; } = PatientTaskStatus.Nyitott;

    public string Title { get; set; } = string.Empty;
    public string? Details { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }

    /// <summary>Opcionális kapcsolat egy parkoltatott számlához.</summary>
    public Guid? InvoiceId { get; set; }
}
