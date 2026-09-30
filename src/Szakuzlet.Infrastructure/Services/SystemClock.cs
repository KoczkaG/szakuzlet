using Szakuzlet.Application.Abstractions;

namespace Szakuzlet.Infrastructure.Services;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
