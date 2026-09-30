using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Application.DataSheets;

/// <summary>A 4 kötelező hozzájárulási kérdés válaszai.</summary>
public record ConsentAnswers(
    bool KihordasiIdoTajekoztatas,
    bool Hirlevel,
    bool PostaiKuldemeny,
    bool EmailKuldemeny);

/// <summary>A páciens által megadott/frissített adatlap-mezők.</summary>
public record DataSheetInput
{
    public string FullName { get; init; } = string.Empty;
    public DateOnly? DateOfBirth { get; init; }
    public string? TajNumber { get; init; }
    public string? MobilePhone { get; init; }
    public string? Email { get; init; }
    public string? PostalCode { get; init; }
    public string? City { get; init; }
    public string? AddressLine { get; init; }
    public ConsentAnswers Consents { get; init; } = new(false, false, false, false);
}

/// <summary>A véglegesítéskor rögzítendő jogi bizonyíték-kontextus (időbélyeg mellé).</summary>
public record SubmissionContext(string? IpAddress, string? UserAgent);

/// <summary>Egy kiküldött adatlap-link adatai (SMS/e-mail számára).</summary>
public record DataSheetLink(Guid DataSheetId, string AccessToken, DateTimeOffset ExpiresAtUtc);
