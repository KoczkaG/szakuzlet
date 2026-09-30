using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Szakuzlet.Application.Banking;

namespace Szakuzlet.Infrastructure.Banking;

/// <summary>
/// Ideiglenes kifizetés-exportáló. A valós SEPA/XML banki export és a Magyar Posta elektronikus
/// utalvány-API elkészültéig naplózza és memóriában megőrzi a tételeket (demó/teszt visszaellenőrzéshez).
/// </summary>
public sealed class MockPayoutExporter : IPayoutExporter
{
    private readonly ILogger<MockPayoutExporter> _logger;
    private readonly ConcurrentQueue<PayoutItem> _sepa = new();
    private readonly ConcurrentQueue<(string Name, string Address, decimal Amount)> _postal = new();

    public MockPayoutExporter(ILogger<MockPayoutExporter> logger) => _logger = logger;

    public IReadOnlyCollection<PayoutItem> Sepa => _sepa.ToArray();
    public IReadOnlyCollection<(string Name, string Address, decimal Amount)> Postal => _postal.ToArray();

    public Task ExportSepaAsync(PayoutItem item, CancellationToken ct = default)
    {
        _sepa.Enqueue(item);
        _logger.LogInformation("SEPA export (mock): {Name} {Amount} Ft", item.BeneficiaryName, item.Amount);
        return Task.CompletedTask;
    }

    public Task ExportPostalOrderAsync(string beneficiaryName, string address, decimal amount, CancellationToken ct = default)
    {
        _postal.Enqueue((beneficiaryName, address, amount));
        _logger.LogInformation("Postai utalvány (mock): {Name} {Amount} Ft", beneficiaryName, amount);
        return Task.CompletedTask;
    }
}
