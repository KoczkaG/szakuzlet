using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Common;
using Szakuzlet.Application.Invoices;
using Szakuzlet.Domain.Entities;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Tests;

public class InvoiceParkingTests
{
    private static async Task<(Patient patient, Guid sheetId)> SeedPatientAsync(
        TestDb db, FakeClock clock, bool postal, bool email)
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FullName = "Teszt Elek",
            Email = "teszt@example.hu",
            MobilePhone = "+36301234567",
            CreatedAtUtc = clock.UtcNow
        };
        db.Context.Patients.Add(patient);
        var sheet = new DataSheet
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Status = DataSheetStatus.Vegleges,
            Channel = DataSheetChannel.HelysziniTablet,
            CreatedAtUtc = clock.UtcNow
        };
        db.Context.DataSheets.Add(sheet);
        db.Context.Consents.Add(new ConsentRecord
        {
            Id = Guid.NewGuid(), PatientId = patient.Id, DataSheetId = sheet.Id,
            Channel = ConsentChannel.PostaiKuldemeny, Granted = postal, RecordedAtUtc = clock.UtcNow
        });
        db.Context.Consents.Add(new ConsentRecord
        {
            Id = Guid.NewGuid(), PatientId = patient.Id, DataSheetId = sheet.Id,
            Channel = ConsentChannel.EmailKuldemeny, Granted = email, RecordedAtUtc = clock.UtcNow
        });
        await db.Context.SaveChangesAsync();
        return (patient, sheet.Id);
    }

    private static (InvoiceParkingService svc, FakeClock clock, FakeNotificationSender notif, TestDb db) Build()
    {
        var db = new TestDb();
        var clock = new FakeClock();
        var events = new EventRecorder(db.Context, clock);
        var notif = new FakeNotificationSender();
        return (new InvoiceParkingService(db.Context, clock, events, notif), clock, notif, db);
    }

    [Fact]
    public async Task Van_hozzajarulas_esetben_a_szamla_kikuldheto()
    {
        var (svc, clock, _, db) = Build();
        using var _db = db;
        var (patient, _) = await SeedPatientAsync(db, clock, postal: true, email: false);

        var invoice = await svc.IssueAsync(patient.Id, "SZ-001");

        Assert.Equal(InvoiceStatus.Kikuldheto, invoice.Status);
        Assert.Empty(await db.Context.Tasks.ToListAsync());
    }

    [Fact]
    public async Task Zero_hozzajarulas_eseten_parkoltat_es_feladatot_general()
    {
        var (svc, clock, _, db) = Build();
        using var _db = db;
        var (patient, _) = await SeedPatientAsync(db, clock, postal: false, email: false);

        var invoice = await svc.IssueAsync(patient.Id, "SZ-002");

        Assert.Equal(InvoiceStatus.Parkoltatva, invoice.Status);
        Assert.Equal(clock.UtcNow.AddDays(30), invoice.ParkingExpiresAtUtc);

        var task = await db.Context.Tasks.SingleAsync();
        Assert.Equal(PatientTaskType.ZeroHozzajarulasTisztazas, task.Type);
        Assert.Equal(PatientTaskStatus.Nyitott, task.Status);

        // Timeline-bejegyzés is keletkezett.
        Assert.Contains(await db.Context.TimelineEvents.ToListAsync(),
            t => t.EventType == "SzamlaParkoltatva");
    }

    [Fact]
    public async Task Teves_kitoltes_feloldja_a_parkoltatast_es_lezarja_a_feladatot()
    {
        var (svc, clock, notif, db) = Build();
        using var _db = db;
        var (patient, _) = await SeedPatientAsync(db, clock, postal: false, email: false);
        var invoice = await svc.IssueAsync(patient.Id, "SZ-003");

        await svc.ResolveAsWrongEntryAsync(invoice.Id, allowPostal: false, allowEmail: true);

        var refreshed = await db.Context.Invoices.SingleAsync();
        Assert.Equal(InvoiceStatus.Kikuldheto, refreshed.Status);
        Assert.All(await db.Context.Tasks.ToListAsync(),
            t => Assert.Equal(PatientTaskStatus.Lezart, t.Status));
        Assert.Contains(notif.Sent, m => m.Subject.Contains("módosításáról"));
    }

    [Fact]
    public async Task Idozitett_lezaras_lezarja_a_lejart_parkoltatasokat()
    {
        var (svc, clock, _, db) = Build();
        using var _db = db;
        var (patient, _) = await SeedPatientAsync(db, clock, postal: false, email: false);
        var invoice = await svc.IssueAsync(patient.Id, "SZ-004");

        // Nincs még lejárva: 0 lezárás.
        Assert.Equal(0, await svc.CloseExpiredParkingsAsync());

        // Idő előre a lejárat után.
        clock.UtcNow = invoice.ParkingExpiresAtUtc!.Value.AddDays(1);
        var closed = await svc.CloseExpiredParkingsAsync();

        Assert.Equal(1, closed);
        var refreshed = await db.Context.Invoices.SingleAsync();
        Assert.Equal(InvoiceStatus.ParkoltatasLezarva, refreshed.Status);
        Assert.Contains(await db.Context.TimelineEvents.ToListAsync(),
            t => t.EventType == "SzamlaParkoltatasLezarva");
    }
}
