using System.Globalization;
using Szakuzlet.Application.Philips;

namespace Szakuzlet.Infrastructure.Philips;

/// <summary>
/// A Philips-csereprojekt importfájl feldolgozója. Éles környezetben a külső Excel helyett
/// egy egyszerű CSV formátumot vár (fejléc: taj,nev,modell,gyariszam,csere_datum).
/// A CSV a legkisebb súrlódású formátum az egyszeri, adminisztrátori importhoz.
/// </summary>
public static class PhilipsCsvParser
{
    public static IReadOnlyList<PhilipsImportRow> Parse(string csv)
    {
        var rows = new List<PhilipsImportRow>();
        using var reader = new StringReader(csv);

        string? line;
        var first = true;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = SplitCsvLine(line);

            // Fejlécsor átugrása (ha az első oszlop nem szám és "taj"-jal kezdődik).
            if (first)
            {
                first = false;
                if (cols.Length > 0 && cols[0].Trim().StartsWith("taj", StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            if (cols.Length < 4) continue;

            var taj = Empty(cols, 0);
            var name = Empty(cols, 1) ?? "";
            var model = Empty(cols, 2) ?? "";
            var serial = Empty(cols, 3) ?? "";
            DateOnly? replacedOn = null;
            if (cols.Length >= 5 && DateOnly.TryParse(cols[4].Trim(),
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                replacedOn = d;

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(serial))
                continue;

            rows.Add(new PhilipsImportRow(taj, name, model, serial, replacedOn));
        }

        return rows;
    }

    private static string? Empty(string[] cols, int i)
        => i < cols.Length && !string.IsNullOrWhiteSpace(cols[i]) ? cols[i].Trim() : null;

    private static string[] SplitCsvLine(string line)
    {
        // Egyszerű vesszős szeparálás, idézőjeles mezők támogatásával.
        var result = new List<string>();
        var sb = new System.Text.StringBuilder();
        var inQuotes = false;
        foreach (var ch in line)
        {
            if (ch == '"') { inQuotes = !inQuotes; continue; }
            if (ch == ',' && !inQuotes) { result.Add(sb.ToString()); sb.Clear(); continue; }
            sb.Append(ch);
        }
        result.Add(sb.ToString());
        return result.ToArray();
    }
}
