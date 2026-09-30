using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Domain.Entities;

/// <summary>
/// Egy konkrét hozzájárulási válasz a 4 kötelező kérdés egyikére.
/// A hozzájárulás önmagában egy auditálható, időbélyeggel ellátott jogi tény.
/// </summary>
public class ConsentRecord
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    /// <summary>Melyik adatlap-kitöltéskor keletkezett ez a hozzájárulás.</summary>
    public Guid DataSheetId { get; set; }
    public DataSheet DataSheet { get; set; } = null!;

    public ConsentChannel Channel { get; set; }

    /// <summary>Igen (true) vagy Nem (false) válasz.</summary>
    public bool Granted { get; set; }

    /// <summary>A válasz rögzítésének pontos időbélyege.</summary>
    public DateTimeOffset RecordedAtUtc { get; set; }
}
