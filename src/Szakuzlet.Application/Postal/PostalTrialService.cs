using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Application.Banking;
using Szakuzlet.Application.Common;
using Szakuzlet.Application.Notifications;
using Szakuzlet.Domain.Entities;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Application.Postal;

/// <summary>Postai próbaigény befogadásának adatai.</summary>
public record PostalTrialInput(string? DeviceModel, string? MaskModel, decimal PayableAmount);

/// <summary>Egy elmaradós (kint ragadt) próba riport-tétele.</summary>
public record OverdueTrial(Guid TrialId, string OrderNumber, string PatientName, DateOnly? Deadline);

/// <summary>
/// Postai úton indított próbakezelések teljes életciklusa (III. Modul C):
/// igény → banki előre utalás párosítás → digitális szerződés-aláírás (raktári zár) →
/// futáros feladás → kézbesítés (8 napos maszkcsere + kontroll-követés) → „Hahó” + elmaradós riport.
/// </summary>
public sealed class PostalTrialService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly EventRecorder _events;
    private readonly INotificationSender _notifications;
    private readonly IBankClient _bank;

    private const int TrialMonths = 2;
    private const int MaskSwapDays = 8;
    private const int ReminderGraceDays = 8;

    public PostalTrialService(IAppDbContext db, IClock clock, EventRecorder events,
        INotificationSender notifications, IBankClient bank)
    {
        _db = db;
        _clock = clock;
        _events = events;
        _notifications = notifications;
        _bank = bank;
    }

    /// <summary>Postai próbaigény befogadása. Egyedi rendelésszámot (banki közlemény) generál.</summary>
    public async Task<PostalTrial> RequestAsync(Guid patientId, PostalTrialInput input, CancellationToken ct = default)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen páciens: {patientId}");

        var now = _clock.UtcNow;
        var trial = new PostalTrial
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            OrderNumber = $"P-{now.Year}-{now.Ticks % 1_000_000:D6}",
            Status = PostalTrialStatus.FizetesreVar,
            DeviceModel = input.DeviceModel,
            MaskModel = input.MaskModel,
            PayableAmount = input.PayableAmount,
            CreatedAtUtc = now
        };
        _db.PostalTrials.Add(trial);

        _events.Timeline(patient.Id, "PostaiProbaIgeny",
            $"Postai próbakezelés igényelve ({trial.OrderNumber}); előre utalásra vár.");

        // Utalási adatok kiküldése az egyedi közleménnyel.
        if (!string.IsNullOrWhiteSpace(patient.Email))
            await _notifications.SendAsync(new NotificationMessage(NotificationKind.Email, patient.Email!,
                "Megrendelését megkaptuk",
                $"Kérjük, utalja el a(z) {trial.PayableAmount:N0} Ft-ot, a közlemény rovatba írja: {trial.OrderNumber}."), ct);

        await _db.SaveChangesAsync(ct);
        return trial;
    }

    /// <summary>
    /// Banki szinkron: a fizetésre váró próbáknál ellenőrzi az előre utalás beérkezését az egyedi
    /// közlemény alapján. Beérkezéskor a próba aláírásra vár, és megnyugtató értesítés megy ki.
    /// Visszaadja a párosított próbák számát. (Ütemezett háttérfolyamat hívja.)
    /// </summary>
    public async Task<int> SyncPaymentsAsync(CancellationToken ct = default)
    {
        var waiting = await _db.PostalTrials
            .Where(t => t.Status == PostalTrialStatus.FizetesreVar)
            .ToListAsync(ct);

        var matched = 0;
        foreach (var trial in waiting)
        {
            var transfer = await _bank.FindTransferAsync(trial.OrderNumber, ct);
            if (transfer is null) continue;

            trial.Status = PostalTrialStatus.AlairasraVar;
            matched++;

            var patient = await _db.Patients.FirstAsync(p => p.Id == trial.PatientId, ct);
            _events.Timeline(patient.Id, "PostaiProbaFizetes",
                $"Utalás beérkezett ({trial.OrderNumber}); szerződés aláírásra vár.");
            if (!string.IsNullOrWhiteSpace(patient.Email))
                await _notifications.SendAsync(new NotificationMessage(NotificationKind.Email, patient.Email!,
                    "Utalását megkaptuk", "Köszönjük, utalása beérkezett! A csomag feladása a szerződés " +
                    "digitális aláírása után indul."), ct);
        }

        if (matched > 0) await _db.SaveChangesAsync(ct);
        return matched;
    }

    /// <summary>
    /// Digitális szerződés-aláírás rögzítése: a próba „csomagolható” lesz (a raktári zár feloldódik).
    /// A tényleges eIDAS-aláírást a ContractService végzi; itt a próba állapotát léptetjük.
    /// </summary>
    public async Task LinkSignedContractAsync(Guid trialId, Guid contractId, CancellationToken ct = default)
    {
        var trial = await LoadAsync(trialId, ct);
        if (trial.Status != PostalTrialStatus.AlairasraVar)
            throw new InvalidOperationException("A próba nem aláírásra váró állapotban van.");

        trial.ContractId = contractId;
        trial.Status = PostalTrialStatus.Csomagolhato;
        _events.Timeline(trial.PatientId, "PostaiProbaAlairva",
            "Szerződés aláírva – a csomag feladható (raktári zár feloldva).");
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Raktári zár ellenőrzése: aláíratlan (nem „csomagolható”) próbához NEM adható futár.
    /// A feladás rögzítése; a próba kiszállítás alatt lesz.
    /// </summary>
    public async Task MarkShippedAsync(Guid trialId, Guid shipmentId, CancellationToken ct = default)
    {
        var trial = await LoadAsync(trialId, ct);
        if (trial.Status != PostalTrialStatus.Csomagolhato)
            throw new InvalidOperationException(
                "Aláíratlan/jóváhagyatlan próba nem adható futárnak (raktári zár).");

        trial.ShipmentId = shipmentId;
        trial.Status = PostalTrialStatus.KiszallitasAlatt;
        _events.Timeline(trial.PatientId, "PostaiProbaFeladva", "A csomag feladva, kiszállítás alatt.");
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Sikeres kézbesítés: elindul a próbaidőszak számlálója, beállítjuk a 8 napos maszkcsere és a
    /// 2 havi próbahatáridőt, és kiküldjük a támogató levelet (8 napos maszkcsere garanciával).
    /// </summary>
    public async Task MarkDeliveredAsync(Guid trialId, DateOnly? controlDate = null, CancellationToken ct = default)
    {
        var trial = await LoadAsync(trialId, ct);
        var now = _clock.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        trial.Status = PostalTrialStatus.Kezbesitve;
        trial.DeliveredAtUtc = now;
        trial.MaskSwapDeadline = today.AddDays(MaskSwapDays);
        trial.TrialDeadline = today.AddMonths(TrialMonths);
        trial.ControlDate = controlDate;

        var patient = await _db.Patients.FirstAsync(p => p.Id == trial.PatientId, ct);
        var controlText = controlDate is { } d
            ? $"Kórházi kontrolljának időpontja: {d:yyyy-MM-dd}."
            : "Kórházi kontrolljának időpontja nincs rögzítve – kérjük, egyeztesse a kezelő kórházával.";
        if (!string.IsNullOrWhiteSpace(patient.Email))
            await _notifications.SendAsync(new NotificationMessage(NotificationKind.Email, patient.Email!,
                "Csomagját átvette, terápiája elindult!",
                $"Bármilyen kérdés esetén keressen minket bizalommal! Amennyiben a maszk mérete vagy típusa " +
                $"nem kényelmes, a kézbesítéstől számított {MaskSwapDays} napon belül DÍJMENTES maszkcserét " +
                $"biztosítunk. {controlText}"), ct);

        _events.Timeline(patient.Id, "PostaiProbaKezbesitve",
            $"Csomag kézbesítve; próbaidőszak lejárat: {trial.TrialDeadline:yyyy-MM-dd}.");
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Kontroll-dátum módosítása; a próbahatáridőt is átkalibrálja (téves riasztás elkerülése).</summary>
    public async Task UpdateControlDateAsync(Guid trialId, DateOnly newControlDate, CancellationToken ct = default)
    {
        var trial = await LoadAsync(trialId, ct);
        trial.ControlDate = newControlDate;
        // A próbahatáridőt a kontrollhoz igazítjuk (a kontroll után zárható a próba).
        trial.TrialDeadline = newControlDate;
        trial.ReminderSent = false; // az emlékeztető órát újraindítjuk
        _events.Timeline(trial.PatientId, "KontrollModositva",
            $"Kontroll időpont módosítva: {newControlDate:yyyy-MM-dd}; határidők átkalibrálva.");
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Lezárás-segítő: a próbahatáridő közelében „Hahó” emlékeztetőt küld, és összeállítja az
    /// elmaradós (kint ragadt) próbák riportját azokból, akik a türelmi idő után sem reagáltak.
    /// (Ütemezett háttérfolyamat hívja.)
    /// </summary>
    public async Task<IReadOnlyList<OverdueTrial>> RunReminderAndOverdueAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);

        var active = await _db.PostalTrials
            .Where(t => t.Status == PostalTrialStatus.Kezbesitve && t.TrialDeadline != null)
            .ToListAsync(ct);

        var overdue = new List<OverdueTrial>();
        foreach (var trial in active)
        {
            var deadline = trial.TrialDeadline!.Value;

            // 1) Határidő elérésekor egyszeri „Hahó” emlékeztető.
            if (!trial.ReminderSent && today >= deadline)
            {
                trial.ReminderSent = true;
                var patient = await _db.Patients.FirstAsync(p => p.Id == trial.PatientId, ct);
                if (!string.IsNullOrWhiteSpace(patient.Email))
                    await _notifications.SendAsync(new NotificationMessage(NotificationKind.Email, patient.Email!,
                        "Emlékeztető – próbaidőszak lezárása",
                        "Próbaidőszaka a végéhez ért. Kérjük, egyeztessen velünk a lezárásról " +
                        "(hosszabbítás / megtartás / kontroll-egyeztetés)."), ct);
                _events.Timeline(trial.PatientId, "HahoEmlekezteto", "Lezárás előtti emlékeztető kiküldve.");
            }

            // 2) Ha a türelmi idő (emlékeztető + grace) után sem reagált -> elmaradós riport.
            if (trial.ReminderSent && today >= deadline.AddDays(ReminderGraceDays))
            {
                var patient = await _db.Patients.FirstAsync(p => p.Id == trial.PatientId, ct);
                overdue.Add(new OverdueTrial(trial.Id, trial.OrderNumber, patient.FullName, deadline));
            }
        }

        await _db.SaveChangesAsync(ct);
        return overdue;
    }

    private async Task<PostalTrial> LoadAsync(Guid id, CancellationToken ct)
        => await _db.PostalTrials.FirstOrDefaultAsync(t => t.Id == id, ct)
           ?? throw new InvalidOperationException($"Nincs ilyen postai próba: {id}");
}
