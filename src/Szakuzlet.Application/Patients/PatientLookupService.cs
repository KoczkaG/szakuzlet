using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Application.Kvl;
using Szakuzlet.Domain.Entities;

namespace Szakuzlet.Application.Patients;

/// <summary>Egy találat a régi ügyfél kereséséből (a tableten megjelenítendő).</summary>
public record PatientMatch(Guid PatientId, string FullName, string? MaskedCity, bool HasMissingContact);

/// <summary>A keresés kimenete a "Keresési elágazás" (I. Modul A, feladatlista 3. pont) szerint.</summary>
public record LookupResult(IReadOnlyList<PatientMatch> Matches)
{
    /// <summary>Nincs találat – a tableten "Írja be a nevét" / "Kérje munkatársunk segítségét".</summary>
    public bool IsEmpty => Matches.Count == 0;

    /// <summary>Névazonosság – több találat, a páciensnek/pultosnak választania kell.</summary>
    public bool IsAmbiguous => Matches.Count > 1;

    /// <summary>Egyértelmű találat.</summary>
    public bool IsSingle => Matches.Count == 1;
}

/// <summary>
/// Régi ügyfelek diszkrét előhívása születési dátum (vagy KVL partnerkód) alapján.
/// A találatokat a helyi adatbázisból és a KVL-ből is összegyűjti, és a hiányzó
/// helyieket importálja, hogy a "Gyors adatfrissítési panel" dolgozhasson velük.
/// </summary>
public sealed class PatientLookupService
{
    private readonly IAppDbContext _db;
    private readonly IKvlClient _kvl;
    private readonly IClock _clock;

    public PatientLookupService(IAppDbContext db, IKvlClient kvl, IClock clock)
    {
        _db = db;
        _kvl = kvl;
        _clock = clock;
    }

    public async Task<LookupResult> FindByDateOfBirthAsync(DateOnly dob, CancellationToken ct = default)
    {
        // 1) KVL-ből származó találatok importálása/frissítése a helyi DB-be.
        var kvlPartners = await _kvl.FindPartnersByDateOfBirthAsync(dob, ct);
        foreach (var p in kvlPartners)
            await EnsureLocalPatientAsync(p, ct);

        // 2) Helyi találatok (a frissen importáltakkal együtt).
        var locals = await _db.Patients
            .Where(p => p.DateOfBirth == dob)
            .OrderBy(p => p.FullName)
            .ToListAsync(ct);

        var matches = locals
            .Select(p => new PatientMatch(p.Id, p.FullName, MaskCity(p.City), p.HasMissingRequiredContact))
            .ToList();

        return new LookupResult(matches);
    }

    public async Task<PatientMatch?> FindByPartnerCodeAsync(string partnerCode, CancellationToken ct = default)
    {
        var local = await _db.Patients.FirstOrDefaultAsync(p => p.KvlPartnerCode == partnerCode, ct);
        if (local is null)
        {
            var kvl = await _kvl.GetPartnerByCodeAsync(partnerCode, ct);
            if (kvl is null) return null;
            local = await EnsureLocalPatientAsync(kvl, ct);
        }
        return new PatientMatch(local.Id, local.FullName, MaskCity(local.City), local.HasMissingRequiredContact);
    }

    /// <summary>A KVL partner leképezése helyi Patient entitásra (import vagy frissítés).</summary>
    private async Task<Patient> EnsureLocalPatientAsync(KvlPartnerDto dto, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        Patient? patient = null;
        if (!string.IsNullOrWhiteSpace(dto.PartnerCode))
            patient = await _db.Patients.FirstOrDefaultAsync(p => p.KvlPartnerCode == dto.PartnerCode, ct);

        if (patient is null)
        {
            patient = new Patient
            {
                Id = Guid.NewGuid(),
                KvlPartnerCode = dto.PartnerCode,
                IsLegacy = true,
                CreatedAtUtc = now
            };
            _db.Patients.Add(patient);
        }

        patient.FullName = dto.FullName;
        patient.DateOfBirth = dto.DateOfBirth;
        patient.TajNumber = dto.TajNumber;
        patient.MobilePhone = dto.MobilePhone;
        patient.Email = dto.Email;
        patient.PostalCode = dto.PostalCode;
        patient.City = dto.City;
        patient.AddressLine = dto.AddressLine;
        patient.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(ct);
        return patient;
    }

    /// <summary>Diszkréció: a városnév első betűjén túl elrejtjük (a tableten csak segédinfó).</summary>
    private static string? MaskCity(string? city)
        => string.IsNullOrWhiteSpace(city) ? null : $"{city[0]}…";
}
