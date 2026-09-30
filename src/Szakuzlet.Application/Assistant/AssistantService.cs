using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Application.Assistant;

/// <summary>A Digitális Asszisztens munkalistája (prioritásalapú áttekintés).</summary>
public record WorkflowOverview(
    int ExpressWaiting,
    int PostalAwaitingPayment,
    int PostalAwaitingSignature,
    int ContractsAwaitingSignature,
    int OutOfStockExpress);

/// <summary>Próbakezelési vezetői riport egy időszakra.</summary>
public record TrialReport(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int Started,
    int Closed,
    int Rejected,
    int Extended,
    double RejectionRate);

/// <summary>
/// KVL Digitális Asszisztens (háttér-workflow) és automata pénzügyi-vezetői riporting (III. Modul F).
/// A meglévő adatszigetekből prioritásos munkalistát és összesített vezetői riportot állít elő –
/// kiváltva a manuális Excel-táblák vezetését.
/// </summary>
public sealed class AssistantService
{
    private readonly IAppDbContext _db;

    public AssistantService(IAppDbContext db) => _db = db;

    /// <summary>A folyamatban lévő ügyek prioritásalapú áttekintése (a pultosok napi munkalistája).</summary>
    public async Task<WorkflowOverview> GetWorkflowAsync(CancellationToken ct = default)
    {
        var expressWaiting = await _db.ExpressIntakes
            .CountAsync(x => x.Status == ExpressIntakeStatus.ExpresszKiszolgalasraVar, ct);
        var outOfStock = await _db.ExpressIntakes
            .CountAsync(x => x.Status == ExpressIntakeStatus.BeszerzesreVar, ct);

        var postalPayment = await _db.PostalTrials
            .CountAsync(x => x.Status == PostalTrialStatus.FizetesreVar, ct);
        var postalSignature = await _db.PostalTrials
            .CountAsync(x => x.Status == PostalTrialStatus.AlairasraVar, ct);

        var contractsSignature = await _db.Contracts
            .CountAsync(x => x.Status == ContractStatus.Elokeszitve, ct);

        return new WorkflowOverview(expressWaiting, postalPayment, postalSignature,
            contractsSignature, outOfStock);
    }

    /// <summary>
    /// Vezetői próbakezelési riport egy időszakra: megkezdett és lezárt próbák, elutasítások és
    /// hosszabbítások száma, valamint az elutasítási arány. (Az Excel-vezetés kiváltása.)
    /// </summary>
    public async Task<TrialReport> GetTrialReportAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc,
        CancellationToken ct = default)
    {
        // Megkezdett próbák: az időszakban létrehozott szerződések.
        var started = await _db.Contracts
            .CountAsync(c => c.CreatedAtUtc >= fromUtc && c.CreatedAtUtc < toUtc, ct);

        // A lezárási kimeneteleket az audit-naplóból olvassuk ki (a TrialClosureService írja).
        var closureAudits = await _db.AuditLog
            .Where(a => a.EntityType == "Contract"
                        && a.TimestampUtc >= fromUtc && a.TimestampUtc < toUtc
                        && (a.Action == "ProbaLezarva_Sikeres"
                            || a.Action == "ProbaLezarva_Elutasitva"
                            || a.Action == "ProbaHosszabbitva"))
            .ToListAsync(ct);

        var rejected = closureAudits.Count(a => a.Action == "ProbaLezarva_Elutasitva");
        var successful = closureAudits.Count(a => a.Action == "ProbaLezarva_Sikeres");
        var extended = closureAudits.Count(a => a.Action == "ProbaHosszabbitva");
        var closed = rejected + successful;

        var rejectionRate = closed == 0 ? 0 : Math.Round(rejected * 100.0 / closed, 1);

        return new TrialReport(fromUtc, toUtc, started, closed, rejected, extended, rejectionRate);
    }
}
