using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Application.Logistics;

/// <summary>Egy csomag aktuális státusza a futárcég (GLS/MPL) rendszeréből.</summary>
public record CourierStatusDto(string TrackingNumber, ShipmentStatus Status, string? ReceivedBy, DateTimeOffset UpdatedAtUtc);

/// <summary>
/// A futárszolgálati rendszerek (GLS / MPL) API-jának absztrakciója. A valós API-k
/// elkészültéig mock implementáció szolgálja ki. A pultosnak szigorúan tilos külső
/// futárfelületeken keresgélnie – minden státusz a Timeline-on jelenik meg.
/// </summary>
public interface ICourierClient
{
    /// <summary>Egy csomag aktuális státuszának lekérdezése tracking szám alapján.</summary>
    Task<CourierStatusDto?> GetStatusAsync(string trackingNumber, CancellationToken ct = default);
}
