namespace Szakuzlet.Domain.Enums;

/// <summary>A postai úton indított próbakezelés állapota (III. Modul C).</summary>
public enum PostalTrialStatus
{
    /// <summary>Igény befogadva, jóváhagyásra/adategyeztetésre vár.</summary>
    Igenyelve = 0,

    /// <summary>Jóváhagyva, előre utalásra vár (banki szinkron figyeli).</summary>
    FizetesreVar = 1,

    /// <summary>Fizetés beérkezett, digitális szerződés-aláírásra vár.</summary>
    AlairasraVar = 2,

    /// <summary>Aláírva – a csomag feladható (raktári zár feloldva).</summary>
    Csomagolhato = 3,

    /// <summary>Kiszállítás alatt (futárnak átadva).</summary>
    KiszallitasAlatt = 4,

    /// <summary>Kézbesítve – a próbaidőszak számlálója elindult.</summary>
    Kezbesitve = 5,

    /// <summary>Lezárva.</summary>
    Lezarva = 6
}
