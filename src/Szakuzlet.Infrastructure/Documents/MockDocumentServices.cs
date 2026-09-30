using Szakuzlet.Application.Documents;

namespace Szakuzlet.Infrastructure.Documents;

/// <summary>
/// Ideiglenes dokumentum-generátor. A valós PDF-motor elkészültéig determinisztikus
/// hivatkozásokat ad vissza (a dokumentumtár helyett). Word-sablonokat nem használ.
/// </summary>
public sealed class MockDocumentGenerator : IDocumentGenerator
{
    public Task<GeneratedDocument> GenerateContractPdfAsync(Guid contractId, CancellationToken ct = default)
        => Task.FromResult(new GeneratedDocument($"contract-pdf/{contractId}"));

    public Task<GeneratedDocument> GenerateWarrantyAsync(Guid contractId, string serialNumber, CancellationToken ct = default)
        => Task.FromResult(new GeneratedDocument($"warranty/{contractId}/{serialNumber}"));
}

/// <summary>
/// Ideiglenes OCR mock. A valós motor elkészültéig a nyers tartalmat egyszerű „kulcs=érték”
/// soronkénti formátumban értelmezi (a demó/teszt így determinisztikus adatot adhat át).
/// </summary>
public sealed class MockOcrService : IOcrService
{
    public Task<AmbulanceSheetData> ExtractAmbulanceSheetAsync(string rawContent, CancellationToken ct = default)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in (rawContent ?? "").Split('\n'))
        {
            var idx = line.IndexOf('=');
            if (idx <= 0) continue;
            map[line[..idx].Trim()] = line[(idx + 1)..].Trim();
        }

        decimal? pressure = map.TryGetValue("pressure", out var p)
            && decimal.TryParse(p, System.Globalization.CultureInfo.InvariantCulture, out var pv) ? pv : null;
        DateOnly? issued = map.TryGetValue("issued", out var d) && DateOnly.TryParse(d, out var dv) ? dv : null;

        return Task.FromResult(new AmbulanceSheetData(
            Get(map, "name"), Get(map, "taj"), Get(map, "postal"), Get(map, "city"), Get(map, "address"),
            Get(map, "doctor"), Get(map, "stamp"), Get(map, "institution"),
            Get(map, "device"), Get(map, "mask"), pressure, issued));
    }

    private static string? Get(Dictionary<string, string> m, string k)
        => m.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;
}
