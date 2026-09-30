namespace Szakuzlet.Domain.Entities;

/// <summary>
/// Egy hatósági EAN-sorszám a virtuális matrica-poolban (II. Modul C). A fizikai
/// matricatekercsek digitális kiváltása: a hatóságtól kapott sorszámok tömbje, amelyekből
/// vényes értékesítéskor a rendszer automatikusan kioszt egyet, és ráégeti a PDF számlára.
/// </summary>
public class EanCode
{
    public Guid Id { get; set; }

    /// <summary>A hatósági EAN-sorszám (egyedi).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Felhasználva-e (kiosztották egy számlához).</summary>
    public bool Used { get; set; }

    /// <summary>A számla, amelyhez kiosztották (ha felhasznált).</summary>
    public Guid? AssignedToInvoiceId { get; set; }

    public DateTimeOffset? AssignedAtUtc { get; set; }

    public DateTimeOffset ImportedAtUtc { get; set; }
}
