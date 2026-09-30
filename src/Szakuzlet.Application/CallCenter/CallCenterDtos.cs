using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Application.CallCenter;

/// <summary>
/// Bejövő hívás kezdeti feldolgozásának eredménye – ezt adja vissza a telefonközpontnak
/// a rendszer, amikor a hívás beérkezik (a zsilip + CRM-találat alapján).
/// </summary>
public record IncomingCallResult(
    Guid CallId,
    /// <summary>Nyitvatartási időn belül érkezett-e (zsilip döntése).</summary>
    bool WithinOpeningHours,
    /// <summary>Van-e CRM-találat a hívószámra vagy megadott adatra (A/B ág).</summary>
    bool CrmMatch,
    /// <summary>A beazonosított beteg azonosítója, ha van találat.</summary>
    Guid? PatientId,
    /// <summary>A beteg neve (a pultos képernyőjére), ha van találat.</summary>
    string? PatientName,
    /// <summary>Régi (menürendszert nem használó) beteg-e – szóbeli adatfrissítés kötelező.</summary>
    bool IsLegacyPatient);

/// <summary>Kezelő döntése a hangrögzítésről (9-es gomb / „Hangrögzítés leállítása”).</summary>
public record RecordingDecision(RecordingState State, string Actor, string Reason);
