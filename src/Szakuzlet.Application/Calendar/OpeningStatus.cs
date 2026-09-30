namespace Szakuzlet.Application.Calendar;

/// <summary>
/// Egy adott időpontra vonatkozó nyitvatartási állapot – ezt kérdezi le az IVR időzítő-zsilip,
/// a webshop és a Google szinkron.
/// </summary>
public record OpeningStatus(
    bool IsOpen,
    /// <summary>A vizsgált napra vonatkozó címke, ha van felülírás (pl. „Húsvéthétfő”).</summary>
    string? OverrideLabel,
    /// <summary>A következő nyitás időpontja (helyi idő), ha jelenleg zárva van.</summary>
    DateTimeOffset? NextOpenAtLocal);
