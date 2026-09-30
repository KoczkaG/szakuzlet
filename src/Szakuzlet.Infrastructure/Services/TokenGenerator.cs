using System.Security.Cryptography;
using Szakuzlet.Application.Abstractions;

namespace Szakuzlet.Infrastructure.Services;

public sealed class TokenGenerator : ITokenGenerator
{
    public string CreateToken()
    {
        // 32 bájt entrópia, URL-biztos base64 kódolással.
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
