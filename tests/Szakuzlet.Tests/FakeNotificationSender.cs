using Szakuzlet.Application.Notifications;

namespace Szakuzlet.Tests;

public sealed class FakeNotificationSender : INotificationSender
{
    public List<NotificationMessage> Sent { get; } = new();

    public Task SendAsync(NotificationMessage message, CancellationToken ct = default)
    {
        Sent.Add(message);
        return Task.CompletedTask;
    }
}
