using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Domain.Entities;

/// <summary>
/// Egy kiállított számla (vagy egyéb kiküldendő dokumentum) a „Zéró hozzájárulás” és
/// számla-parkoltatási radar szempontjából (I. Modul A, feladatlista 8. pont).
/// </summary>
public class Invoice
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    /// <summary>Számla azonosító / sorszám (üzleti hivatkozás).</summary>
    public string Number { get; set; } = string.Empty;

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Kikuldheto;

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Parkoltatás esetén: mikor jár le az 1 hónapos időzítő.</summary>
    public DateTimeOffset? ParkingExpiresAtUtc { get; set; }

    public DateTimeOffset? SentAtUtc { get; set; }

    /// <summary>A számla tételei (termék/modellnév a Timeline-hoz).</summary>
    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
}
