using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Application.Contracts;

/// <summary>Egy próbakezeléses szerződés előkészítésének adatai.</summary>
public record ContractDraftInput(
    ServiceChannel Channel,
    string DeviceModel,
    string DeviceSerialNumber,
    string? MaskModel,
    decimal? PressureCmH2O,
    decimal DepositAmount);

/// <summary>Az előkészített szerződés összefoglalója (a kétoldali monitorhoz / előnézethez).</summary>
public record ContractSummary(
    Guid ContractId,
    string Number,
    string PatientName,
    string DeviceModel,
    string DeviceSerialNumber,
    decimal DepositAmount,
    string? PdfReference);
