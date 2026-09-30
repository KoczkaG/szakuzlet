using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Common;
using Szakuzlet.Application.Postal;
using Szakuzlet.Domain.Entities;
using Szakuzlet.Domain.Enums;
using Szakuzlet.Infrastructure.Banking;

namespace Szakuzlet.Tests;

public class PostalTrialTests
{
    private static (PostalTrialService svc, MockBankClient bank, FakeNotificationSender notif, FakeClock clock, TestDb db)
        Build()
    {
        var db = new TestDb();
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero) };
        var events = new EventRecorder(db.Context, clock);
        var notif = new FakeNotificationSender();
        var bank = new MockBankClient();
        return (new PostalTrialService(db.Context, clock, events, notif, bank), bank, notif, clock, db);
    }

    private static async Task<Patient> AddPatientAsync(TestDb db, FakeClock clock)
    {
        var p = new Patient
        {
            Id = Guid.NewGuid(), FullName = "Postai Pál", Email = "p@x.hu",
            MobilePhone = "+36301234567", CreatedAtUtc = clock.UtcNow
        };
        db.Context.Patients.Add(p);
        await db.Context.SaveChangesAsync();
        return p;
    }

    private static PostalTrialInput Input() => new("AirSense 11", "Orrmaszk M", 120_000m);

    [Fact]
    public async Task Igeny_fizetesre_var_es_utalasi_adatot_kuld()
    {
        var (svc, _, notif, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock);

        var trial = await svc.RequestAsync(p.Id, Input());

        Assert.Equal(PostalTrialStatus.FizetesreVar, trial.Status);
        Assert.StartsWith("P-", trial.OrderNumber);
        Assert.Contains(notif.Sent, m => m.Body.Contains(trial.OrderNumber));
    }

    [Fact]
    public async Task Banki_szinkron_parositja_az_utalast_a_kozlemeny_alapjan()
    {
        var (svc, bank, _, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock);
        var trial = await svc.RequestAsync(p.Id, Input());

        // Nincs utalás -> nincs párosítás.
        Assert.Equal(0, await svc.SyncPaymentsAsync());

        // Beérkezik az utalás a közleménnyel.
        bank.SimulateTransfer(trial.OrderNumber, 120_000m, clock.UtcNow);
        var matched = await svc.SyncPaymentsAsync();

        Assert.Equal(1, matched);
        Assert.Equal(PostalTrialStatus.AlairasraVar,
            (await db.Context.PostalTrials.SingleAsync()).Status);
    }

    [Fact]
    public async Task Alairatlan_proba_nem_adhato_futarnak_raktari_zar()
    {
        var (svc, bank, _, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock);
        var trial = await svc.RequestAsync(p.Id, Input());
        bank.SimulateTransfer(trial.OrderNumber, 120_000m, clock.UtcNow);
        await svc.SyncPaymentsAsync(); // AlairasraVar

        // Aláírás nélkül a feladás tiltott.
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.MarkShippedAsync(trial.Id, Guid.NewGuid()));

        // Aláírás után csomagolható, majd feladható.
        await svc.LinkSignedContractAsync(trial.Id, Guid.NewGuid());
        await svc.MarkShippedAsync(trial.Id, Guid.NewGuid());
        Assert.Equal(PostalTrialStatus.KiszallitasAlatt,
            (await db.Context.PostalTrials.SingleAsync()).Status);
    }

    [Fact]
    public async Task Kezbesites_beallitja_a_hataridoket_es_maszkcseret_kuld()
    {
        var (svc, bank, notif, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock);
        var trial = await svc.RequestAsync(p.Id, Input());
        bank.SimulateTransfer(trial.OrderNumber, 120_000m, clock.UtcNow);
        await svc.SyncPaymentsAsync();
        await svc.LinkSignedContractAsync(trial.Id, Guid.NewGuid());
        await svc.MarkShippedAsync(trial.Id, Guid.NewGuid());

        await svc.MarkDeliveredAsync(trial.Id);

        var refreshed = await db.Context.PostalTrials.SingleAsync();
        Assert.Equal(PostalTrialStatus.Kezbesitve, refreshed.Status);
        Assert.Equal(new DateOnly(2026, 6, 9), refreshed.MaskSwapDeadline);  // +8 nap
        Assert.Equal(new DateOnly(2026, 8, 1), refreshed.TrialDeadline);     // +2 hónap
        Assert.Contains(notif.Sent, m => m.Body.Contains("DÍJMENTES maszkcserét"));
    }

    [Fact]
    public async Task Kontroll_modositas_atkalibralja_a_hataridot()
    {
        var (svc, bank, _, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock);
        var trial = await svc.RequestAsync(p.Id, Input());
        bank.SimulateTransfer(trial.OrderNumber, 120_000m, clock.UtcNow);
        await svc.SyncPaymentsAsync();
        await svc.LinkSignedContractAsync(trial.Id, Guid.NewGuid());
        await svc.MarkShippedAsync(trial.Id, Guid.NewGuid());
        await svc.MarkDeliveredAsync(trial.Id);

        await svc.UpdateControlDateAsync(trial.Id, new DateOnly(2026, 9, 15));

        var refreshed = await db.Context.PostalTrials.SingleAsync();
        Assert.Equal(new DateOnly(2026, 9, 15), refreshed.ControlDate);
        Assert.Equal(new DateOnly(2026, 9, 15), refreshed.TrialDeadline);
    }

    [Fact]
    public async Task Haho_emlekezteto_majd_elmaradós_riport()
    {
        var (svc, bank, notif, clock, db) = Build();
        using var _db = db;
        var p = await AddPatientAsync(db, clock);
        var trial = await svc.RequestAsync(p.Id, Input());
        bank.SimulateTransfer(trial.OrderNumber, 120_000m, clock.UtcNow);
        await svc.SyncPaymentsAsync();
        await svc.LinkSignedContractAsync(trial.Id, Guid.NewGuid());
        await svc.MarkShippedAsync(trial.Id, Guid.NewGuid());
        await svc.MarkDeliveredAsync(trial.Id); // trial deadline: 2026-08-01

        // A határidő előtt: nincs emlékeztető, nincs elmaradó.
        var before = await svc.RunReminderAndOverdueAsync();
        Assert.Empty(before);

        // A határidőn: „Hahó” emlékeztető megy ki, de még nem elmaradó.
        clock.UtcNow = new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero);
        var onDeadline = await svc.RunReminderAndOverdueAsync();
        Assert.Empty(onDeadline);
        Assert.Contains(notif.Sent, m => m.Subject.Contains("próbaidőszak lezárása"));

        // A türelmi idő (8 nap) után: elmaradós riportban szerepel.
        clock.UtcNow = new DateTimeOffset(2026, 8, 10, 8, 0, 0, TimeSpan.Zero);
        var overdue = await svc.RunReminderAndOverdueAsync();
        Assert.Single(overdue);
        Assert.Equal(trial.OrderNumber, overdue[0].OrderNumber);
    }
}
