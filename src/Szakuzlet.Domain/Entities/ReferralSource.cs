namespace Szakuzlet.Domain.Entities;

/// <summary>
/// Küldő intézmény / alváslabor / kezelőorvos törzsadata. Előre feltöltött, a hívásvégi
/// jegyzet legördülő menüjéből választható (I. Modul E, 2. pont), és a vezetői statisztika
/// alapja (honnan érkezik a legtöbb beteg).
/// </summary>
public class ReferralSource
{
    public Guid Id { get; set; }

    /// <summary>Intézmény / labor neve (pl. "Városi Kórház Alváslabor").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Konkrét kezelőorvos neve (opcionális).</summary>
    public string? DoctorName { get; set; }

    public bool IsActive { get; set; } = true;
}
