using Szakuzlet.Application.Calendar;

namespace Szakuzlet.Tests;

public sealed class FakeOpeningHoursSync : IOpeningHoursSync
{
    public int SyncCount { get; private set; }
    public IReadOnlyList<DayOpening> LastSynced { get; private set; } = Array.Empty<DayOpening>();

    public Task SyncAsync(IReadOnlyList<DayOpening> days, CancellationToken ct = default)
    {
        SyncCount++;
        LastSynced = days;
        return Task.CompletedTask;
    }
}
