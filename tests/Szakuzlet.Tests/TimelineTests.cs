using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Common;
using Szakuzlet.Application.Logistics;
using Szakuzlet.Application.Philips;
using Szakuzlet.Application.Timeline;
using Szakuzlet.Domain.Entities;
using Szakuzlet.Domain.Enums;
using Szakuzlet.Infrastructure.Logistics;
using Szakuzlet.Infrastructure.Philips;

namespace Szakuzlet.Tests;

public class TimelineTests
{
    private static async Task<Patient> AddPatientAsync(TestDb db, FakeClock clock,
        string name = "Idő Vonal", string? taj = null)
    {
        var p = new Patient
        {
            Id = Guid.NewGuid(), FullName = name, TajNumber = taj,
            DateOfBirth = new DateOnly(1960, 5, 5), CreatedAtUtc = clock.UtcNow
        };
        db.Context.Patients.Add(p);
        await db.Context.SaveChangesAsync();
        return p;
    }

    [Fact]
    public async Task Timeline_osszefesuli_a_szamlat_termeknevvel_csomagot_es_esemenyeket()
    {
        using var db = new TestDb();
        var clock = new FakeClock();
        var events = new EventRecorder(db.Context, clock);
        var svc = new TimelineService(db.Context);
        var p = await AddPatientAsync(db, clock);

        // Számla tétellel (konkrét modellnév).
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(), PatientId = p.Id, Number = "SZ-100",
            CreatedAtUtc = clock.UtcNow.AddDays(-2)
        };
        invoice.Lines.Add(new InvoiceLine
        {
            Id = Guid.NewGuid(), InvoiceId = invoice.Id,
            ProductName = "AirSense 11 AutoSet", Quantity = 1
        });
        db.Context.Invoices.Add(invoice);

        // Csomag.
        db.Context.Shipments.Add(new Shipment
        {
            Id = Guid.NewGuid(), PatientId = p.Id, Courier = Courier.Gls,
            TrackingNumber = "GLS-1", Status = ShipmentStatus.Atveve, ReceivedBy = "Idő Vonal",
            CreatedAtUtc = clock.UtcNow.AddDays(-1), StatusUpdatedAtUtc = clock.UtcNow.AddDays(-1)
        });

        // Általános esemény.
        events.Timeline(p.Id, "AdatlapVeglegesitve", "Digitális adatlap véglegesítve.");
        await db.Context.SaveChangesAsync();

        var timeline = await svc.GetAsync(p.Id);

        Assert.Equal(3, timeline.Items.Count);
        // Időrend: csökkenő (legfrissebb elöl). Az esemény a "most", legfrissebb.
        Assert.True(timeline.Items[0].OccurredAtUtc >= timeline.Items[1].OccurredAtUtc);
        // A számlához a konkrét modellnév látszik.
        Assert.Contains(timeline.Items, i => i.Category == "Számla" && i.Detail!.Contains("AirSense 11"));
        Assert.Contains(timeline.Items, i => i.Category == "Csomag" && i.Detail!.Contains("Átvéve"));
    }

    [Fact]
    public async Task Futar_szinkron_frissiti_a_statuszt_es_timeline_esemenyt_general()
    {
        using var db = new TestDb();
        var clock = new FakeClock();
        var events = new EventRecorder(db.Context, clock);
        var courier = new MockCourierClient();
        var shipSvc = new ShipmentService(db.Context, clock, events, courier);
        var p = await AddPatientAsync(db, clock);

        var shipment = await shipSvc.CreateAsync(p.Id, Courier.Mpl, "MPL-9");
        Assert.Equal(ShipmentStatus.Feladva, shipment.Status);

        // A futár rendszerében a csomag átvéve.
        courier.SetStatus("MPL-9", ShipmentStatus.Atveve, "Kovács Anna", clock.UtcNow.AddHours(3));
        var changed = await shipSvc.SyncOpenShipmentsAsync();

        Assert.Equal(1, changed);
        var refreshed = await db.Context.Shipments.SingleAsync();
        Assert.Equal(ShipmentStatus.Atveve, refreshed.Status);
        Assert.Equal("Kovács Anna", refreshed.ReceivedBy);
        Assert.Contains(await db.Context.TimelineEvents.ToListAsync(),
            t => t.EventType == "CsomagStatusz");

        // Újabb szinkron már nem változtat (átvéve = végállapot).
        Assert.Equal(0, await shipSvc.SyncOpenShipmentsAsync());
    }

    [Fact]
    public async Task Philips_import_TAJ_alapjan_par_es_riasztast_general()
    {
        using var db = new TestDb();
        var clock = new FakeClock();
        var events = new EventRecorder(db.Context, clock);
        var svc = new PhilipsImportService(db.Context, clock, events);
        var timeline = new TimelineService(db.Context);

        var p = await AddPatientAsync(db, clock, "Teszt Tamás", taj: "123 456 789");

        var rows = new List<PhilipsImportRow>
        {
            new("123456789", "Teszt Tamás", "DreamStation 2", "SN-ABC-001", new DateOnly(2024, 3, 1)),
            new("999999999", "Ismeretlen Iván", "DreamStation 2", "SN-XYZ-002", null)
        };
        var result = await svc.ImportAsync(rows);

        Assert.Equal(1, result.Matched);
        Assert.Equal(1, result.Unmatched);
        Assert.Contains("Ismeretlen Iván", result.UnmatchedNames);

        // A beteg megnyitásakor piros riasztás.
        var alert = await timeline.GetPhilipsAlertAsync(p.Id);
        Assert.NotNull(alert);
        Assert.Equal("DreamStation 2", alert!.Model);
        Assert.Equal("SN-ABC-001", alert.SerialNumber);
    }

    [Fact]
    public async Task Philips_import_fallback_nevre_ha_nincs_TAJ()
    {
        using var db = new TestDb();
        var clock = new FakeClock();
        var events = new EventRecorder(db.Context, clock);
        var svc = new PhilipsImportService(db.Context, clock, events);
        var p = await AddPatientAsync(db, clock, "Név Egyezés", taj: null);

        var rows = new List<PhilipsImportRow>
        {
            new(null, "név egyezés", "System One", "SN-1", null) // kis-nagybetű érzéketlen
        };
        var result = await svc.ImportAsync(rows);

        Assert.Equal(1, result.Matched);
        Assert.NotNull(await db.Context.PhilipsReplacements.FirstOrDefaultAsync(x => x.PatientId == p.Id));
    }

    [Fact]
    public void CsvParser_feldolgozza_a_fejleces_es_idezojeles_sorokat()
    {
        var csv = "taj,nev,modell,gyariszam,csere_datum\n" +
                  "123456789,\"Teszt, Tamás\",DreamStation 2,SN-1,2024-03-01\n" +
                  ",Név Nélküli TAJ,System One,SN-2,\n";

        var rows = PhilipsCsvParser.Parse(csv);

        Assert.Equal(2, rows.Count);
        Assert.Equal("Teszt, Tamás", rows[0].PatientName); // idézőjeles vessző megőrizve
        Assert.Equal(new DateOnly(2024, 3, 1), rows[0].ReplacedOn);
        Assert.Null(rows[1].ReplacedOn);
    }
}
