namespace Szakuzlet.Application.Abstractions;

/// <summary>
/// Intelligens cím-adatbázis (I. Modul A, feladatlista 4. pont):
/// irányítószámhoz automatikusan hozzárendeli a település nevét,
/// minimalizálva a manuális gépelési hibákat.
/// </summary>
public interface IPostalCodeLookup
{
    /// <summary>A településnév(ek) lekérése irányítószám alapján. Üres, ha nincs találat.</summary>
    Task<IReadOnlyList<string>> GetCitiesAsync(string postalCode, CancellationToken ct = default);
}
