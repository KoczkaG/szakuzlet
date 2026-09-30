using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Domain.Entities;

/// <summary>
/// Egy digitális GDPR/adatlap-kitöltési esemény. A véglegesítéskor rögzített időbélyeg és IP-cím
/// adja a jogi bizonyítékot arra, hogy az aktív jelölőnégyzet-kiválasztás egyenértékű a kézi aláírással
/// (I. Modul A, 10. oldal – belső jogi megjegyzés).
/// </summary>
public class DataSheet
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public DataSheetStatus Status { get; set; } = DataSheetStatus.Kikuldve;
    public DataSheetChannel Channel { get; set; }

    /// <summary>Egyedi, biztonságos token a linkes eléréshez (SMS/e-mail). Csak online csatornánál releváns.</summary>
    public string? AccessToken { get; set; }

    /// <summary>A kiküldött link lejárati ideje.</summary>
    public DateTimeOffset? TokenExpiresAtUtc { get; set; }

    // --- Jogi bizonyíték a véglegesítéskor ---
    public DateTimeOffset? SubmittedAtUtc { get; set; }
    public string? SubmittedFromIp { get; set; }
    public string? SubmittedUserAgent { get; set; }

    // --- Metaadatok ---
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>A kitöltéshez tartozó hozzájárulási válaszok (a 4 kötelező kérdés).</summary>
    public ICollection<ConsentRecord> Consents { get; set; } = new List<ConsentRecord>();
}
