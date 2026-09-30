using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Domain.Entities;

/// <summary>
/// Egy visszahívási igény az önürítő listán. A rendszer automatikusan törli (teljesítettre
/// állítja), ha a számot időközben elérték vagy a beteg maga hívott vissza.
/// </summary>
public class CallbackRequest
{
    public Guid Id { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public Guid? PatientId { get; set; }
    public Patient? Patient { get; set; }

    public CallbackKind Kind { get; set; }
    public CallbackStatus Status { get; set; } = CallbackStatus.Nyitott;

    /// <summary>Mikorra időzítve keletkezik a feladat (pl. munkaidőn kívülinél a következő munkanap 9:00).</summary>
    public DateTimeOffset DueAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ResolvedAtUtc { get; set; }

    /// <summary>Hogyan zárult (pl. "Sikeres visszahívás", "Időközben elértük", "Beteg visszahívott").</summary>
    public string? Resolution { get; set; }
}
