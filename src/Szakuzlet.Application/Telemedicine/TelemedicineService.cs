using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Application.Common;
using Szakuzlet.Domain.Entities;

namespace Szakuzlet.Application.Telemedicine;

/// <summary>
/// Jogi védőháló a NEAK postai kiszállításokhoz – telemedicina és digitális oktatás-követés
/// (III. Modul E). A személyes betanítást a dinamikus, termékalapú oktatóvideó-megtekintés
/// váltja ki, auditálható, időbélyeges igazolással. Régi (rutinos) beteg nyilatkozhat, hogy
/// betanításra nem tart igényt.
/// </summary>
public sealed class TelemedicineService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly EventRecorder _events;

    public TelemedicineService(IAppDbContext db, IClock clock, EventRecorder events)
    {
        _db = db;
        _clock = clock;
        _events = events;
    }

    /// <summary>
    /// A betegnek kiküldendő oktatóvideók összeválogatása a szerződés termékei alapján:
    /// a konkrét készülék- és maszkmodellhez tartozó videók, plusz az általános (közös) videók.
    /// </summary>
    public async Task<IReadOnlyList<EducationVideo>> GetVideosForProductsAsync(
        IEnumerable<string> productModels, CancellationToken ct = default)
    {
        var models = productModels
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => m.Trim())
            .ToList();

        var all = await _db.EducationVideos.Where(v => v.IsActive).ToListAsync(ct);
        return all
            .Where(v => v.IsGeneric || models.Any(m => string.Equals(m, v.ProductModel, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(v => !v.IsGeneric) // előbb a termékspecifikusak
            .ThenBy(v => v.Title)
            .ToList();
    }

    /// <summary>
    /// A videók megtekintésének és megértésének jogi leokéztatása. Törölhetetlen, időbélyeges
    /// igazolást rögzít a beteg Idővonalán – ez a hatóság felé bizonyíték a betanításról.
    /// </summary>
    public async Task<EducationAcknowledgement> AcknowledgeVideosAsync(Guid patientId, Guid? contractId,
        IEnumerable<string> products, string? ip, CancellationToken ct = default)
    {
        return await RecordAsync(patientId, contractId, products, ip, videos: true, waiver: false,
            audit: ack =>
            {
                _events.Audit("EducationAcknowledgement", ack.Id.ToString(), "OktatovideoLeokezve",
                    ack.Products, "PACIENS", ip);
                _events.Timeline(patientId, "OktatasIgazolva",
                    "A beteg az oktatóvideókat megtekintette és megértette (időbélyeges igazolás).");
            }, ct);
    }

    /// <summary>
    /// Régi (rutinos) beteg nyilatkozata: a betanításra nem tart igényt, a használatot ismeri.
    /// Ez jogilag mentesíti a NEAK-rendelet betanítási kötelezettsége alól.
    /// </summary>
    public async Task<EducationAcknowledgement> WaiveAsRoutineUserAsync(Guid patientId, Guid? contractId,
        string? ip, CancellationToken ct = default)
    {
        return await RecordAsync(patientId, contractId, Array.Empty<string>(), ip, videos: false, waiver: true,
            audit: ack =>
            {
                _events.Audit("EducationAcknowledgement", ack.Id.ToString(), "BetanitasLemondva_Rutinos",
                    "A beteg nyilatkozott: rutinszerűen ismeri, betanításra nem tart igényt.", "PACIENS", ip);
                _events.Timeline(patientId, "OktatasLemondva",
                    "Régi beteg nyilatkozata: betanításra nem tart igényt (rutinos használó).");
            }, ct);
    }

    private async Task<EducationAcknowledgement> RecordAsync(Guid patientId, Guid? contractId,
        IEnumerable<string> products, string? ip, bool videos, bool waiver,
        Action<EducationAcknowledgement> audit, CancellationToken ct)
    {
        _ = await _db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen páciens: {patientId}");

        var ack = new EducationAcknowledgement
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            ContractId = contractId,
            VideosAcknowledged = videos,
            RoutineUserWaiver = waiver,
            Products = string.Join(", ", products),
            AcknowledgedAtUtc = _clock.UtcNow,
            FromIp = ip
        };
        _db.EducationAcknowledgements.Add(ack);
        audit(ack); // audit + timeline a mentés ELŐTT
        await _db.SaveChangesAsync(ct);
        return ack;
    }
}
