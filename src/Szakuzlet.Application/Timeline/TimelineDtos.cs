namespace Szakuzlet.Application.Timeline;

/// <summary>Egy egységes idővonal-tétel a beteg adatlapján (görgethető nézet).</summary>
public record TimelineItem(
    DateTimeOffset OccurredAtUtc,
    /// <summary>Forrás/kategória (pl. „Számla”, „Csomag”, „Hívás”, „Esemény”).</summary>
    string Category,
    string Title,
    string? Detail);

/// <summary>
/// A Philips-csereprojekt piros riasztásának adata. Null, ha a beteg nem érintett.
/// A pultos a beteg megnyitásakor kapja meg (I. Modul D, 3. pont).
/// </summary>
public record PhilipsAlert(string Model, string SerialNumber, DateOnly? ReplacedOn);

/// <summary>A beteg teljes idővonala + a megnyitáskori riasztás.</summary>
public record PatientTimeline(
    Guid PatientId,
    string PatientName,
    PhilipsAlert? PhilipsAlert,
    IReadOnlyList<TimelineItem> Items);
