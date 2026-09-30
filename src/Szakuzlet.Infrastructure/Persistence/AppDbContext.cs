using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Domain.Entities;

namespace Szakuzlet.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<DataSheet> DataSheets => Set<DataSheet>();
    public DbSet<ConsentRecord> Consents => Set<ConsentRecord>();
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();

    Task<int> IAppDbContext.SaveChangesAsync(CancellationToken ct) => base.SaveChangesAsync(ct);

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Patient>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FullName).IsRequired().HasMaxLength(200);
            e.Property(x => x.KvlPartnerCode).HasMaxLength(50);
            e.Property(x => x.TajNumber).HasMaxLength(20);
            e.Property(x => x.MobilePhone).HasMaxLength(30);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.PostalCode).HasMaxLength(10);
            e.Property(x => x.City).HasMaxLength(120);
            e.Property(x => x.AddressLine).HasMaxLength(300);
            e.HasIndex(x => x.KvlPartnerCode);
            e.HasIndex(x => x.DateOfBirth);
            e.Ignore(x => x.HasMissingRequiredContact);
        });

        b.Entity<DataSheet>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.AccessToken).HasMaxLength(128);
            e.Property(x => x.SubmittedFromIp).HasMaxLength(64);
            e.Property(x => x.SubmittedUserAgent).HasMaxLength(512);
            e.HasIndex(x => x.AccessToken).IsUnique();
            e.HasOne(x => x.Patient)
                .WithMany(p => p.DataSheets)
                .HasForeignKey(x => x.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ConsentRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Patient)
                .WithMany(p => p.Consents)
                .HasForeignKey(x => x.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.DataSheet)
                .WithMany(d => d.Consents)
                .HasForeignKey(x => x.DataSheetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AuditLogEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.EntityType).IsRequired().HasMaxLength(100);
            e.Property(x => x.EntityId).IsRequired().HasMaxLength(64);
            e.Property(x => x.Action).IsRequired().HasMaxLength(100);
            e.Property(x => x.Actor).IsRequired().HasMaxLength(120);
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
        });
    }
}
