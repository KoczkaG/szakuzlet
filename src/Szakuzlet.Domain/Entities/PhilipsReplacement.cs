namespace Szakuzlet.Domain.Entities;

/// <summary>
/// A Philips-termékvisszahívás során kicserélt gép adatai. Korábban külső Excel-táblázatban
/// vezetve; ez importálja a KVL-be és köti a beteghez. Az érintett beteg megnyitásakor
/// automatikus piros riasztás jelenik meg (I. Modul D, 3. pont).
/// </summary>
public class PhilipsReplacement
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    /// <summary>A cserélt (új) készülék modellneve.</summary>
    public string ReplacementModel { get; set; } = string.Empty;

    /// <summary>A cserélt (új) készülék egyedi gyári száma.</summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>A csere dátuma (ha ismert az importált adatból).</summary>
    public DateOnly? ReplacedOn { get; set; }

    public DateTimeOffset ImportedAtUtc { get; set; }
}
