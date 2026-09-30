using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Domain.Entities;

/// <summary>
/// Egy telefonhívás rendszerszintű bejegyzése. A telefonközpont (külső VoIP) eseményei
/// alapján jön létre és frissül; a hívásfájl a beteg Idővonalával linkelhető.
/// </summary>
public class CallRecord
{
    public Guid Id { get; set; }

    public CallDirection Direction { get; set; }

    /// <summary>A hívó fél telefonszáma (bejövőnél), illetve a hívott szám (kimenőnél).</summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Beazonosított beteg (ha a CRM-találat sikeres). Null, ha ismeretlen.</summary>
    public Guid? PatientId { get; set; }
    public Patient? Patient { get; set; }

    /// <summary>Melyik IVR menüpontból érkezik (pl. "3. Szerviz"). Csak jelzés a pultosnak.</summary>
    public string? IvrMenu { get; set; }

    public RecordingState Recording { get; set; } = RecordingState.Rogzitve;

    /// <summary>A rögzített hangfájl külső azonosítója/URL-je (a telefonközpont adja).</summary>
    public string? RecordingReference { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? EndedAtUtc { get; set; }

    /// <summary>Igaz, ha a hívást fogadták; hamis, ha nem fogadott (önürítő listára kerül).</summary>
    public bool Answered { get; set; }
}
