using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Domain.Entities;

/// <summary>
/// A beteghez rendelt egy telefonszám (beteg mobil, házi szám, vagy jogilag jóváhagyott
/// kapcsolattartó/hozzátartozó/megbízott). A Click-to-Call minden ilyen szám mellett indítható.
/// </summary>
public class PatientPhone
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public PhoneKind Kind { get; set; }

    public string Number { get; set; } = string.Empty;

    /// <summary>A kapcsolattartó/hozzátartozó neve (ha nem maga a beteg száma).</summary>
    public string? ContactName { get; set; }

    /// <summary>
    /// Jogilag jóváhagyott-e a kapcsolattartói szám kezelése/hívása. A beteg saját
    /// mobil/házi száma alapból jóváhagyottnak tekintendő.
    /// </summary>
    public bool LegallyApproved { get; set; }
}
