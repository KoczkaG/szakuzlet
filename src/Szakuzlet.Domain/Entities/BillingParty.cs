namespace Szakuzlet.Domain.Entities;

/// <summary>
/// A számla vevő-adatai céges vagy egészségpénztári számlázáskor. Elkülönített blokk:
/// a beteg magánszemélyes törzsadatait SOHA nem írja felül (II. Modul A, 3. pont).
/// Owned entity az Invoice-on belül.
/// </summary>
public class BillingParty
{
    /// <summary>A számlán megjelenő név (céges név, vagy az EP hierarchikus összefűzött név).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Adószám (céges/szigorú EP esetén).</summary>
    public string? TaxNumber { get; set; }

    /// <summary>A számlán megjelenő cím (céges székhely vagy szigorú EP hivatalos címe).</summary>
    public string? Address { get; set; }
}
