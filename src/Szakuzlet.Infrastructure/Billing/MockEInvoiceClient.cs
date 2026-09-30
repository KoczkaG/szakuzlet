using Microsoft.Extensions.Logging;
using Szakuzlet.Application.Billing;

namespace Szakuzlet.Infrastructure.Billing;

/// <summary>
/// Ideiglenes e-számla kliens. A valós Számlázz.hu API elkészültéig egy determinisztikus,
/// hivatkozást generáló mock, ami „NAV-jelentettként” adja vissza az eredményt.
/// </summary>
public sealed class MockEInvoiceClient : IEInvoiceClient
{
    private readonly ILogger<MockEInvoiceClient> _logger;
    public MockEInvoiceClient(ILogger<MockEInvoiceClient> logger) => _logger = logger;

    public Task<EInvoiceResult> IssueAsync(EInvoiceRequest request, CancellationToken ct = default)
    {
        var reference = $"EINV-{request.InvoiceNumber}";
        _logger.LogInformation("E-számla kiállítva (mock): {Ref} vevő={Buyer}", reference, request.BuyerName);
        return Task.FromResult(new EInvoiceResult(reference, NavReported: true));
    }
}
