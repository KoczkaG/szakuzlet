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
    public DbSet<BusinessHour> BusinessHours => Set<BusinessHour>();
    public DbSet<CalendarOverride> CalendarOverrides => Set<CalendarOverride>();
    public DbSet<CallRecord> Calls => Set<CallRecord>();
    public DbSet<CallbackRequest> Callbacks => Set<CallbackRequest>();
    public DbSet<PatientPhone> Phones => Set<PatientPhone>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<PhilipsReplacement> PhilipsReplacements => Set<PhilipsReplacement>();
    public DbSet<CallNote> CallNotes => Set<CallNote>();
    public DbSet<ReferralSource> ReferralSources => Set<ReferralSource>();
    public DbSet<DataCompletionRequest> DataCompletionRequests => Set<DataCompletionRequest>();
    public DbSet<HealthFund> HealthFunds => Set<HealthFund>();
    public DbSet<EanCode> EanCodes => Set<EanCode>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ExpressIntake> ExpressIntakes => Set<ExpressIntake>();
    public DbSet<PostalTrial> PostalTrials => Set<PostalTrial>();

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
            e.Property(x => x.EpName).HasMaxLength(200);
            e.Property(x => x.EpMemberId).HasMaxLength(60);
            e.Property(x => x.EpBeneficiaryName).HasMaxLength(200);
            e.Property(x => x.AssignedEanCode).HasMaxLength(60);
            e.Property(x => x.EInvoiceReference).HasMaxLength(100);
            e.HasOne(x => x.Patient).WithMany(p => p.Invoices)
                .HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.Status);
            // Duplikáció-védelem: a számla sorszáma egyedi (II. Modul A – fantomszámlák ellen).
            e.HasIndex(x => x.Number).IsUnique();
            // Céges/EP vevő-adatok elkülönített (owned) blokkban.
            e.OwnsOne(x => x.BillingParty, bp =>
            {
                bp.Property(p => p.Name).HasMaxLength(300);
                bp.Property(p => p.TaxNumber).HasMaxLength(30);
                bp.Property(p => p.Address).HasMaxLength(300);
            });
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

        b.Entity<BusinessHour>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Day).IsUnique();
        });

        b.Entity<CalendarOverride>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Label).IsRequired().HasMaxLength(120);
            e.HasIndex(x => x.Date).IsUnique();
        });

        b.Entity<CallRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.PhoneNumber).IsRequired().HasMaxLength(30);
            e.Property(x => x.IvrMenu).HasMaxLength(100);
            e.Property(x => x.RecordingReference).HasMaxLength(300);
            e.HasOne(x => x.Patient).WithMany()
                .HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.PhoneNumber);
        });

        b.Entity<CallbackRequest>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.PhoneNumber).IsRequired().HasMaxLength(30);
            e.Property(x => x.Resolution).HasMaxLength(300);
            e.HasOne(x => x.Patient).WithMany()
                .HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.PhoneNumber, x.Status });
        });

        b.Entity<PatientPhone>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Number).IsRequired().HasMaxLength(30);
            e.Property(x => x.ContactName).HasMaxLength(200);
            e.HasOne(x => x.Patient).WithMany(p => p.Phones)
                .HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<InvoiceLine>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ItemCode).HasMaxLength(50);
            e.Property(x => x.ProductName).IsRequired().HasMaxLength(300);
            e.HasOne(x => x.Invoice).WithMany(i => i.Lines)
                .HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Shipment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TrackingNumber).IsRequired().HasMaxLength(60);
            e.Property(x => x.ReceivedBy).HasMaxLength(200);
            e.HasOne(x => x.Patient).WithMany(p => p.Shipments)
                .HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.TrackingNumber);
        });

        b.Entity<PhilipsReplacement>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ReplacementModel).IsRequired().HasMaxLength(200);
            e.Property(x => x.SerialNumber).IsRequired().HasMaxLength(100);
            // Egy beteghez legfeljebb egy csereprojekt-adat.
            e.HasOne(x => x.Patient).WithOne(p => p.PhilipsReplacement)
                .HasForeignKey<PhilipsReplacement>(x => x.PatientId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.SerialNumber);
        });

        b.Entity<ReferralSource>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.DoctorName).HasMaxLength(200);
        });

        b.Entity<CallNote>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Summary).IsRequired().HasMaxLength(2000);
            e.HasOne(x => x.Call).WithMany()
                .HasForeignKey(x => x.CallId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Patient).WithMany()
                .HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.ReferralSource).WithMany()
                .HasForeignKey(x => x.ReferralSourceId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.CallId).IsUnique();
        });

        b.Entity<DataCompletionRequest>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Token).IsRequired().HasMaxLength(128);
            e.Property(x => x.MissingFields).HasMaxLength(200);
            e.HasOne(x => x.Patient).WithMany()
                .HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.Token).IsUnique();
        });

        b.Entity<HealthFund>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.OfficialAddress).HasMaxLength(300);
            e.Property(x => x.TaxNumber).HasMaxLength(30);
        });

        b.Entity<EanCode>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).IsRequired().HasMaxLength(60);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.Used);
        });

        b.Entity<PostalTrial>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.OrderNumber).IsRequired().HasMaxLength(50);
            e.Property(x => x.DeviceModel).HasMaxLength(200);
            e.Property(x => x.MaskModel).HasMaxLength(200);
            e.Property(x => x.PayableAmount).HasColumnType("numeric(12,2)");
            e.HasOne(x => x.Patient).WithMany()
                .HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.OrderNumber).IsUnique();
            e.HasIndex(x => x.Status);
        });

        b.Entity<ExpressIntake>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RequesterName).HasMaxLength(200);
            e.Property(x => x.RequesterPhone).HasMaxLength(30);
            e.Property(x => x.RequesterEmail).HasMaxLength(256);
            e.Property(x => x.DeviceModel).HasMaxLength(200);
            e.Property(x => x.MaskModel).HasMaxLength(200);
            e.Property(x => x.PayableAmount).HasColumnType("numeric(12,2)");
            e.Property(x => x.PressureCmH2O).HasColumnType("numeric(5,2)");
            e.HasOne(x => x.Patient).WithMany()
                .HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.Status);
        });

        b.Entity<Contract>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Number).IsRequired().HasMaxLength(50);
            e.Property(x => x.DeviceModel).IsRequired().HasMaxLength(200);
            e.Property(x => x.DeviceSerialNumber).IsRequired().HasMaxLength(100);
            e.Property(x => x.MaskModel).HasMaxLength(200);
            e.Property(x => x.PdfReference).HasMaxLength(300);
            e.Property(x => x.ScannedReference).HasMaxLength(300);
            e.Property(x => x.SignedFromIp).HasMaxLength(64);
            e.Property(x => x.DepositAmount).HasColumnType("numeric(12,2)");
            e.Property(x => x.PressureCmH2O).HasColumnType("numeric(5,2)");
            e.HasOne(x => x.Patient).WithMany(p => p.Contracts)
                .HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.Number).IsUnique();
            e.HasIndex(x => x.Status);
        });
    }
}
