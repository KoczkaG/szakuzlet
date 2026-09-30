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

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
