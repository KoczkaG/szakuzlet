namespace Szakuzlet.Application.Billing;

/// <summary>Céges (belföldi adóalany) vevő-adatok.</summary>
public record CompanyBilling(string Name, string TaxNumber, string Address);

/// <summary>Egészségpénztári számlázási adatok.</summary>
public record EpBilling(Guid HealthFundId, string MemberId, string? BeneficiaryName);
