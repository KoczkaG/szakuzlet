using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Application.DataSheets;
using Szakuzlet.Application.Kvl;
using Szakuzlet.Application.Patients;
using Szakuzlet.Infrastructure.Kvl;
using Szakuzlet.Infrastructure.Persistence;
using Szakuzlet.Infrastructure.Services;

namespace Szakuzlet.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        // Provider-váltás: éles környezetben PostgreSQL (a céges szerveren),
        // fejlesztéshez SQLite is választható a Domain-modell módosítása nélkül.
        var provider = config["Database:Provider"] ?? "Postgres";
        var connectionString = config.GetConnectionString("Default");

        services.AddDbContext<AppDbContext>(opt =>
        {
            if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                opt.UseSqlite(connectionString ?? "Data Source=szakuzlet.db");
            }
            else
            {
                opt.UseNpgsql(connectionString
                    ?? "Host=localhost;Database=szakuzlet;Username=postgres;Password=postgres");
            }
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ITokenGenerator, TokenGenerator>();
        services.AddSingleton<IPostalCodeLookup, StaticPostalCodeLookup>();

        // A valós KVL API elkészültéig mock. Cseréje: egyetlen sor módosítása.
        services.AddSingleton<IKvlClient, MockKvlClient>();

        // Application szolgáltatások.
        services.AddScoped<DataSheetService>();
        services.AddScoped<PatientLookupService>();

        return services;
    }
}
