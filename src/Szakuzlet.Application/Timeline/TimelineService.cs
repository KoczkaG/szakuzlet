using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Application.Timeline;

/// <summary>
/// Központi Ügyféltörténet Idővonal (I. Modul D). Egyetlen, egységes, időrendi nézetbe fésüli
/// össze a korábban elszigetelt adatszigeteket: pénzügyi számlák (konkrét termék/modellnévvel),
/// raktári/logisztikai csomagstátuszok, valamint a kommunikációs és szerviz-események.
/// Kiszolgálja a Philips-csereprojekt piros riasztását is.
/// </summary>
public sealed class TimelineService
{
    private readonly IAppDbContext _db;

    public TimelineService(IAppDbContext db) => _db = db;

    public async Task<PatientTimeline> GetAsync(Guid patientId, CancellationToken ct = default)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen páciens: {patientId}");

        var items = new List<TimelineItem>();

        // 1) Általános események (adatlap, hívások, parkoltatás stb.) – a TimelineEvent táblából.
        var events = await _db.TimelineEvents
            .Where(t => t.PatientId == patientId)
            .ToListAsync(ct);
        items.AddRange(events.Select(e => new TimelineItem(
            e.OccurredAtUtc, "Esemény", e.Summary, null)));

        // 2) Számlák – a tételek konkrét termék/modellnevével (kritikus elvárás).
        var invoices = await _db.Invoices
            .Where(i => i.PatientId == patientId)
            .Include(i => i.Lines)
            .ToListAsync(ct);
        items.AddRange(invoices.Select(i => new TimelineItem(
            i.CreatedAtUtc, "Számla",
            $"Számla {i.Number}",
            i.Lines.Count > 0
                ? string.Join(", ", i.Lines.Select(l => $"{l.ProductName} ×{l.Quantity}"))
                : "(nincs tétel)")));

        // 3) Csomagok – élő futár-státusszal.
        var shipments = await _db.Shipments
            .Where(s => s.PatientId == patientId)
            .ToListAsync(ct);
        items.AddRange(shipments.Select(s => new TimelineItem(
            s.StatusUpdatedAtUtc, "Csomag",
            $"{s.Courier} {s.TrackingNumber}",
            $"{HuStatus(s.Status)}{(s.ReceivedBy is not null ? $" – átvette: {s.ReceivedBy}" : "")}")));

        var ordered = items
            .OrderByDescending(i => i.OccurredAtUtc)
            .ToList();

        var alert = await GetPhilipsAlertAsync(patientId, ct);

        return new PatientTimeline(patient.Id, patient.FullName, alert, ordered);
    }

    /// <summary>
    /// A Philips-csereprojekt piros riasztás adata a beteg megnyitásakor. Null, ha nem érintett.
    /// </summary>
    public async Task<PhilipsAlert?> GetPhilipsAlertAsync(Guid patientId, CancellationToken ct = default)
    {
        var pr = await _db.PhilipsReplacements.FirstOrDefaultAsync(x => x.PatientId == patientId, ct);
        return pr is null ? null : new PhilipsAlert(pr.ReplacementModel, pr.SerialNumber, pr.ReplacedOn);
    }

    private static string HuStatus(ShipmentStatus s) => s switch
    {
        ShipmentStatus.Feladva => "Feladva",
        ShipmentStatus.KezbesitesAlatt => "Kézbesítés alatt",
        ShipmentStatus.SikertelenKezbesites => "Sikertelen kézbesítés / Nem kereste",
        ShipmentStatus.Atveve => "Átvéve",
        _ => s.ToString()
    };
}
