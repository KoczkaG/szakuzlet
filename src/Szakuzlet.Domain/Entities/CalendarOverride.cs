namespace Szakuzlet.Domain.Entities;

/// <summary>
/// Egy adott naptári napra vonatkozó egyedi felülírás: állami munkaszüneti nap,
/// ledolgozós szombat vagy rendkívüli/ünnepi rövidített nyitvatartás.
/// Ez felülírja az adott napra vonatkozó alapértelmezett <see cref="BusinessHour"/>-t.
/// </summary>
public class CalendarOverride
{
    public Guid Id { get; set; }

    public DateOnly Date { get; set; }

    /// <summary>Rövid megnevezés (pl. "Húsvéthétfő", "Ledolgozós szombat").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Zárva van-e ezen a napon (ünnepnapi zárvatartás).</summary>
    public bool IsClosed { get; set; }

    /// <summary>Rendkívüli nyitás időpontja (ha nem zárt).</summary>
    public TimeOnly? OpensAt { get; set; }

    /// <summary>Rendkívüli zárás időpontja (ha nem zárt).</summary>
    public TimeOnly? ClosesAt { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
