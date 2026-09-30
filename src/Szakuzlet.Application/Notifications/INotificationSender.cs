namespace Szakuzlet.Application.Notifications;

public enum NotificationKind { Sms, Email }

/// <summary>Egy kiküldendő értesítés (SMS vagy e-mail).</summary>
public record NotificationMessage(
    NotificationKind Kind,
    string Recipient,
    string Subject,
    string Body);

/// <summary>
/// SMS/e-mail kiküldés absztrakciója. A valós szolgáltató (SMS gateway, SMTP,
/// marketing-szoftver) elkészültéig egy naplózó/tároló mock implementáció szolgálja ki.
/// </summary>
public interface INotificationSender
{
    Task SendAsync(NotificationMessage message, CancellationToken ct = default);
}
