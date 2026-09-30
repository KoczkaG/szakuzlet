using Microsoft.EntityFrameworkCore;
using Szakuzlet.Domain.Entities;

namespace Szakuzlet.Application.Abstractions;

/// <summary>
/// Az adatelérés absztrakciója. Az Application réteg ezen keresztül dolgozik,
/// így nem függ közvetlenül a konkrét EF Core / PostgreSQL implementációtól.
/// </summary>
public interface IAppDbContext
{
    DbSet<Patient> Patients { get; }
    DbSet<DataSheet> DataSheets { get; }
    DbSet<ConsentRecord> Consents { get; }
    DbSet<AuditLogEntry> AuditLog { get; }
    DbSet<TimelineEvent> TimelineEvents { get; }
    DbSet<PatientTask> Tasks { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<Order> Orders { get; }
    DbSet<BusinessHour> BusinessHours { get; }
    DbSet<CalendarOverride> CalendarOverrides { get; }
    DbSet<CallRecord> Calls { get; }
    DbSet<CallbackRequest> Callbacks { get; }
    DbSet<PatientPhone> Phones { get; }
    DbSet<InvoiceLine> InvoiceLines { get; }
    DbSet<Shipment> Shipments { get; }
    DbSet<PhilipsReplacement> PhilipsReplacements { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
