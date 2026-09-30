using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Application.Common;
using Szakuzlet.Domain.Entities;

namespace Szakuzlet.Application.Philips;

/// <summary>Egy sor a Philips-csereprojekt importból.</summary>
public record PhilipsImportRow(
    string? Taj, string PatientName, string ReplacementModel, string SerialNumber, DateOnly? ReplacedOn);

/// <summary>Az import eredménye.</summary>
public record PhilipsImportResult(int Matched, int Unmatched, IReadOnlyList<string> UnmatchedNames);

/// <summary>
/// A Philips-csereprojekt korábbi külső Excel-adatbázisának egyszeri importja a KVL-be
/// (I. Modul D, 3. pont). A sorokat a betegekhez köti (TAJ, fallback névazonosság alapján),
/// és rögzíti a gépcsere-adatokat a piros riasztáshoz.
/// </summary>
public sealed class PhilipsImportService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly EventRecorder _events;

    public PhilipsImportService(IAppDbContext db, IClock clock, EventRecorder events)
    {
        _db = db;
        _clock = clock;
        _events = events;
    }

    public async Task<PhilipsImportResult> ImportAsync(
        IReadOnlyList<PhilipsImportRow> rows, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var matched = 0;
        var unmatchedNames = new List<string>();

        foreach (var row in rows)
        {
            var patient = await MatchPatientAsync(row, ct);
            if (patient is null)
            {
                unmatchedNames.Add(row.PatientName);
                continue;
            }

            var existing = await _db.PhilipsReplacements
                .FirstOrDefaultAsync(x => x.PatientId == patient.Id, ct);
            if (existing is null)
            {
                existing = new PhilipsReplacement
                {
                    Id = Guid.NewGuid(),
                    PatientId = patient.Id,
                    ImportedAtUtc = now
                };
                _db.PhilipsReplacements.Add(existing);
            }
            existing.ReplacementModel = row.ReplacementModel;
            existing.SerialNumber = row.SerialNumber;
            existing.ReplacedOn = row.ReplacedOn;

            _events.Audit("PhilipsReplacement", patient.Id.ToString(), "PhilipsAdatImportalva",
                $"Modell: {row.ReplacementModel}, gyári szám: {row.SerialNumber}");
            _events.Timeline(patient.Id, "PhilipsCsere",
                $"Philips-csereprojekt: {row.ReplacementModel} (gyári szám: {row.SerialNumber}).");
            matched++;
        }

        await _db.SaveChangesAsync(ct);
        return new PhilipsImportResult(matched, unmatchedNames.Count, unmatchedNames);
    }

    private async Task<Patient?> MatchPatientAsync(PhilipsImportRow row, CancellationToken ct)
    {
        // Elsődleges kulcs: TAJ-szám (normalizált, csak számjegyek).
        if (!string.IsNullOrWhiteSpace(row.Taj))
        {
            var tajDigits = Digits(row.Taj);
            var byTaj = (await _db.Patients.Where(p => p.TajNumber != null).ToListAsync(ct))
                .FirstOrDefault(p => Digits(p.TajNumber!) == tajDigits);
            if (byTaj is not null) return byTaj;
        }

        // Fallback: pontos névegyezés (kis-nagybetű érzéketlen).
        var name = row.PatientName.Trim();
        return await _db.Patients.FirstOrDefaultAsync(
            p => p.FullName.ToLower() == name.ToLower(), ct);
    }

    private static string Digits(string s) => new(s.Where(char.IsDigit).ToArray());
}
