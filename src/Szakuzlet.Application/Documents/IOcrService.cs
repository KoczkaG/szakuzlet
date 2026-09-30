namespace Szakuzlet.Application.Documents;

/// <summary>Az ambuláns lapból kiolvasott adatok (III. Modul A).</summary>
public record AmbulanceSheetData(
    string? PatientName, string? Taj, string? PostalCode, string? City, string? AddressLine,
    string? DoctorName, string? DoctorStampCode, string? Institution,
    string? DeviceModel, string? MaskModel, decimal? PressureCmH2O, DateOnly? IssuedOn);

/// <summary>
/// OCR (karakterfelismerés) absztrakció az ambuláns lap / EESZT-PDF adatkiolvasásához.
/// A valós OCR-motor elkészültéig mock szolgálja ki. Az igazolványokat SOHA nem szkenneljük
/// (szigorú GDPR-elv); csak a szakorvosi ambuláns lapot.
/// </summary>
public interface IOcrService
{
    /// <summary>Egy dokumentum (kép/PDF) nyers tartalmából strukturált adatok kinyerése.</summary>
    Task<AmbulanceSheetData> ExtractAmbulanceSheetAsync(string rawContent, CancellationToken ct = default);
}
