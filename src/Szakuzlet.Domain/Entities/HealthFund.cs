namespace Szakuzlet.Domain.Entities;

/// <summary>
/// Egészségpénztár törzsadata (II. Modul B). A legtöbb EP elfogadja a könnyített, a beteg
/// lakcímére kiállított számlát; néhány „szigorú” EP viszont kizárólag a saját székhelyére és
/// adószámára – ezeket a <see cref="StrictBilling"/> „Riasztó/Zászló” jelöli.
/// </summary>
public class HealthFund
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Szigorú EP: kizárólag adószámmal és a saját hivatalos címével fogadja be a számlát.
    /// Ilyenkor a rendszer automatikusan átállítja a számlázási kategóriát.
    /// </summary>
    public bool StrictBilling { get; set; }

    /// <summary>Szigorú EP hivatalos székhelye (a számlára emelendő).</summary>
    public string? OfficialAddress { get; set; }

    /// <summary>Szigorú EP adószáma (a számlára emelendő).</summary>
    public string? TaxNumber { get; set; }

    public bool IsActive { get; set; } = true;
}
