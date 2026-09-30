namespace Szakuzlet.Domain.Enums;

/// <summary>Postai megrendelés állapota az adatpótlási protokoll szerint (I. Modul A, 10-11. pont).</summary>
public enum OrderStatus
{
    /// <summary>Függőben lévő / Adatpótlásra váró: hiányzik a kötelező kontaktadat.</summary>
    AdatpotlasraVar = 0,

    /// <summary>Kiszolgálható: az adatpótlás megtörtént, mehet a számlázás/postázás.</summary>
    Kiszolgalhato = 1,

    /// <summary>Lezárva: a csomag lezárva, rendszerüzenet + számla kiküldve.</summary>
    Lezarva = 2
}
