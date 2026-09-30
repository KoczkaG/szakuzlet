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

    /// <summary>Vevő-kategória (adónem). Az átváltás nem törli a beteg törzsadatait.</summary>
    public BillingCategory Category { get; set; } = BillingCategory.Maganszemely;

    /// <summary>Fizetési mód (a zárási logikai szűrő ezt veti össze a tételekkel).</summary>
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Keszpenz;

    /// <summary>
    /// Céges/EP vevő-adatok elkülönített blokkban. Null magánszemélynél. Owned entity –
    /// a beteg profilját nem befolyásolja.
    /// </summary>
    public BillingParty? BillingParty { get; set; }

    // --- Egészségpénztári (EP) mezők (II. Modul B) ---
    public string? EpName { get; set; }
    public string? EpMemberId { get; set; }
    public string? EpBeneficiaryName { get; set; }

    // --- Vényes/e-számla (II. Modul C) ---
    /// <summary>Vényes értékesítés-e (ekkor kap EAN-kódot a poolból).</summary>
    public bool IsPrescription { get; set; }

    /// <summary>A ráégetett hatósági EAN-sorszám (vényesnél).</summary>
    public string? AssignedEanCode { get; set; }

    /// <summary>A hiteles e-számla külső azonosítója (Számlázz.hu mock).</summary>
    public string? EInvoiceReference { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Parkoltatás esetén: mikor jár le az 1 hónapos időzítő.</summary>
    public DateTimeOffset? ParkingExpiresAtUtc { get; set; }

    public DateTimeOffset? SentAtUtc { get; set; }

    /// <summary>A számla tételei (termék/modellnév a Timeline-hoz).</summary>
    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
}
