namespace Szakuzlet.Application.Billing;

/// <summary>A kártyás fizetés eredménye a terminálról.</summary>
public record CardChargeResult(bool Approved, string? DeclineReason);

/// <summary>
/// A kártyás terminál (POS) absztrakciója (II. Modul D). A KVL a fizetendő összeget kiküldi
/// a terminálra; a NAV-számla csak SIKERES banki jóváhagyás után generálódhat. A valós terminál
/// integráció elkészültéig mock szolgálja ki (limit/fedezet szimulációval).
/// </summary>
public interface ICardTerminal
{
    Task<CardChargeResult> ChargeAsync(decimal amount, CancellationToken ct = default);
}
