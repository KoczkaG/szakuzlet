using Szakuzlet.Application.Billing;

namespace Szakuzlet.Tests;

public sealed class FakeEInvoiceClient : IEInvoiceClient
{
    public List<EInvoiceRequest> Issued { get; } = new();

    public Task<EInvoiceResult> IssueAsync(EInvoiceRequest request, CancellationToken ct = default)
    {
        Issued.Add(request);
        return Task.FromResult(new EInvoiceResult($"EINV-{request.InvoiceNumber}", NavReported: true));
    }
}

/// <summary>Közös factory a BillingService (és EanPool) teszt-példányosításához.</summary>
public static class BillingTestFactory
{
    public static (BillingService billing, EanPoolService eanPool, FakeNotificationSender notif,
        FakeEInvoiceClient einv, Szakuzlet.Infrastructure.Billing.MockCardTerminal terminal)
        Create(TestDb db, FakeClock clock)
    {
        var events = new Szakuzlet.Application.Common.EventRecorder(db.Context, clock);
        var notif = new FakeNotificationSender();
        var einv = new FakeEInvoiceClient();
        var terminal = new Szakuzlet.Infrastructure.Billing.MockCardTerminal();
        var numbers = new Szakuzlet.Infrastructure.Services.InvoiceNumberGenerator(db.Context, clock);
        var eanPool = new EanPoolService(db.Context, clock, events, notif);
        var billing = new BillingService(db.Context, clock, events, numbers, eanPool, einv, notif, terminal);
        return (billing, eanPool, notif, einv, terminal);
    }
}
