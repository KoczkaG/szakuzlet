namespace Szakuzlet.Domain.Enums;

/// <summary>Az online előkészített (expressz) ügymenet besorolása az előszűrő alapján.</summary>
public enum IntakeClassification
{
    /// <summary>NEAK-támogatott próbakezelés (szerződéskötés).</summary>
    NeakTamogatottProbakezeles = 0,

    /// <summary>Magánellátásos, teljes áras vásárlás.</summary>
    MaganellatasTeljesAr = 1
}

/// <summary>Az online előkészített ügymenet státusza.</summary>
public enum ExpressIntakeStatus
{
    /// <summary>Beérkezett, feldolgozásra/összekészítésre vár.</summary>
    Beerkezett = 0,

    /// <summary>Expressz kiszolgálásra vár (összekészítve, a beteg jöhet).</summary>
    ExpresszKiszolgalasraVar = 1,

    /// <summary>Készlethiány miatt beszerzésre vár (a beteg ne induljon el).</summary>
    BeszerzesreVar = 2,

    /// <summary>Sikeresen átvéve (lezárva).</summary>
    Atveve = 3,

    /// <summary>Az expressz sáv megszakadt (variálás/maszkpróba) – normál sorba került.</summary>
    NormalSorbaKerult = 4
}

/// <summary>Az igénylő szerepe az online előkészítő felületen.</summary>
public enum RequesterRole
{
    Beteg = 0,
    Hozzatartozo = 1
}
