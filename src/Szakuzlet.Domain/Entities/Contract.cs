using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Domain.Entities;

/// <summary>
/// Próbakezeléses szerződés (III. Modul A). A KVL adataiból generált PDF (Word-sablonok helyett),
/// eIDAS SMS-kódos vagy papír-szkennelt hibrid aláírással. A jogi bizonyítékot (időbélyeg + IP)
/// az aláírás rögzíti.
/// </summary>
public class Contract
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    /// <summary>Szerződés sorszáma (üzleti hivatkozás).</summary>
    public string Number { get; set; } = string.Empty;

    public ServiceChannel Channel { get; set; }
    public ContractStatus Status { get; set; } = ContractStatus.Elokeszitve;

    // --- Eszköz és terápiás adatok ---
    public string DeviceModel { get; set; } = string.Empty;
    public string DeviceSerialNumber { get; set; } = string.Empty;
    public string? MaskModel { get; set; }

    /// <summary>Terápiás nyomásérték (vízcm).</summary>
    public decimal? PressureCmH2O { get; set; }

    /// <summary>A letett kaució összege.</summary>
    public decimal DepositAmount { get; set; }

    // --- Aláírás / jogi bizonyíték ---
    public SignatureMethod? SignatureMethod { get; set; }
    public DateTimeOffset? SignedAtUtc { get; set; }
    public string? SignedFromIp { get; set; }

    /// <summary>A generált szerződés-PDF hivatkozása (dokumentumtár).</summary>
    public string? PdfReference { get; set; }

    /// <summary>Papír-szkennelt ág esetén a beszkennelt aláírt példány hivatkozása.</summary>
    public string? ScannedReference { get; set; }

    // --- Metaadatok ---
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>A kaució bevételi bizonylatához tartozó számla (opcionális kapcsolat).</summary>
    public Guid? DepositInvoiceId { get; set; }
}
