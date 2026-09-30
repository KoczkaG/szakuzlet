using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Common;
using Szakuzlet.Application.DataSheets;
using Szakuzlet.Application.Invoices;
using Szakuzlet.Application.Orders;
using Szakuzlet.Application.Patients;
using Szakuzlet.Domain.Enums;
using Szakuzlet.Infrastructure.Kvl;

namespace Szakuzlet.Tests;

/// <summary>
/// Végponttól végpontig forgatókönyv: régi ügyfél előhívása -> adatlap véglegesítése
/// (nincs küldési hozzájárulás) -> számla parkoltatás -> egyeztetés -> postai rendelés.
/// </summary>
public class EndToEndScenarioTests
{
    [Fact]
    public async Task Teljes_A_resz_folyamat_egyben_mukodik()
    {
        using var db = new TestDb();
        var clock = new FakeClock();
        var kvl = new MockKvlClient();
        var tokens = new FakeTokenGenerator();
        var notif = new FakeNotificationSender();
        var events = new EventRecorder(db.Context, clock);

        var lookup = new PatientLookupService(db.Context, kvl, clock);
        var sheets = new DataSheetService(db.Context, clock, tokens, kvl, events);
        var invoices = new InvoiceParkingService(db.Context, clock, events, notif);
        var orders = new OrderCompletionService(db.Context, clock, events, notif, tokens);

        // 1) Régi ügyfél előhívása (Kovács Lajosné, 1956-03-22).
        var match = (await lookup.FindByDateOfBirthAsync(new DateOnly(1956, 3, 22)))
            .Matches.First(m => m.FullName == "Kovács Lajosné");

        // 2) Pulti adatlap véglegesítése – a beteg SEM postai SEM e-mailes küldéshez nem járul hozzá.
        var sheetId = await sheets.CreateWalkInSheetAsync(match.PatientId);
        await sheets.SubmitAsync(sheetId, new DataSheetInput
        {
            FullName = "Kovács Lajosné",
            DateOfBirth = new DateOnly(1956, 3, 22),
            TajNumber = "123 456 789",
            MobilePhone = "+36301234567",
            Email = "kovacs.lajosne@example.hu",
            PostalCode = "1145",
            City = "Budapest",
            AddressLine = "Lakatos utca 22.",
            Consents = new ConsentAnswers(
                KihordasiIdoTajekoztatas: true,
                Hirlevel: false,
                PostaiKuldemeny: false,
                EmailKuldemeny: false)
        }, new SubmissionContext("1.1.1.1", "E2E"));

        // 3) Számla kiállítása -> a radar parkoltat + feladatot generál.
        var invoice = await invoices.IssueAsync(match.PatientId, "SZ-E2E-1");
        Assert.Equal(InvoiceStatus.Parkoltatva, invoice.Status);
        Assert.Single(await db.Context.Tasks.Where(t => t.Status == PatientTaskStatus.Nyitott).ToListAsync());

        // 4) Telefonos egyeztetés: téves kitöltés volt, az ügyfél mégis kér e-mailt.
        await invoices.ResolveAsWrongEntryAsync(invoice.Id, allowPostal: false, allowEmail: true);
        Assert.Equal(InvoiceStatus.Kikuldheto,
            (await db.Context.Invoices.SingleAsync(i => i.Id == invoice.Id)).Status);
        Assert.Empty(await db.Context.Tasks.Where(t => t.Status == PatientTaskStatus.Nyitott).ToListAsync());

        // 5) Postai rendelés – most már van e-mail, így kiszolgálható, majd lezárható.
        var order = await orders.RegisterAsync(match.PatientId, "REND-E2E-1");
        Assert.Equal(OrderStatus.Kiszolgalhato, order.Status);
        await orders.CloseAsync(order.Id);
        Assert.Equal(OrderStatus.Lezarva,
            (await db.Context.Orders.SingleAsync(o => o.Id == order.Id)).Status);

        // 6) A teljes idővonal (History) tartalmazza a főbb eseményeket.
        var timeline = await db.Context.TimelineEvents
            .Where(t => t.PatientId == match.PatientId)
            .Select(t => t.EventType)
            .ToListAsync();
        Assert.Contains("AdatlapVeglegesitve", timeline);
        Assert.Contains("SzamlaParkoltatva", timeline);
        Assert.Contains("SzamlaParkoltatasFeloldva", timeline);
        Assert.Contains("RendelesLezarva", timeline);

        // 7) A KVL mock is megkapta a frissített profilt.
        var kvlPartner = await kvl.GetPartnerByCodeAsync("KVL-1001");
        Assert.Equal("Budapest", kvlPartner!.City);
    }
}
