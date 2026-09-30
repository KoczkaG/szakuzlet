namespace Szakuzlet.Domain.Enums;

/// <summary>A számla kiküldési állapota a „Zéró hozzájárulás” radar szempontjából.</summary>
public enum InvoiceStatus
{
    /// <summary>Kiküldhető és/vagy kiküldve (van érvényes csatorna-hozzájárulás).</summary>
    Kikuldheto = 0,

    /// <summary>
    /// Parkoltatva: sem postai, sem e-mailes hozzájárulás nincs. A rendszer nem küldi ki
    /// automatikusan, digitálisan „parkolva” tartja 1 hónapos időzítővel.
    /// </summary>
    Parkoltatva = 1,

    /// <summary>A parkoltatás lezárult (időzítő lejárt vagy egyeztetés megtörtént).</summary>
    ParkoltatasLezarva = 2,

    /// <summary>Kiküldve a betegnek.</summary>
    Kikuldve = 3
}
