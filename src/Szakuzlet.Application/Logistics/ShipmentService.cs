using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Application.Common;
using Szakuzlet.Domain.Entities;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Application.Logistics;

/// <summary>
/// Csomagok kezelése és élő futárszolgálati szinkron (I. Modul D, 2. pont).
/// A csomagstátuszok a futár-API-ból frissülnek, és a beteg Idővonalán jelennek meg.
/// </summary>
public sealed class ShipmentService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly EventRecorder _events;
    private readonly ICourierClient _courier;

    public ShipmentService(IAppDbContext db, IClock clock, EventRecorder events, ICourierClient courier)
    {
        _db = db;
        _clock = clock;
        _events = events;
        _courier = courier;
    }

    /// <summary>Csomag rögzítése (feladás).</summary>
    public async Task<Shipment> CreateAsync(Guid patientId, Courier courier, string trackingNumber,
        CancellationToken ct = default)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen páciens: {patientId}");

        var now = _clock.UtcNow;
        var shipment = new Shipment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Courier = courier,
            TrackingNumber = trackingNumber,
            Status = ShipmentStatus.Feladva,
            CreatedAtUtc = now,
            StatusUpdatedAtUtc = now
        };
        _db.Shipments.Add(shipment);
        _events.Timeline(patient.Id, "CsomagFeladva",
            $"Csomag feladva ({courier} {trackingNumber}).");
        await _db.SaveChangesAsync(ct);
        return shipment;
    }

    /// <summary>
    /// A nyitott (még nem átvett) csomagok státuszának frissítése a futár-API-ból.
    /// Státuszváltáskor Timeline-esemény keletkezik. Visszaadja a frissített csomagok számát.
    /// (Ütemezett háttérfolyamat és/vagy pulti frissítés hívja.)
    /// </summary>
    public async Task<int> SyncOpenShipmentsAsync(CancellationToken ct = default)
    {
        var open = await _db.Shipments
            .Where(s => s.Status != ShipmentStatus.Atveve)
            .ToListAsync(ct);

        var changed = 0;
        foreach (var s in open)
        {
            var status = await _courier.GetStatusAsync(s.TrackingNumber, ct);
            if (status is null || status.Status == s.Status) continue;

            s.Status = status.Status;
            s.ReceivedBy = status.ReceivedBy;
            s.StatusUpdatedAtUtc = status.UpdatedAtUtc;

            _events.Timeline(s.PatientId, "CsomagStatusz",
                $"Csomag {s.TrackingNumber}: {status.Status}" +
                (status.ReceivedBy is not null ? $" (átvette: {status.ReceivedBy})" : "") + ".");
            changed++;
        }

        if (changed > 0)
            await _db.SaveChangesAsync(ct);
        return changed;
    }
}
