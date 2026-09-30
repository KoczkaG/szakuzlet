using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Application.Common;
using Szakuzlet.Application.Notifications;
using Szakuzlet.Domain.Entities;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Application.Express;

/// <summary>Az online előkészítő portál beérkező igénye.</summary>
public record ExpressIntakeInput(
    RequesterRole RequesterRole,
    string? RequesterName,
    string? RequesterPhone,
    string? RequesterEmail,
    string? DeviceModel,
    string? MaskModel,
    decimal? PressureCmH2O,
    /// <summary>Magánellátásból érkezik-e (az előszűrő ez alapján sorol be).</summary>
    bool FromPrivateCare,
    /// <summary>TB-jogosultság az ambuláns lap alapján (NEAK-hoz kell).</summary>
    bool HasTbEligibility,
    decimal DepositOrPrice);

/// <summary>
/// Expressz pulti kiszolgálási mód (III. Modul B). Az online előkészített ügymenetek befogadása,
/// intelligens előszűrése (NEAK / magánellátás), összekészítés-értesítés, és a variálós ügyfelek
/// kezelése (az expressz sáv megszakítása normál sorra).
/// </summary>
public sealed class ExpressIntakeService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly EventRecorder _events;
    private readonly INotificationSender _notifications;

    public ExpressIntakeService(IAppDbContext db, IClock clock, EventRecorder events,
        INotificationSender notifications)
    {
        _db = db;
        _clock = clock;
        _events = events;
        _notifications = notifications;
    }

    /// <summary>
    /// Online igény befogadása + intelligens előszűrő. Magánellátás vagy hiányzó TB-jogosultság
    /// esetén „Magánellátás - teljes ár”, egyébként „NEAK-támogatott próbakezelés”.
    /// </summary>
    public async Task<ExpressIntake> SubmitAsync(Guid patientId, ExpressIntakeInput input,
        CancellationToken ct = default)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen páciens: {patientId}");

        // Előszűrő: magánellátás VAGY nincs TB-jogosultság -> teljes áras magánügy.
        var classification = (input.FromPrivateCare || !input.HasTbEligibility)
            ? IntakeClassification.MaganellatasTeljesAr
            : IntakeClassification.NeakTamogatottProbakezeles;

        var now = _clock.UtcNow;
        var intake = new ExpressIntake
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            RequesterRole = input.RequesterRole,
            RequesterName = input.RequesterName,
            RequesterPhone = input.RequesterPhone,
            RequesterEmail = input.RequesterEmail,
            Classification = classification,
            Status = ExpressIntakeStatus.Beerkezett,
            DeviceModel = input.DeviceModel,
            MaskModel = input.MaskModel,
            PressureCmH2O = input.PressureCmH2O,
            PayableAmount = input.DepositOrPrice,
            CreatedAtUtc = now
        };
        _db.ExpressIntakes.Add(intake);

        _events.Audit("ExpressIntake", intake.Id.ToString(), "OnlineIgenyBefogadva",
            $"Besorolás: {classification}");
        _events.Timeline(patient.Id, "ExpresszIgeny",
            $"Online előkészített igény befogadva ({HuClass(classification)}).");

        await _db.SaveChangesAsync(ct);
        return intake;
    }

    /// <summary>
    /// Összekészítés kész: „Expressz kiszolgálásra vár” státusz + automata státuszértesítés
    /// az ügyfélnek/hozzátartozónak a fizetendő összeggel.
    /// </summary>
    public async Task MarkReadyAsync(Guid intakeId, CancellationToken ct = default)
    {
        var intake = await LoadAsync(intakeId, ct);
        var patient = await _db.Patients.FirstAsync(p => p.Id == intake.PatientId, ct);
        var now = _clock.UtcNow;

        intake.Status = ExpressIntakeStatus.ExpresszKiszolgalasraVar;
        intake.ReadyAtUtc = now;

        var kind = intake.Classification == IntakeClassification.NeakTamogatottProbakezeles
            ? "NEAK-támogatott próbakezelési önrész/kaució"
            : "magánellátásos teljes ár";
        var body = $"Csomagját hiánytalanul összekészítettük, Szaküzletünkben várakozás nélkül átvehető, " +
                   $"várjuk Önt! Fizetendő ({kind}): {intake.PayableAmount:N0} Ft. " +
                   $"Fizethet bankkártyával vagy készpénzzel a helyszínen.";
        await NotifyRequesterAsync(intake, patient, "Csomagja átvehető", body, ct);

        _events.Timeline(patient.Id, "ExpresszKesz", "Az expressz csomag összekészítve, átvehető.");
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Készlethiány: várakoztató értesítés (a beteg NE induljon el), beszerzésre vár státusz.</summary>
    public async Task MarkOutOfStockAsync(Guid intakeId, CancellationToken ct = default)
    {
        var intake = await LoadAsync(intakeId, ct);
        var patient = await _db.Patients.FirstAsync(p => p.Id == intake.PatientId, ct);
        intake.Status = ExpressIntakeStatus.BeszerzesreVar;

        await NotifyRequesterAsync(intake, patient, "Igényét feldolgoztuk",
            "Online igényét és dokumentumait sikeresen feldolgoztuk, azonban a kért eszköz jelenleg " +
            "beszerzés alatt áll. Kérjük, a végső értesítésünkig NE induljon el a Szaküzletbe, türelmét kérjük!", ct);

        _events.Timeline(patient.Id, "ExpresszKeszlethiany", "Expressz igény: készlethiány, beszerzésre vár.");
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Sikeres személyes átvétel (lezárás).</summary>
    public async Task MarkPickedUpAsync(Guid intakeId, CancellationToken ct = default)
    {
        var intake = await LoadAsync(intakeId, ct);
        intake.Status = ExpressIntakeStatus.Atveve;
        _events.Timeline(intake.PatientId, "ExpresszAtveve", "Az expressz csomagot átvették.");
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Kritikus protokoll: ha a beteg a helyszínen variál / maszkpróbát kér / bizonytalan,
    /// az expressz sáv megszakad, az ügy normál pulti sorba kerül (III. Modul B, 4. pont).
    /// </summary>
    public async Task BreakToNormalQueueAsync(Guid intakeId, string reason, CancellationToken ct = default)
    {
        var intake = await LoadAsync(intakeId, ct);
        intake.Status = ExpressIntakeStatus.NormalSorbaKerult;
        _events.Audit("ExpressIntake", intake.Id.ToString(), "ExpresszMegszakadt", reason);
        _events.Timeline(intake.PatientId, "ExpresszMegszakadt",
            $"Az expressz kiszolgálás megszakadt ({reason}); normál sorba került.");
        await _db.SaveChangesAsync(ct);
    }

    private async Task<ExpressIntake> LoadAsync(Guid id, CancellationToken ct)
        => await _db.ExpressIntakes.FirstOrDefaultAsync(x => x.Id == id, ct)
           ?? throw new InvalidOperationException($"Nincs ilyen expressz igény: {id}");

    private async Task NotifyRequesterAsync(ExpressIntake intake, Patient patient,
        string subject, string body, CancellationToken ct)
    {
        // Hozzátartozó esetén az ő elérhetőségére; egyébként a beteg adatai.
        var email = intake.RequesterRole == RequesterRole.Hozzatartozo
            ? intake.RequesterEmail : patient.Email;
        var phone = intake.RequesterRole == RequesterRole.Hozzatartozo
            ? intake.RequesterPhone : patient.MobilePhone;

        if (!string.IsNullOrWhiteSpace(email))
            await _notifications.SendAsync(new NotificationMessage(NotificationKind.Email, email!, subject, body), ct);
        else if (!string.IsNullOrWhiteSpace(phone))
            await _notifications.SendAsync(new NotificationMessage(NotificationKind.Sms, phone!, subject, body), ct);
    }

    private static string HuClass(IntakeClassification c) => c switch
    {
        IntakeClassification.NeakTamogatottProbakezeles => "NEAK-támogatott próbakezelés",
        _ => "magánellátás - teljes ár"
    };
}
