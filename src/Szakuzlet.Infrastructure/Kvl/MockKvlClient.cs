using System.Collections.Concurrent;
using Szakuzlet.Application.Kvl;

namespace Szakuzlet.Infrastructure.Kvl;

/// <summary>
/// Ideiglenes, memóriában futó KVL mock. A valós KVL API végpontok elkészültéig
/// szolgálja ki az <see cref="IKvlClient"/> szerződést, realisztikus seed adatokkal
/// (beleértve egy szándékos névazonosságot két 1956-03-22-i születésű partnernél,
/// hogy a "Keresési elágazás" tesztelhető legyen – I. Modul A, feladatlista 3. pont).
/// </summary>
public sealed class MockKvlClient : IKvlClient
{
    private readonly ConcurrentDictionary<string, KvlPartnerDto> _store = new();
    private int _seq = 1000;

    public MockKvlClient()
    {
        Seed(new KvlPartnerDto
        {
            PartnerCode = "KVL-1001",
            FullName = "Kovács Lajosné",
            DateOfBirth = new DateOnly(1956, 3, 22),
            TajNumber = "123 456 789",
            MobilePhone = "+36301234567",
            Email = "kovacs.lajosne@example.hu",
            PostalCode = "1145",
            City = "Budapest",
            AddressLine = "Lakatos utca 22."
        });
        // Szándékos névazonosság-teszt: azonos születési dátum, más személy.
        Seed(new KvlPartnerDto
        {
            PartnerCode = "KVL-1002",
            FullName = "Nagy Sándor",
            DateOfBirth = new DateOnly(1956, 3, 22),
            TajNumber = null,             // hiányos: TAJ nincs
            MobilePhone = null,           // hiányos: mobil nincs -> "Gyors adatfrissítési panel"
            Email = "nagy.sandor@example.hu",
            PostalCode = "4025",
            City = "Debrecen",
            AddressLine = "Piac utca 5."
        });
        Seed(new KvlPartnerDto
        {
            PartnerCode = "KVL-1003",
            FullName = "Szabó Erzsébet",
            DateOfBirth = new DateOnly(1970, 11, 8),
            TajNumber = "987 654 321",
            MobilePhone = "+36209876543",
            Email = null,                 // hiányos: e-mail nincs
            PostalCode = "6720",
            City = "Szeged",
            AddressLine = "Kárász utca 1."
        });
    }

    private void Seed(KvlPartnerDto dto) => _store[dto.PartnerCode!] = dto;

    public Task<IReadOnlyList<KvlPartnerDto>> FindPartnersByDateOfBirthAsync(
        DateOnly dateOfBirth, CancellationToken ct = default)
    {
        IReadOnlyList<KvlPartnerDto> result = _store.Values
            .Where(p => p.DateOfBirth == dateOfBirth)
            .OrderBy(p => p.FullName)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<KvlPartnerDto?> GetPartnerByCodeAsync(string partnerCode, CancellationToken ct = default)
    {
        _store.TryGetValue(partnerCode, out var dto);
        return Task.FromResult(dto);
    }

    public Task<string> UpsertPartnerAsync(KvlPartnerDto partner, CancellationToken ct = default)
    {
        var code = partner.PartnerCode;
        if (string.IsNullOrWhiteSpace(code))
            code = $"KVL-{Interlocked.Increment(ref _seq)}";

        _store[code] = partner with { PartnerCode = code };
        return Task.FromResult(code);
    }
}
