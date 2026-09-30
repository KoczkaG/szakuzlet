using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Domain.Entities;

/// <summary>
/// Hívásvégi jegyzet (I. Modul E). A hívás lezárásakor kötelezően felugró, strukturált
/// adatbeviteli ablak eredménye. A jegyzet a beteg Idővonalára is beíródik, és visszahívási
/// igény esetén automata feladatot generál.
/// </summary>
public class CallNote
{
    public Guid Id { get; set; }

    public Guid CallId { get; set; }
    public CallRecord Call { get; set; } = null!;

    /// <summary>A hívott/hívó beteg (ha beazonosított).</summary>
    public Guid? PatientId { get; set; }
    public Patient? Patient { get; set; }

    /// <summary>A megjelölt témakör(ök) (több is lehet).</summary>
    public CallTopic Topics { get; set; }

    /// <summary>A küldő intézmény / alváslabor (opcionális, statisztikához).</summary>
    public Guid? ReferralSourceId { get; set; }
    public ReferralSource? ReferralSource { get; set; }

    /// <summary>A beszélgetés lényegének kötelező szöveges összefoglalása.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Igényelt-e visszahívást / teendőt (automata feladatgenerálás alapja).</summary>
    public bool FollowUpRequired { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
