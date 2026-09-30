namespace Szakuzlet.Application.DataQuality;

/// <summary>
/// A feltételes pulti riasztás eredménye adatlap-megnyitáskor. Ha bármelyik kötelező
/// kontaktmező hiányzik, a pultosnak választania kell az „A”/„B” opció közül.
/// </summary>
public record MissingContactAlert(bool HasMissing, IReadOnlyList<string> MissingFields)
{
    public string MissingFieldsText => string.Join(", ", MissingFields);
}

/// <summary>Az „A” opcióban a pultos által rögzített hiányzó adatok.</summary>
public record ContactUpdate(string? Email, string? MobilePhone, string? TajNumber);
