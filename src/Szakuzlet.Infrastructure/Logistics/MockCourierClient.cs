using System.Collections.Concurrent;
using Szakuzlet.Application.Logistics;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Infrastructure.Logistics;

/// <summary>
/// Ideiglenes futár mock. A valós GLS/MPL API elkészültéig szolgál. A tesztelhetőség
/// érdekében a státusz kézzel beállítható (SetStatus), így demózható a Timeline-frissülés.
/// </summary>
public sealed class MockCourierClient : ICourierClient
{
    private readonly ConcurrentDictionary<string, CourierStatusDto> _statuses = new();

    /// <summary>Teszt/demó: egy csomag státuszának kézi beállítása.</summary>
    public void SetStatus(string trackingNumber, ShipmentStatus status, string? receivedBy, DateTimeOffset at)
        => _statuses[trackingNumber] = new CourierStatusDto(trackingNumber, status, receivedBy, at);

    public Task<CourierStatusDto?> GetStatusAsync(string trackingNumber, CancellationToken ct = default)
    {
        _statuses.TryGetValue(trackingNumber, out var dto);
        return Task.FromResult(dto);
    }
}
