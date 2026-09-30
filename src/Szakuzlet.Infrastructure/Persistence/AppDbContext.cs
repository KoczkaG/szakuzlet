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
    public DbSet<TimelineEvent> TimelineEvents => Set<TimelineEvent>();
    public DbSet<PatientTask> Tasks => Set<PatientTask>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Order> Orders => Set<Order>();

    Task<int> IAppDbContext.SaveChangesAsync(CancellationToken ct) => base.SaveChangesAsync(ct);

    protected override void ConfigureConventions(ModelConfigurationBuilder cb)
    {
        // SQLite nem tud DateTimeOffset-et rendezni/összehasonlítani natívan.
        // Fejlesztői SQLite alatt long (Unix ms) formában tároljuk; PostgreSQL natívan kezeli.
        if (Database.IsSqlite())
        {
            cb.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToLongConverter>();
            cb.Properties<DateTimeOffset?>().HaveConversion<NullableDateTimeOffsetToLongConverter>();
        }
    }

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

        b.Entity<TimelineEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.EventType).IsRequired().HasMaxLength(100);
            e.Property(x => x.Summary).IsRequired().HasMaxLength(1000);
            e.HasOne(x => x.Patient).WithMany(p => p.TimelineEvents)
                .HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.PatientId, x.OccurredAtUtc });
        });

        b.Entity<PatientTask>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired().HasMaxLength(200);
            e.Property(x => x.Details).HasMaxLength(2000);
            e.HasOne(x => x.Patient).WithMany(p => p.Tasks)
                .HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.Status);
        });

        b.Entity<Invoice>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Number).IsRequired().HasMaxLength(50);
            e.HasOne(x => x.Patient).WithMany(p => p.Invoices)
                .HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.Status);
        });

        b.Entity<Order>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Number).IsRequired().HasMaxLength(50);
            e.Property(x => x.DataCompletionToken).HasMaxLength(128);
            e.HasOne(x => x.Patient).WithMany(p => p.Orders)
                .HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.DataCompletionToken);
            e.HasIndex(x => x.Status);
        });
    }
}
