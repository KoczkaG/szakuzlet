namespace Szakuzlet.Domain.Entities;

/// <summary>
/// A „B” opció önkiszolgáló adatpótló kérése (I. Modul F). A pultos egyetlen gombnyomással
/// egyedi, biztonságos tokenes linket generál, amit SMS-ben/e-mailben kiküld a betegnek.
/// A beteg a linken maga pótolja a hiányzó kontaktadatokat.
/// </summary>
public class DataCompletionRequest
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public string Token { get; set; } = string.Empty;

    /// <summary>Mely mezők hiányoztak a kérés generálásakor (megjelenítéshez).</summary>
    public string MissingFields { get; set; } = string.Empty;

    public bool Completed { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
