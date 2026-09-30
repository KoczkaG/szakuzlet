using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Domain.Entities;

/// <summary>
/// Postai megrendelés. A rögzítéskor, ha a kötelező kontaktadatok hiányosak, a rendszer
/// letiltja az azonnali lezárást/számlázást és „Adatpótlásra váró” státuszba teszi.
/// (I. Modul A, feladatlista 10-11. pont)
/// </summary>
public class Order
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    /// <summary>Megrendelés azonosító / sorszám.</summary>
    public string Number { get; set; } = string.Empty;

    public OrderStatus Status { get; set; }

    /// <summary>Adatpótláshoz kiküldött egyedi, biztonságos token (SMS/e-mail link).</summary>
    public string? DataCompletionToken { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
}
