using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Application.Dashboard;

public record OpenTaskView(Guid Id, string PatientName, string Title, string? Details, DateTimeOffset CreatedAtUtc);
public record ParkedInvoiceView(Guid Id, string PatientName, string Number, DateTimeOffset? ParkingExpiresAtUtc);
public record PendingOrderView(Guid Id, string PatientName, string Number, DateTimeOffset CreatedAtUtc);

public record DashboardData(
    IReadOnlyList<OpenTaskView> OpenTasks,
    IReadOnlyList<ParkedInvoiceView> ParkedInvoices,
    IReadOnlyList<PendingOrderView> PendingOrders);

/// <summary>A pulti áttekintő (Dashboard) adatait szolgáltatja.</summary>
public sealed class DashboardService
{
    private readonly IAppDbContext _db;

    public DashboardService(IAppDbContext db) => _db = db;

    public async Task<DashboardData> GetAsync(CancellationToken ct = default)
    {
        var tasks = await _db.Tasks
            .Where(t => t.Status == PatientTaskStatus.Nyitott)
            .Select(t => new OpenTaskView(t.Id, t.Patient.FullName, t.Title, t.Details, t.CreatedAtUtc))
            .ToListAsync(ct);

        var parked = await _db.Invoices
            .Where(i => i.Status == InvoiceStatus.Parkoltatva)
            .Select(i => new ParkedInvoiceView(i.Id, i.Patient.FullName, i.Number, i.ParkingExpiresAtUtc))
            .ToListAsync(ct);

        var orders = await _db.Orders
            .Where(o => o.Status == OrderStatus.AdatpotlasraVar)
            .Select(o => new PendingOrderView(o.Id, o.Patient.FullName, o.Number, o.CreatedAtUtc))
            .ToListAsync(ct);

        return new DashboardData(tasks, parked, orders);
    }
}
