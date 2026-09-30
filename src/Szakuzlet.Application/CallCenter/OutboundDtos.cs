using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Application.CallCenter;

/// <summary>Egy hívható szám a beteg adatlapján (Click-to-Call gomb mellette).</summary>
public record DialableNumber(Guid PhoneId, PhoneKind Kind, string Number, string? ContactName, bool LegallyApproved);

/// <summary>
/// A kimenő hívás indításának eredménye. Tartalmazza a felugró adatlap-kontextust és a
/// pultos képernyőjén megjelenítendő GDPR emlékeztető sablont.
/// </summary>
public record OutboundCallStarted(
    Guid CallId,
    Guid PatientId,
    string PatientName,
    string DialedNumber,
    PhoneKind DialedKind,
    /// <summary>A kezelő által kötelezően bemondandó GDPR sablon.</summary>
    string GdprPrompt,
    /// <summary>Belső érvkészlet a gyanakvás kezelésére (a képernyőn előhívható).</summary>
    string SuspicionScript);
