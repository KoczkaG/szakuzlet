namespace Szakuzlet.Application.Signing;

/// <summary>Az eIDAS SMS-kódos hitelesítés kihívása (a kiküldött kód azonosítója).</summary>
public record SignatureChallenge(string ChallengeId, string PhoneNumber);

/// <summary>A hitelesítés eredménye.</summary>
public record SignatureVerification(bool Success, DateTimeOffset SignedAtUtc);

/// <summary>
/// eIDAS-konform SMS-kódos elektronikus aláírás absztrakciója (III. Modul A/C/E). A valós
/// szolgáltató (a bankok/Ügyfélkapu által is használt zárt technológia) elkészültéig mock
/// szolgálja ki: kiküld egy kódot, majd ellenőrzi. A sikeres hitelesítés jogilag egyenértékű
/// a kézi aláírással; az időbélyeget a rendszer rögzíti.
/// </summary>
public interface ISignatureService
{
    /// <summary>SMS-kód kiküldése a megadott számra; visszaadja a kihívás azonosítóját.</summary>
    Task<SignatureChallenge> SendCodeAsync(string phoneNumber, CancellationToken ct = default);

    /// <summary>A beírt kód ellenőrzése a kihívás ellen.</summary>
    Task<SignatureVerification> VerifyAsync(string challengeId, string code, CancellationToken ct = default);
}
