using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Common;
using Szakuzlet.Application.Orders;
using Szakuzlet.Application.WearExpiry;
using Szakuzlet.Domain.Entities;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Tests;

public class OrderAndWearTests
{
    private static Patient NewPatient(FakeClock clock, string? mobile, string? email) => new()
    {
        Id = Guid.NewGuid(),
        FullName = "Rendelő Rózsa",
        MobilePhone = mobile,
        Email = email,
        CreatedAtUtc = clock.UtcNow
    };

    private static (OrderCompletionService svc, FakeClock clock, FakeNotificationSender notif, TestDb db) BuildOrder()
    {
        var db = new TestDb();
        var clock = new FakeClock();
        var events = new EventRecorder(db.Context, clock);
        var notif = new FakeNotificationSender();
        var tokens = new FakeTokenGenerator();
        return (new OrderCompletionService(db.Context, clock, events, notif, tokens), clock, notif, db);
    }

    [Fact]
    public async Task Hianyos_kontakt_eseten_adatpotlasra_var_es_linket_kuld()
    {
        var (svc, clock, notif, db) = BuildOrder();
        using var _db = db;
        var p = NewPatient(clock, mobile: null, email: null);
        db.Context.Patients.Add(p);
        await db.Context.SaveChangesAsync();

        var order = await svc.RegisterAsync(p.Id, "REND-001");

        Assert.Equal(OrderStatus.AdatpotlasraVar, order.Status);
        Assert.NotNull(order.DataCompletionToken);
        // Nincs elérhetőség, így a pult intézi; értesítés nem ment ki.
        Assert.Empty(notif.Sent);
    }

    [Fact]
    public async Task Van_kontakt_eseten_azonnal_kiszolgalhato()
    {
        var (svc, clock, _, db) = BuildOrder();
        using var _db = db;
        var p = NewPatient(clock, mobile: "+36301112222", email: null);
        db.Context.Patients.Add(p);
        await db.Context.SaveChangesAsync();

        var order = await svc.RegisterAsync(p.Id, "REND-002");

        Assert.Equal(OrderStatus.Kiszolgalhato, order.Status);
    }

    [Fact]
    public async Task Adatpotlas_aktivalja_a_rendelest()
    {
        var (svc, clock, _, db) = BuildOrder();
        using var _db = db;
        var p = NewPatient(clock, mobile: null, email: null);
        db.Context.Patients.Add(p);
        await db.Context.SaveChangesAsync();
        var order = await svc.RegisterAsync(p.Id, "REND-003");

        await svc.CompleteDataAsync(order.DataCompletionToken!, mobile: "+36309998888", email: null);

        var refreshed = await db.Context.Orders.SingleAsync();
        Assert.Equal(OrderStatus.Kiszolgalhato, refreshed.Status);
        Assert.Null(refreshed.DataCompletionToken); // link elhasználódott
        var refreshedPatient = await db.Context.Patients.SingleAsync();
        Assert.Equal("+36309998888", refreshedPatient.MobilePhone);
    }

    [Fact]
    public async Task Adatpotlasra_varo_rendeles_nem_zarhato_le()
    {
        var (svc, clock, _, db) = BuildOrder();
        using var _db = db;
        var p = NewPatient(clock, mobile: null, email: null);
        db.Context.Patients.Add(p);
        await db.Context.SaveChangesAsync();
        var order = await svc.RegisterAsync(p.Id, "REND-004");

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CloseAsync(order.Id));
    }

    [Fact]
    public async Task Lezaras_rendszeruzenetet_kuld()
    {
        var (svc, clock, notif, db) = BuildOrder();
        using var _db = db;
        var p = NewPatient(clock, mobile: null, email: "rozsa@example.hu");
        db.Context.Patients.Add(p);
        await db.Context.SaveChangesAsync();
        var order = await svc.RegisterAsync(p.Id, "REND-005"); // kiszolgálható (van e-mail)

        await svc.CloseAsync(order.Id);

        var refreshed = await db.Context.Orders.SingleAsync();
        Assert.Equal(OrderStatus.Lezarva, refreshed.Status);
        Assert.Contains(notif.Sent, m => m.Subject.Contains("Köszönjük megrendelését"));
        Assert.Contains(await db.Context.TimelineEvents.ToListAsync(),
            t => t.EventType == "RendelesLezarva");
    }

    [Fact]
    public async Task Kihordasi_ido_ertesito_csak_hozzajarulassal_es_lejaratkor_megy_ki()
    {
        var db = new TestDb();
        using var _db = db;
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero) };
        var events = new EventRecorder(db.Context, clock);
        var notif = new FakeNotificationSender();
        var svc = new WearExpiryService(db.Context, clock, events, notif);

        // Páciens: 2025-01-01-i vásárlás, 12 hó kihordás -> 2026-01-01 lejárat (múltbeli).
        var p = NewPatient(clock, mobile: "+36301234567", email: null);
        p.LastPurchaseDate = new DateOnly(2025, 1, 1);
        p.ProductWearMonths = 12;
        db.Context.Patients.Add(p);
        var sheet = new DataSheet
        {
            Id = Guid.NewGuid(), PatientId = p.Id, Status = DataSheetStatus.Vegleges,
            Channel = DataSheetChannel.HelysziniTablet, CreatedAtUtc = clock.UtcNow
        };
        db.Context.DataSheets.Add(sheet);
        db.Context.Consents.Add(new ConsentRecord
        {
            Id = Guid.NewGuid(), PatientId = p.Id, DataSheetId = sheet.Id,
            Channel = ConsentChannel.KihordasiIdoTajekoztatas, Granted = true, RecordedAtUtc = clock.UtcNow
        });
        await db.Context.SaveChangesAsync();

        var sent = await svc.RunAsync();

        Assert.Equal(1, sent);
        Assert.Contains(notif.Sent, m => m.Subject.Contains("kihordási idő"));
        // Nem küld duplán.
        Assert.Equal(0, await svc.RunAsync());
    }

    [Fact]
    public async Task Kihordasi_ido_ertesito_hozzajarulas_nelkul_nem_megy_ki()
    {
        var db = new TestDb();
        using var _db = db;
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero) };
        var events = new EventRecorder(db.Context, clock);
        var notif = new FakeNotificationSender();
        var svc = new WearExpiryService(db.Context, clock, events, notif);

        var p = NewPatient(clock, mobile: "+36301234567", email: null);
        p.LastPurchaseDate = new DateOnly(2025, 1, 1);
        p.ProductWearMonths = 12;
        db.Context.Patients.Add(p);
        // Nincs hozzájárulás rögzítve.
        await db.Context.SaveChangesAsync();

        Assert.Equal(0, await svc.RunAsync());
        Assert.Empty(notif.Sent);
    }
}
