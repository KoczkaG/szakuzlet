using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Application.Common;
using Szakuzlet.Application.DataSheets;
using Szakuzlet.Application.Invoices;
using Szakuzlet.Application.Kvl;
using Szakuzlet.Application.Notifications;
using Szakuzlet.Application.Orders;
using Szakuzlet.Application.Patients;
using Szakuzlet.Application.WearExpiry;
using Szakuzlet.Infrastructure.Kvl;
using Szakuzlet.Infrastructure.Notifications;
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

        // A valós SMS/e-mail szolgáltató elkészültéig memóriában naplózó küldő.
        // Singleton, hogy a demó/teszt vissza tudja olvasni a kiment üzeneteket.
        services.AddSingleton<InMemoryNotificationSender>();
        services.AddSingleton<INotificationSender>(sp => sp.GetRequiredService<InMemoryNotificationSender>());

        // Nyitvatartás kifelé szinkron (webshop, Google) – mock a valós API-kig.
        services.AddSingleton<Szakuzlet.Infrastructure.Calendar.LoggingOpeningHoursSync>();
        services.AddSingleton<Szakuzlet.Application.Calendar.IOpeningHoursSync>(
            sp => sp.GetRequiredService<Szakuzlet.Infrastructure.Calendar.LoggingOpeningHoursSync>());

        // Application szolgáltatások.
        services.AddScoped<EventRecorder>();
        services.AddScoped<Szakuzlet.Application.Dashboard.DashboardService>();
        services.AddScoped<DataSheetService>();
        services.AddScoped<PatientLookupService>();
        services.AddScoped<InvoiceParkingService>();
        services.AddScoped<OrderCompletionService>();
        services.AddScoped<WearExpiryService>();
        services.AddScoped<Szakuzlet.Application.Calendar.OpeningHoursService>();
        services.AddScoped<Szakuzlet.Application.CallCenter.CallCenterService>();
        services.AddScoped<Szakuzlet.Application.CallCenter.OutboundCallService>();

        // Futár-szinkron (GLS/MPL) mock a valós API-kig.
        services.AddSingleton<Szakuzlet.Infrastructure.Logistics.MockCourierClient>();
        services.AddSingleton<Szakuzlet.Application.Logistics.ICourierClient>(
            sp => sp.GetRequiredService<Szakuzlet.Infrastructure.Logistics.MockCourierClient>());

        services.AddScoped<Szakuzlet.Application.Timeline.TimelineService>();
        services.AddScoped<Szakuzlet.Application.Logistics.ShipmentService>();
        services.AddScoped<Szakuzlet.Application.Philips.PhilipsImportService>();

        return services;
    }
}
