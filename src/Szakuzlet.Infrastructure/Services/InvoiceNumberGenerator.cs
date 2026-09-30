using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Infrastructure.Persistence;

namespace Szakuzlet.Infrastructure.Services;

/// <summary>
/// Egyszerű, egyedi számlasorszám-generátor: „SZ-{év}-{sorszám}” formátum. A folyamatot
/// egy szemafor sorosítja a folyamaton belül; az adatbázis egyedi indexe adja a végső védelmet
/// a duplikáció ellen. (Éles, több példányos környezetben adatbázis-szekvenciára cserélhető.)
/// </summary>
public sealed class InvoiceNumberGenerator : IInvoiceNumberGenerator
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public InvoiceNumberGenerator(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<string> NextAsync(CancellationToken ct = default)
    {
        var year = _clock.UtcNow.Year;
        var prefix = $"SZ-{year}-";

        await Gate.WaitAsync(ct);
        try
        {
            var lastForYear = await _db.Invoices
                .Where(i => i.Number.StartsWith(prefix))
                .Select(i => i.Number)
                .ToListAsync(ct);

            var maxSeq = lastForYear
                .Select(n => int.TryParse(n[prefix.Length..], out var v) ? v : 0)
                .DefaultIfEmpty(0)
                .Max();

            return $"{prefix}{maxSeq + 1:D5}";
        }
        finally
        {
            Gate.Release();
        }
    }
}
