using System.Collections.Concurrent;
using Szakuzlet.Application.Banking;

namespace Szakuzlet.Infrastructure.Banking;

/// <summary>
/// Ideiglenes banki mock. A valós banki API elkészültéig szolgál; a demó/teszt kézzel
/// „beérkeztethet” egy jóváírást egy hivatkozásra (SimulateTransfer).
/// </summary>
public sealed class MockBankClient : IBankClient
{
    private readonly ConcurrentDictionary<string, IncomingTransfer> _transfers = new();

    public void SimulateTransfer(string reference, decimal amount, DateTimeOffset at)
        => _transfers[reference] = new IncomingTransfer(reference, amount, at);

    public Task<IncomingTransfer?> FindTransferAsync(string reference, CancellationToken ct = default)
    {
        _transfers.TryGetValue(reference, out var t);
        return Task.FromResult(t);
    }
}
