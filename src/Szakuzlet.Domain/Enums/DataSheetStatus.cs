namespace Szakuzlet.Domain.Enums;

/// <summary>
/// A digitális adatlap-kitöltési folyamat állapota.
/// </summary>
public enum DataSheetStatus
{
    /// <summary>Létrehozva, kiküldve, de a páciens még nem töltötte ki.</summary>
    Kikuldve = 0,

    /// <summary>A páciens elkezdte, de még nem véglegesítette.</summary>
    Folyamatban = 1,

    /// <summary>A páciens beküldte és jogilag jóváhagyta (időbélyeg + IP rögzítve).</summary>
    Vegleges = 2,

    /// <summary>Lejárt kitöltés nélkül.</summary>
    Lejart = 3
}
