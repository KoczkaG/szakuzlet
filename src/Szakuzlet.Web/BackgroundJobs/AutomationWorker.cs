using Szakuzlet.Application.Invoices;
using Szakuzlet.Application.Logistics;
using Szakuzlet.Application.WearExpiry;

namespace Szakuzlet.Web.BackgroundJobs;

/// <summary>
/// Ütemezett háttérfolyamat az I. Modul A) automatizmusaihoz:
/// - lejárt számla-parkoltatások időzített lezárása (8. pont),
/// - kihordási idő lejárati értesítők kiküldése (9. pont).
///
/// Egyszerű időzítővel fut; éles környezetben ütemezőre (pl. cron/Hangfire) cserélhető.
/// </summary>
public sealed class AutomationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutomationWorker> _logger;

    // Fejlesztésben gyakori futás a demózhatóságért; élesben naponta elég.
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    public AutomationWorker(IServiceScopeFactory scopeFactory, ILogger<AutomationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Kis késleltetés indulás után, hogy a migráció/DB-init lefusson.
        try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var parking = scope.ServiceProvider.GetRequiredService<InvoiceParkingService>();
                var wear = scope.ServiceProvider.GetRequiredService<WearExpiryService>();
                var shipments = scope.ServiceProvider.GetRequiredService<ShipmentService>();

                var closed = await parking.CloseExpiredParkingsAsync(stoppingToken);
                var notified = await wear.RunAsync(stoppingToken);
                var shipmentUpdates = await shipments.SyncOpenShipmentsAsync(stoppingToken);

                if (closed > 0 || notified > 0 || shipmentUpdates > 0)
                    _logger.LogInformation(
                        "Automatizmus lefutott: {Closed} parkoltatás lezárva, {Notified} kihordási értesítő, " +
                        "{Shipments} csomagstátusz frissítve.",
                        closed, notified, shipmentUpdates);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hiba az automatizmus háttérfolyamatban.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
