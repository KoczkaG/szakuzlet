namespace Szakuzlet.Domain.Enums;

/// <summary>
/// Az adatlap kitöltésének csatornája (I. Modul A, feladatlista 1-2. pont).
/// </summary>
public enum DataSheetChannel
{
    /// <summary>Távoli, otthoni kitöltés e-mail/SMS linken keresztül (páciens-portál).</summary>
    TavoliOnline = 0,

    /// <summary>Helyszíni, pulti tabletes/ügyféloldali monitoros kitöltés.</summary>
    HelysziniTablet = 1
}
