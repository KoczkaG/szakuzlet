using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Application.Billing;
using Szakuzlet.Application.Common;
using Szakuzlet.Application.Documents;
using Szakuzlet.Application.Signing;
using Szakuzlet.Domain.Entities;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Application.Contracts;

/// <summary>
/// Próbakezeléses szerződéskötés magja (III. Modul A). Egypontos adatbevitel (OCR-beemeléssel),
/// PDF-szerződés + jótállási jegy generálás Word-sablonok nélkül, kaució + baktériumszűrő
/// számlázás, valamint eIDAS SMS-kódos vagy papír-szkennelt hibrid aláírás jogi bizonyítékkal.
/// </summary>
public sealed class ContractService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly EventRecorder _events;
    private readonly IDocumentGenerator _documents;
    private readonly ISignatureService _signature;
    private readonly BillingService _billing;

    /// <summary>A próbakezeléshez kötelező baktériumszűrők száma.</summary>
    private const int BacterialFilterCount = 2;

    public ContractService(IAppDbContext db, IClock clock, EventRecorder events,
        IDocumentGenerator documents, ISignatureService signature, BillingService billing)
    {
        _db = db;
        _clock = clock;
        _events = events;
        _documents = documents;
        _signature = signature;
        _billing = billing;
    }

    /// <summary>
    /// Szerződés előkészítése: rögzíti az eszköz/terápiás adatokat, számlázza a baktériumszűrőt,
    /// bevételezi a kauciót (a II. Modul számlázójával), majd legenerálja a szerződés-PDF-et.
    /// </summary>
    public async Task<ContractSummary> PrepareAsync(Guid patientId, ContractDraftInput input,
        CancellationToken ct = default)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen páciens: {patientId}");

        var now = _clock.UtcNow;
        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Number = $"SZ-{now.Year}-{now.Ticks % 1_000_000:D6}",
            Channel = input.Channel,
            Status = ContractStatus.Elokeszitve,
            DeviceModel = input.DeviceModel,
            DeviceSerialNumber = input.DeviceSerialNumber,
            MaskModel = input.MaskModel,
            PressureCmH2O = input.PressureCmH2O,
            DepositAmount = input.DepositAmount,
            CreatedAtUtc = now
        };
        _db.Contracts.Add(contract);
        await _db.SaveChangesAsync(ct);

        // Baktériumszűrő számlázása (kötelező 2 db) a II. Modul számlázójával.
        var invoice = await _billing.CreateDraftAsync(patient.Id,
            new[] { ((string?)"BAKT", "Baktériumszűrő", BacterialFilterCount) }, ct);
        contract.DepositInvoiceId = invoice.Id;

        // Szerződés-PDF generálása (Word-sablon nélkül).
        var pdf = await _documents.GenerateContractPdfAsync(contract.Id, ct);
        contract.PdfReference = pdf.Reference;

        _events.Audit("Contract", contract.Id.ToString(), "SzerzodesElokeszitve",
            $"Eszköz: {contract.DeviceModel} (SN: {contract.DeviceSerialNumber}); kaució: {contract.DepositAmount}");
        _events.Timeline(patient.Id, "SzerzodesElokeszitve",
            $"Próbakezeléses szerződés előkészítve ({contract.Number}, {contract.DeviceModel}).");
        await _db.SaveChangesAsync(ct);

        return new ContractSummary(contract.Id, contract.Number, patient.FullName,
            contract.DeviceModel, contract.DeviceSerialNumber, contract.DepositAmount, contract.PdfReference);
    }

    /// <summary>eIDAS SMS-kód kiküldése az aláíráshoz (a beteg vagy hozzátartozó számára).</summary>
    public async Task<SignatureChallenge> StartSmsSignatureAsync(Guid contractId, string phoneNumber,
        CancellationToken ct = default)
    {
        _ = await LoadPreparedAsync(contractId, ct);
        return await _signature.SendCodeAsync(phoneNumber, ct);
    }

    /// <summary>
    /// Az SMS-kód ellenőrzése és a szerződés digitális lezárása (eIDAS). Sikeres hitelesítéskor
    /// a szerződés aláírttá válik, a jótállási jegy generálódik, a jogi bizonyíték rögzül.
    /// </summary>
    public async Task<bool> CompleteSmsSignatureAsync(Guid contractId, string challengeId, string code,
        string? ip, CancellationToken ct = default)
    {
        var contract = await LoadPreparedAsync(contractId, ct);
        var verification = await _signature.VerifyAsync(challengeId, code, ct);
        if (!verification.Success)
            return false;

        await FinalizeSignatureAsync(contract, SignatureMethod.SmsKod, verification.SignedAtUtc, ip, ct);
        return true;
    }

    /// <summary>
    /// Papíralapú (fizikai) aláírás hibrid ága: a beteg tollal aláírja, a pultos beszkenneli.
    /// A beszkennelt példány hivatkozása a szerződéshez kötődik, a szerződés aláírttá válik.
    /// </summary>
    public async Task SignOnPaperAsync(Guid contractId, string scannedReference, string? actor,
        CancellationToken ct = default)
    {
        var contract = await LoadPreparedAsync(contractId, ct);
        contract.ScannedReference = scannedReference;
        await FinalizeSignatureAsync(contract, SignatureMethod.PapirSzkennelt, _clock.UtcNow, null, ct, actor);
    }

    private async Task FinalizeSignatureAsync(Contract contract, SignatureMethod method,
        DateTimeOffset signedAt, string? ip, CancellationToken ct, string? actor = null)
    {
        contract.Status = ContractStatus.Alairva;
        contract.SignatureMethod = method;
        contract.SignedAtUtc = signedAt;
        contract.SignedFromIp = ip;

        // Jótállási jegy generálása (ráégetett garanciaévekkel).
        var warranty = await _documents.GenerateWarrantyAsync(contract.Id, contract.DeviceSerialNumber, ct);

        _events.Audit("Contract", contract.Id.ToString(), $"SzerzodesAlairva_{method}",
            $"Jótállás: {warranty.Reference}", actor ?? "PACIENS", ip);
        _events.Timeline(contract.PatientId, "SzerzodesAlairva",
            $"Szerződés aláírva ({method}) – {contract.Number}.");

        await _db.SaveChangesAsync(ct);
    }

    private async Task<Contract> LoadPreparedAsync(Guid contractId, CancellationToken ct)
    {
        var contract = await _db.Contracts.FirstOrDefaultAsync(c => c.Id == contractId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen szerződés: {contractId}");
        if (contract.Status == ContractStatus.Alairva || contract.Status == ContractStatus.Lezarva)
            throw new InvalidOperationException("A szerződés már aláírva/lezárva.");
        return contract;
    }
}
