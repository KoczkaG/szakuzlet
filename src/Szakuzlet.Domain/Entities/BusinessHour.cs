namespace Szakuzlet.Domain.Entities;

/// <summary>
/// Egy hét egy napjának alapértelmezett nyitvatartása. A nyitás hétköznap fixen 8:00
/// (a spec szerint), de az entitás rugalmas. A zárt napokat (szombat/vasárnap)
/// az <see cref="IsClosed"/> jelöli.
/// </summary>
public class BusinessHour
{
    public Guid Id { get; set; }

    public DayOfWeek Day { get; set; }

    /// <summary>Zárva van-e ezen a napon alapértelmezetten (pl. hétvége).</summary>
    public bool IsClosed { get; set; }

    /// <summary>Nyitás időpontja (helyi idő). Null, ha zárva.</summary>
    public TimeOnly? OpensAt { get; set; }

    /// <summary>Zárás időpontja (helyi idő). Null, ha zárva.</summary>
    public TimeOnly? ClosesAt { get; set; }
}
