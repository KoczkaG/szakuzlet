namespace Szakuzlet.Domain.Entities;

/// <summary>
/// Egy számla tételsora. A Timeline kritikus elvárása, hogy ne csak a számla sorszáma,
/// hanem a konkrét termék- és modellnév is közvetlenül olvasható legyen (I. Modul D, 1. pont),
/// kiváltva a számlák egyesével történő megnyitogatását.
/// </summary>
public class InvoiceLine
{
    public Guid Id { get; set; }

    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    /// <summary>Cikkszám (opcionális belső azonosító).</summary>
    public string? ItemCode { get; set; }

    /// <summary>A termék / modell megnevezése (ez jelenik meg a Timeline-on).</summary>
    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;
}
