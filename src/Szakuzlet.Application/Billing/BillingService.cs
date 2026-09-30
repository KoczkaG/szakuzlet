using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Application.Common;
using Szakuzlet.Application.Notifications;
using Szakuzlet.Domain.Entities;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Application.Billing;

/// <summary>
/// Számlázási mag és adónem-kezelés (II. Modul A). Kritikus elv: az adónem-váltás
/// (magánszemély → belföldi adóalany / EP) SOHA nem törli a beteg magánszemélyes törzsadatait –
/// a céges/EP vevő-adatok elkülönített blokkban (BillingParty) élnek.
/// </summary>
public sealed class BillingService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly EventRecorder _events;
    private readonly IInvoiceNumberGenerator _numbers;
    private readonly EanPoolService _eanPool;
    private readonly IEInvoiceClient _eInvoice;
    private readonly INotificationSender _notifications;
    private readonly ICardTerminal _terminal;

    public BillingService(IAppDbContext db, IClock clock, EventRecorder events,
        IInvoiceNumberGenerator numbers, EanPoolService eanPool, IEInvoiceClient eInvoice,
        INotificationSender notifications, ICardTerminal terminal)
    {
        _db = db;
        _clock = clock;
        _events = events;
        _numbers = numbers;
        _eanPool = eanPool;
        _eInvoice = eInvoice;
        _notifications = notifications;
        _terminal = terminal;
    }

    /// <summary>
    /// Új számla létrehozása magánszemélyként (alapértelmezett adónem). A tételek a Timeline-hoz
    /// és a későbbi kiküldéshez. Egyedi sorszám generálódik (duplikáció-mentes).
    /// </summary>
    public async Task<Invoice> CreateDraftAsync(Guid patientId, IEnumerable<(string? code, string name, int qty)> lines,
        CancellationToken ct = default)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen páciens: {patientId}");

        var now = _clock.UtcNow;
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Number = await _numbers.NextAsync(ct),
            Category = BillingCategory.Maganszemely,
            Status = InvoiceStatus.Kikuldheto,
            CreatedAtUtc = now
        };
        foreach (var (code, name, qty) in lines)
            invoice.Lines.Add(new InvoiceLine
            {
                Id = Guid.NewGuid(), InvoiceId = invoice.Id,
                ItemCode = code, ProductName = name, Quantity = qty <= 0 ? 1 : qty
            });

        _db.Invoices.Add(invoice);
        _events.Audit("Invoice", invoice.Id.ToString(), "SzamlaLetrehozva", $"Sorszám: {invoice.Number}");
        await _db.SaveChangesAsync(ct);
        return invoice;
    }

    /// <summary>
    /// Váltás céges (belföldi adóalany) számlázásra. A céges adatok elkülönített blokkba kerülnek,
    /// a beteg magánszemélyes törzsadata változatlan marad.
    /// </summary>
    public async Task SwitchToCompanyAsync(Guid invoiceId, CompanyBilling company, CancellationToken ct = default)
    {
        var invoice = await LoadEditableAsync(invoiceId, ct);
        var patient = await _db.Patients.FirstAsync(p => p.Id == invoice.PatientId, ct);
        var snapshotName = patient.FullName; // a törzsadat megőrzésének igazolása

        invoice.Category = BillingCategory.BelfoldiAdoalany;
        invoice.BillingParty = new BillingParty
        {
            Name = company.Name.Trim(),
            TaxNumber = company.TaxNumber.Trim(),
            Address = company.Address.Trim()
        };
        // EP-mezők nullázása (kizárólag a számlán, nem a betegen).
        invoice.EpName = invoice.EpMemberId = invoice.EpBeneficiaryName = null;

        _events.Audit("Invoice", invoice.Id.ToString(), "AdonemValtas_Ceges",
            $"Céges: {company.Name}; a beteg ({snapshotName}) törzsadata megőrizve.");

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Az aktív egészségpénztárak listája a legördülő menühöz.</summary>
    public Task<List<HealthFund>> GetHealthFundsAsync(CancellationToken ct = default)
        => _db.HealthFunds.Where(f => f.IsActive).OrderBy(f => f.Name).ToListAsync(ct);

    /// <summary>A számla vényesként jelölése (ekkor a lezárás EAN-kódot rendel hozzá).</summary>
    public async Task MarkPrescriptionAsync(Guid invoiceId, bool isPrescription, CancellationToken ct = default)
    {
        var invoice = await LoadEditableAsync(invoiceId, ct);
        invoice.IsPrescription = isPrescription;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Fizetési mód beállítása a kétirányú logikai zárási szűrővel (II. Modul D):
    /// a postaköltség-tétel és a fizetési mód összeférhetőségét ellenőrzi.
    /// </summary>
    public async Task SetPaymentMethodAsync(Guid invoiceId, PaymentMethod method, CancellationToken ct = default)
    {
        var invoice = await _db.Invoices.Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen számla: {invoiceId}");
        if (invoice.Status == InvoiceStatus.Kikuldve)
            throw new InvalidOperationException("Már kiküldött számla fizetési módja nem módosítható.");

        var hasPostage = invoice.Lines.Any(l =>
            l.ProductName.Contains("postaköltség", StringComparison.OrdinalIgnoreCase)
            || (l.ItemCode?.Equals("POSTA", StringComparison.OrdinalIgnoreCase) ?? false));

        var check = BillingValidator.ValidatePaymentConsistency(method, hasPostage);
        if (!check.Ok)
            throw new InvalidOperationException(check.Error);

        invoice.PaymentMethod = method;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// A számla véglegesítése / kiadása (II. Modul C – hibrid pulti számlakiadás):
    /// vényesnél a poolból EAN-sorszámot rendel és „ráégeti”, hiteles e-számlát állít ki
    /// (Számlázz.hu mock, NAV-jelentéssel), majd hibrid módon kézbesíti:
    ///   - ha a betegnek van e-mailje: digitálisan küldi (nincs nyomtatás),
    ///   - egyébként engedélyezi az egy ügyfél-példányos helyszíni nyomtatást.
    /// Visszaadja, hogy nyomtatni kell-e helyben (idős/e-mail nélküli ügyfél).
    /// </summary>
    public async Task<bool> FinalizeAsync(Guid invoiceId, decimal? cardAmount = null, CancellationToken ct = default)
    {
        var invoice = await _db.Invoices.Include(i => i.Patient)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen számla: {invoiceId}");
        if (invoice.Status == InvoiceStatus.Kikuldve)
            throw new InvalidOperationException("A számla már kiküldve/véglegesítve.");

        // Terminál limit-védőháló: bankkártyás fizetésnél előbb a banki jóváhagyás.
        // Sikertelen fizetésnél NEM keletkezik NAV-számla, a folyamat blokkolódik.
        if (invoice.PaymentMethod == PaymentMethod.Bankkartya && cardAmount is { } amount)
        {
            var charge = await _terminal.ChargeAsync(amount, ct);
            if (!charge.Approved)
            {
                _events.Audit("Invoice", invoice.Id.ToString(), "KartyasFizetesSikertelen",
                    charge.DeclineReason);
                throw new InvalidOperationException(
                    $"Kártyás fizetés sikertelen: {charge.DeclineReason} A számla nem került kiállításra.");
            }
        }

        // Vényes: automata EAN-kód kiosztás és ráégetés.
        if (invoice.IsPrescription && invoice.AssignedEanCode is null)
        {
            invoice.AssignedEanCode = await _eanPool.AssignNextAsync(invoice.Id, ct);
            _events.Audit("Invoice", invoice.Id.ToString(), "EanKodRaegetve", invoice.AssignedEanCode);
        }

        // Hiteles e-számla kiállítása (mock).
        var buyerName = invoice.BillingParty?.Name ?? invoice.Patient.FullName;
        var result = await _eInvoice.IssueAsync(
            new EInvoiceRequest(invoice.Number, buyerName, invoice.BillingParty?.TaxNumber, null), ct);
        invoice.EInvoiceReference = result.ExternalReference;

        var patient = invoice.Patient;
        var printNeeded = string.IsNullOrWhiteSpace(patient.Email);

        if (!printNeeded)
        {
            // Digitális ügyfél: e-mailen kiküldjük, nincs papírnyomtatás.
            await _notifications.SendAsync(new NotificationMessage(
                NotificationKind.Email, patient.Email!,
                "Számlája a SOMNO SHOP-tól",
                "Mellékeljük digitális számláját. A könyvelés a háttérben automatikusan megkapja."), ct);
        }

        invoice.Status = InvoiceStatus.Kikuldve;
        invoice.SentAtUtc = _clock.UtcNow;

        // Kihordási idő CRM követés: a tényleges eladási dátum a kihordási automatizmus
        // kiindulópontja (II. Modul E). A WearExpiryService ebből számol lejáratot.
        patient.LastPurchaseDate = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);

        _events.Audit("Invoice", invoice.Id.ToString(), "SzamlaVeglegesitve",
            $"E-számla: {result.ExternalReference}; NAV: {result.NavReported}; nyomtatás: {printNeeded}");
        _events.Timeline(patient.Id, "SzamlaKiallitva",
            $"Számla {invoice.Number} kiállítva" +
            (invoice.AssignedEanCode is not null ? $" (EAN: {invoice.AssignedEanCode})" : "") + ".");

        await _db.SaveChangesAsync(ct);
        return printNeeded;
    }

    /// <summary>
    /// 1 kattintásos utólagos e-számlaküldés (próbaidőszak utáni elszámolásnál). A már kiállított
    /// e-számla PDF-jét sablonüzenettel kilövi a beteg e-mailjére – külső levelező nélkül.
    /// </summary>
    public async Task SendEInvoiceByEmailAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var invoice = await _db.Invoices.Include(i => i.Patient)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen számla: {invoiceId}");
        if (string.IsNullOrWhiteSpace(invoice.Patient.Email))
            throw new InvalidOperationException("A betegnek nincs e-mail címe.");
        if (invoice.EInvoiceReference is null)
            throw new InvalidOperationException("Ehhez a számlához még nincs kiállított e-számla.");

        await _notifications.SendAsync(new NotificationMessage(
            NotificationKind.Email, invoice.Patient.Email!,
            "Számlája a SOMNO SHOP-tól",
            "Mellékeljük a próbaidőszak elszámolásáról szóló digitális számláját."), ct);
        _events.Audit("Invoice", invoice.Id.ToString(), "UtolagosESzamlaKikuldve");
    }

    /// <summary>Visszaváltás magánszemélyre. A céges/EP blokk törlődik a számláról, a beteg adatai érintetlenek.</summary>
    public async Task SwitchToPrivateAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var invoice = await LoadEditableAsync(invoiceId, ct);
        invoice.Category = BillingCategory.Maganszemely;
        invoice.BillingParty = null;
        invoice.EpName = invoice.EpMemberId = invoice.EpBeneficiaryName = null;
        _events.Audit("Invoice", invoice.Id.ToString(), "AdonemValtas_Maganszemely");
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Váltás egészségpénztári (EP) számlázásra (II. Modul B). A rendszer automatikusan összefűzi
    /// a szigorú, hierarchikus névsorrendet a NÉV mezőbe (nincs manuális gépelés). Szigorú EP esetén
    /// „Riasztó/Zászló”: a rendszer a számlázási kategóriát adóalanyra állítja, és beemeli az EP
    /// hivatalos székhelyét és adószámát – miközben a beteg profilja érintetlen marad.
    /// Visszaadja, hogy szigorú EP-t választottak-e (a pultnak megjelenítendő figyelmeztetéshez).
    /// </summary>
    public async Task<bool> SwitchToEpAsync(Guid invoiceId, EpBilling ep, CancellationToken ct = default)
    {
        var invoice = await LoadEditableAsync(invoiceId, ct);
        var patient = await _db.Patients.FirstAsync(p => p.Id == invoice.PatientId, ct);
        var fund = await _db.HealthFunds.FirstOrDefaultAsync(f => f.Id == ep.HealthFundId, ct)
            ?? throw new InvalidOperationException("Nincs ilyen egészségpénztár.");

        invoice.EpName = fund.Name;
        invoice.EpMemberId = ep.MemberId.Trim();
        invoice.EpBeneficiaryName = string.IsNullOrWhiteSpace(ep.BeneficiaryName) ? null : ep.BeneficiaryName.Trim();

        // A számlán megjelenő NÉV mező automata, hierarchikus összefűzése.
        var billingName = ComposeEpName(patient.FullName, fund.Name, invoice.EpBeneficiaryName, invoice.EpMemberId);

        if (fund.StrictBilling)
        {
            // Szigorú EP: adóalany kategória + EP hivatalos székhely/adószám a vevő adataihoz.
            invoice.Category = BillingCategory.BelfoldiAdoalany;
            invoice.BillingParty = new BillingParty
            {
                Name = billingName,
                TaxNumber = fund.TaxNumber,
                Address = fund.OfficialAddress
            };
            _events.Audit("Invoice", invoice.Id.ToString(), "EpValtas_Szigoru",
                $"Szigorú EP: {fund.Name}; székhely+adószám beemelve, beteg profilja megőrizve.");
        }
        else
        {
            // Könnyített EP: a beteg saját lakcímére állítjuk ki, csak a NÉV mező tartalmazza az EP-adatokat.
            invoice.Category = BillingCategory.Egeszsegpenztar;
            invoice.BillingParty = new BillingParty
            {
                Name = billingName,
                TaxNumber = null,
                Address = ComposePatientAddress(patient)
            };
            _events.Audit("Invoice", invoice.Id.ToString(), "EpValtas_Konnyitett",
                $"Könnyített EP: {fund.Name}; a beteg lakcímére kiállítva.");
        }

        await _db.SaveChangesAsync(ct);
        return fund.StrictBilling;
    }

    /// <summary>
    /// A számlán megjelenő EP-névmező szigorú, hierarchikus összefűzése:
    /// [Beteg Neve] / [EP Neve] (Kedvezményezett: [Név] / Tagi azonosító: [Szám]
    /// </summary>
    public static string ComposeEpName(string patientName, string epName, string? beneficiary, string? memberId)
    {
        var beneficiaryPart = string.IsNullOrWhiteSpace(beneficiary) ? "" : $"Kedvezményezett: {beneficiary} / ";
        return $"{patientName} / {epName} ({beneficiaryPart}Tagi azonosító: {memberId}";
    }

    private static string ComposePatientAddress(Patient p)
        => string.Join(" ", new[] { p.PostalCode, p.City, p.AddressLine }.Where(s => !string.IsNullOrWhiteSpace(s)));

    private async Task<Invoice> LoadEditableAsync(Guid invoiceId, CancellationToken ct)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen számla: {invoiceId}");
        if (invoice.Status is InvoiceStatus.Kikuldve)
            throw new InvalidOperationException("Már kiküldött számla adóneme nem módosítható.");
        return invoice;
    }
}
