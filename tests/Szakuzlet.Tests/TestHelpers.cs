using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Infrastructure.Persistence;

namespace Szakuzlet.Tests;

/// <summary>Determinisztikus óra a tesztekhez.</summary>
public sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
}

/// <summary>Kiszámítható token-generátor a tesztekhez.</summary>
public sealed class FakeTokenGenerator : ITokenGenerator
{
    private int _n;
    public string CreateToken() => $"token-{++_n}";
}

/// <summary>
/// SQLite in-memory adatbázis egy tesztre. A kapcsolatot nyitva kell tartani,
/// amíg a teszt fut, különben a memória-DB megszűnik.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _conn;
    public AppDbContext Context { get; }

    public TestDb()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_conn)
            .Options;
        Context = new AppDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        _conn.Dispose();
    }
}
