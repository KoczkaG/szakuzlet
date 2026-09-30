using Szakuzlet.Application.Billing;

namespace Szakuzlet.Infrastructure.Billing;

/// <summary>
/// Ideiglenes kártyás terminál mock. A valós POS-integráció elkészültéig szolgál.
/// A demó/teszt kedvéért beállítható napi limit; a limit feletti összeget elutasítja
/// (a nagy értékű kauciós ügyeknél előforduló limit-hiba szimulálására).
/// </summary>
public sealed class MockCardTerminal : ICardTerminal
{
    /// <summary>A jóváhagyható maximum összeg. Alapból nincs korlát.</summary>
    public decimal Limit { get; set; } = decimal.MaxValue;

    public Task<CardChargeResult> ChargeAsync(decimal amount, CancellationToken ct = default)
    {
        if (amount > Limit)
            return Task.FromResult(new CardChargeResult(false, "Limit túllépés vagy fedezethiány."));
        return Task.FromResult(new CardChargeResult(true, null));
    }
}
