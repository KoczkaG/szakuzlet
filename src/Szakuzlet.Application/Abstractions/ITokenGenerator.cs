namespace Szakuzlet.Application.Abstractions;

/// <summary>Kriptográfiailag biztonságos, egyedi hozzáférési token generálása a linkes eléréshez.</summary>
public interface ITokenGenerator
{
    string CreateToken();
}
