namespace Szakuzlet.Domain.Entities;

/// <summary>
/// A beteg oktatási igazolása (III. Modul E). Törölhetetlen, időbélyeges bizonyíték arról, hogy
/// a beteg a készülék/maszk használatára és tisztítására vonatkozó vizuális oktatóanyagot
/// megtekintette és megértette – ez váltja ki (auditálhatóan) a személyes betanítást. Alternatíva:
/// a régi (rutinos) beteg nyilatkozata, hogy betanításra nem tart igényt.
/// </summary>
public class EducationAcknowledgement
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    /// <summary>A kapcsolódó szerződés (opcionális).</summary>
    public Guid? ContractId { get; set; }

    /// <summary>Igaz: a videók megtekintve+megértve. Hamis: régi beteg lemondott a betanításról.</summary>
    public bool VideosAcknowledged { get; set; }

    /// <summary>Régi beteg nyilatkozata: nem tart igényt betanításra (rutinszerűen ismeri).</summary>
    public bool RoutineUserWaiver { get; set; }

    /// <summary>Mely termékekhez tartozott az oktatás (rögzítéshez).</summary>
    public string Products { get; set; } = string.Empty;

    public DateTimeOffset AcknowledgedAtUtc { get; set; }
    public string? FromIp { get; set; }
}
