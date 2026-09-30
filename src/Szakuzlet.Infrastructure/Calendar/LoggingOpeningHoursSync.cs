using Microsoft.Extensions.Logging;
using Szakuzlet.Application.Calendar;

namespace Szakuzlet.Infrastructure.Calendar;

/// <summary>
/// Ideiglenes nyitvatartás-szinkron: naplózza és memóriában megőrzi a legutóbb kiküldött
/// nyitvatartást. A valós Google Business Profile / webshop API elkészültéig szolgál.
/// </summary>
public sealed class LoggingOpeningHoursSync : IOpeningHoursSync
{
    private readonly ILogger<LoggingOpeningHoursSync> _logger;

    public LoggingOpeningHoursSync(ILogger<LoggingOpeningHoursSync> logger) => _logger = logger;

    /// <summary>A legutóbb szinkronizált napok (demó/teszt visszaellenőrzéshez).</summary>
    public IReadOnlyList<DayOpening> LastSynced { get; private set; } = Array.Empty<DayOpening>();

    public Task SyncAsync(IReadOnlyList<DayOpening> days, CancellationToken ct = default)
    {
        LastSynced = days;
        _logger.LogInformation(
            "Nyitvatartás szinkronizálva {Count} napra (webshop + Google mock).", days.Count);
        return Task.CompletedTask;
    }
}
