namespace Szakuzlet.Application.Banking;

/// <summary>Egy beérkezett banki jóváírás (a közlemény/azonosító alapján párosítható).</summary>
public record IncomingTransfer(string Reference, decimal Amount, DateTimeOffset ReceivedAtUtc);

/// <summary>
/// A banki tranzakciók figyelésének absztrakciója (III. Modul C). Az előre utalásos
/// próbakezeléseknél az egyedi rendelésszám (közlemény) alapján párosítja a beérkező összeget.
/// A valós banki API elkészültéig mock szolgálja ki.
/// </summary>
public interface IBankClient
{
    /// <summary>Beérkezett-e jóváírás a megadott egyedi hivatkozásra (közleményre)? Null, ha még nem.</summary>
    Task<IncomingTransfer?> FindTransferAsync(string reference, CancellationToken ct = default);
}
