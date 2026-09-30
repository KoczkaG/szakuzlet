using Szakuzlet.Application.Abstractions;

namespace Szakuzlet.Infrastructure.Services;

/// <summary>
/// Egyszerű, beépített irányítószám → település kereső. Kezdő adathalmazzal indul;
/// éles környezetben a teljes magyar irányítószám-adatbázisra (CSV/DB) cserélhető
/// az interfész megtartásával.
/// </summary>
public sealed class StaticPostalCodeLookup : IPostalCodeLookup
{
    // Egy irányítószámhoz több település is tartozhat (pl. közös postai körzet).
    private static readonly Dictionary<string, string[]> Map = new()
    {
        ["1011"] = ["Budapest"],
        ["1051"] = ["Budapest"],
        ["1145"] = ["Budapest"],
        ["2000"] = ["Szentendre"],
        ["2100"] = ["Gödöllő"],
        ["3300"] = ["Eger"],
        ["4025"] = ["Debrecen"],
        ["4400"] = ["Nyíregyháza"],
        ["5000"] = ["Szolnok"],
        ["6000"] = ["Kecskemét"],
        ["6720"] = ["Szeged"],
        ["7100"] = ["Szekszárd"],
        ["7400"] = ["Kaposvár"],
        ["7621"] = ["Pécs"],
        ["8000"] = ["Székesfehérvár"],
        ["8200"] = ["Veszprém"],
        ["8800"] = ["Nagykanizsa"],
        ["9000"] = ["Győr"],
        ["9700"] = ["Szombathely"],
    };

    public Task<IReadOnlyList<string>> GetCitiesAsync(string postalCode, CancellationToken ct = default)
    {
        var key = (postalCode ?? string.Empty).Trim();
        IReadOnlyList<string> result = Map.TryGetValue(key, out var cities)
            ? cities
            : Array.Empty<string>();
        return Task.FromResult(result);
    }
}
