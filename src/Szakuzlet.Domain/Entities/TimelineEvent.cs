namespace Szakuzlet.Domain.Entities;

/// <summary>
/// A beteg Ügyféltörténet Idővonalának (History / Timeline) egy eseménye.
/// Kronologikus eseménynapló, amely minden ügyfél-interakciót rögzít.
/// A részletes vizuális/technikai specifikáció az I. Modul D) pontban készül,
/// de az A) pont automatizmusai (parkoltatás lezárása, rendeléslezárás) már ide írnak.
/// </summary>
public class TimelineEvent
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    /// <summary>Az esemény típusa (pl. "AdatlapVeglegesitve", "SzamlaParkoltatva", "RendelesLezarva").</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Emberi olvasható összefoglaló, ami az idővonalon megjelenik.</summary>
    public string Summary { get; set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; set; }
}
