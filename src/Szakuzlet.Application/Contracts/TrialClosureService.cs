using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Application.Banking;
using Szakuzlet.Application.Billing;
using Szakuzlet.Application.Common;
using Szakuzlet.Domain.Entities;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Application.Contracts;

/// <summary>Banki visszautalási adatok (SEPA).</summary>
public record RefundBankDetails(string BeneficiaryName, string Iban, string BankName);

/// <summary>
/// Próbakezelés lezárása és nyilatkozat-menedzsment (III. Modul D). Három kimenetel:
///   I.  Sikeres – vénybeváltás, TB-különbség elszámolás, visszajáró kaució kifizetése,
///   II. Hosszabbítás – az orvos meghosszabbítja a próbaidőt (nincs pénzügyi zárás),
///   III.Elutasítás – eszköz-visszavétel, bérleti díj/tisztítás levonás (méltányossági felülbírálattal),
///       az eszköz karantén/rehabilitáció státuszba kerül.
/// </summary>
public sealed class TrialClosureService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly EventRecorder _events;
    private readonly SettlementService _settlement;
    private readonly IPayoutExporter _payout;

    public TrialClosureService(IAppDbContext db, IClock clock, EventRecorder events,
        SettlementService settlement, IPayoutExporter payout)
    {
        _db = db;
        _clock = clock;
        _events = events;
        _settlement = settlement;
        _payout = payout;
    }

    /// <summary>
    /// I. kimenetel – sikeres próba: a TB-különbség kalkulátor kiszámolja a visszajárót
    /// (kaució − TB-önrész − egyéb), majd banki átutalással (SEPA export) vagy postai csekkel
    /// visszatéríti. A szerződés lezárul.
    /// </summary>
    public async Task<DepositSettlement> CloseSuccessfulAsync(Guid contractId, decimal tbSelfPart,
        decimal otherDeductions, RefundBankDetails? bank, CancellationToken ct = default)
    {
        var contract = await LoadOpenAsync(contractId, ct);
        var settlement = _settlement.BuildDepositSettlement(contract.DepositAmount, tbSelfPart, otherDeductions);

        if (settlement.Refund > 0)
            await ExportRefundAsync(contract, settlement.Refund, bank, ct);

        contract.Status = ContractStatus.Lezarva;
        _events.Audit("Contract", contract.Id.ToString(), "ProbaLezarva_Sikeres",
            $"Visszajáró: {settlement.Refund:N0} Ft (kaució {contract.DepositAmount:N0} − TB {tbSelfPart:N0} − egyéb {otherDeductions:N0}).");
        _events.Timeline(contract.PatientId, "ProbaLezarva",
            $"Próbakezelés sikeresen lezárva; visszajáró: {settlement.Refund:N0} Ft.");
        await _db.SaveChangesAsync(ct);
        return settlement;
    }

    /// <summary>
    /// II. kimenetel – orvosi próbaidő-hosszabbítás: pénzügyi bizonylatolás nélkül, az új
    /// lejárati dátum rögzítése. A szerződés nyitva marad.
    /// </summary>
    public async Task ExtendTrialAsync(Guid contractId, DateOnly newDeadline, CancellationToken ct = default)
    {
        var contract = await LoadOpenAsync(contractId, ct);
        contract.Status = ContractStatus.ProbaFut;
        _events.Audit("Contract", contract.Id.ToString(), "ProbaHosszabbitva", $"Új határidő: {newDeadline:yyyy-MM-dd}");
        _events.Timeline(contract.PatientId, "ProbaHosszabbitva",
            $"Orvosi próbaidő-hosszabbítás; új határidő: {newDeadline:yyyy-MM-dd}.");
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// III. kimenetel – terápia-elutasítás és eszköz-visszavétel. A használati napok alapján
    /// bérleti díj + kötelező tisztítási költség levonható, de a pultos méltányosságból
    /// (indoklással) csökkentheti/elengedheti. Az eszköz karantén/rehabilitáció státuszba kerül.
    /// Visszaadja a végleges elszámolást (pozitív = visszajár, 0 = nincs visszajáró).
    /// </summary>
    public async Task<DepositSettlement> CloseRejectedAsync(Guid contractId, decimal rentalFee,
        decimal cleaningFee, decimal waiverAmount, string? waiverReason, RefundBankDetails? bank,
        CancellationToken ct = default)
    {
        var contract = await LoadOpenAsync(contractId, ct);

        // Méltányossági felülbírálat: a levonások csökkenthetők (nem mehetnek 0 alá).
        var deductions = Math.Max(0, rentalFee + cleaningFee - Math.Max(0, waiverAmount));
        var settlement = _settlement.BuildDepositSettlement(contract.DepositAmount, tbSelfPart: 0, otherDeductions: deductions);

        if (waiverAmount > 0)
            _events.Audit("Contract", contract.Id.ToString(), "MeltanyossagiFelulbiralas",
                $"Elengedett: {waiverAmount:N0} Ft. Indok: {waiverReason}");

        if (settlement.Refund > 0)
            await ExportRefundAsync(contract, settlement.Refund, bank, ct);

        contract.Status = ContractStatus.Lezarva;
        _events.Audit("Contract", contract.Id.ToString(), "ProbaLezarva_Elutasitva",
            $"Levonás: {deductions:N0} Ft; visszajáró: {settlement.Refund:N0} Ft.");
        _events.Timeline(contract.PatientId, "ProbaElutasitva",
            $"Terápia elutasítva, eszköz visszavéve; visszajáró: {settlement.Refund:N0} Ft.");

        // Az eszköz karantén/rehabilitáció státuszba kerül (a V. modul szervize dolgozza fel).
        _events.Timeline(contract.PatientId, "EszkozKarantenba",
            $"Visszavett eszköz karanténba (SN: {contract.DeviceSerialNumber}) – tisztítás/rehabilitáció.");

        await _db.SaveChangesAsync(ct);
        return settlement;
    }

    private async Task ExportRefundAsync(Contract contract, decimal refund, RefundBankDetails? bank, CancellationToken ct)
    {
        var patient = await _db.Patients.FirstAsync(p => p.Id == contract.PatientId, ct);
        if (bank is not null)
        {
            await _payout.ExportSepaAsync(new PayoutItem(
                bank.BeneficiaryName, bank.Iban, bank.BankName, refund, $"Kaució visszatérítés – {contract.Number}"), ct);
        }
        else
        {
            // Nincs bankszámla -> postai csekk a beteg lakcímére.
            var address = string.Join(" ", new[] { patient.PostalCode, patient.City, patient.AddressLine }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
            await _payout.ExportPostalOrderAsync(patient.FullName, address, refund, ct);
        }
    }

    private async Task<Contract> LoadOpenAsync(Guid contractId, CancellationToken ct)
    {
        var contract = await _db.Contracts.FirstOrDefaultAsync(c => c.Id == contractId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen szerződés: {contractId}");
        if (contract.Status == ContractStatus.Lezarva)
            throw new InvalidOperationException("A szerződés már lezárva.");
        return contract;
    }
}
