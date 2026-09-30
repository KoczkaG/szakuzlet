using System.Collections.Concurrent;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Application.Signing;

namespace Szakuzlet.Infrastructure.Signing;

/// <summary>
/// Ideiglenes eIDAS SMS-kódos aláírás mock. A valós szolgáltató elkészültéig determinisztikus
/// kódot generál (a demó/teszt kedvéért kiolvasható), és ellenőrzi. Éles környezetben a valós
/// hitelesítő szolgáltatásra cserélhető az interfész megtartásával.
/// </summary>
public sealed class MockSignatureService : ISignatureService
{
    private readonly IClock _clock;
    private readonly ConcurrentDictionary<string, (string Code, string Phone)> _challenges = new();

    public MockSignatureService(IClock clock) => _clock = clock;

    /// <summary>A demó/teszt lekérdezheti a kiküldött kódot egy kihíváshoz.</summary>
    public string? PeekCode(string challengeId)
        => _challenges.TryGetValue(challengeId, out var c) ? c.Code : null;

    public Task<SignatureChallenge> SendCodeAsync(string phoneNumber, CancellationToken ct = default)
    {
        var challengeId = Guid.NewGuid().ToString("N");
        // Determinisztikus 4 jegyű kód a telefonszám alapján (mockhoz).
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        var code = digits.Length >= 4 ? digits[^4..] : "1234";
        _challenges[challengeId] = (code, phoneNumber);
        return Task.FromResult(new SignatureChallenge(challengeId, phoneNumber));
    }

    public Task<SignatureVerification> VerifyAsync(string challengeId, string code, CancellationToken ct = default)
    {
        var ok = _challenges.TryGetValue(challengeId, out var c) && c.Code == code.Trim();
        if (ok) _challenges.TryRemove(challengeId, out _);
        return Task.FromResult(new SignatureVerification(ok, _clock.UtcNow));
    }
}
