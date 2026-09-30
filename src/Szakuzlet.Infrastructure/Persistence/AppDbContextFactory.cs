using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Szakuzlet.Infrastructure.Persistence;

/// <summary>
/// Design-time factory az EF Core CLI eszközök (migrációk) számára.
/// A verziózott migrációkat mindig a PostgreSQL provider ellen generáljuk,
/// mert az az éles cél-adatbázis.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=szakuzlet;Username=postgres;Password=postgres")
            .Options;
        return new AppDbContext(options);
    }
}
