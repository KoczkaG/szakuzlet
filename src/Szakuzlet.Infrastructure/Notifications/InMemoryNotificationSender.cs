using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Szakuzlet.Application.Notifications;

namespace Szakuzlet.Infrastructure.Notifications;

/// <summary>
/// Ideiglenes értesítés-küldő: naplóz és memóriában megőrzi a kiment üzeneteket.
/// A valós SMS gateway / SMTP / marketing-szoftver integráció elkészültéig szolgál.
/// A megőrzött üzenetek a demó/teszt során visszaellenőrizhetők.
/// </summary>
public sealed class InMemoryNotificationSender : INotificationSender
{
    private readonly ILogger<InMemoryNotificationSender> _logger;
    private readonly ConcurrentQueue<NotificationMessage> _sent = new();

    public InMemoryNotificationSender(ILogger<InMemoryNotificationSender> logger) => _logger = logger;

    public IReadOnlyCollection<NotificationMessage> Sent => _sent.ToArray();

    public Task SendAsync(NotificationMessage message, CancellationToken ct = default)
    {
        _sent.Enqueue(message);
        _logger.LogInformation("Értesítés kiküldve [{Kind}] címzett={Recipient} tárgy={Subject}",
            message.Kind, message.Recipient, message.Subject);
        return Task.CompletedTask;
    }
}
