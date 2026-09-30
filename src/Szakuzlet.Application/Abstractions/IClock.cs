namespace Szakuzlet.Application.Abstractions;

/// <summary>Az idő absztrakciója a determinisztikus tesztelhetőségért.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
