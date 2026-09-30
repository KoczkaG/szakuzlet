namespace Szakuzlet.Application.Billing;

/// <summary>Az e-számla kiállításának kérése.</summary>
public record EInvoiceRequest(string InvoiceNumber, string BuyerName, string? TaxNumber, decimal? GrossTotal);

/// <summary>Az e-számla kiállításának eredménye (hiteles PDF hivatkozás + NAV-jelentés státusz).</summary>
public record EInvoiceResult(string ExternalReference, bool NavReported);

/// <summary>
/// A hiteles e-számlázás (pl. Számlázz.hu) API absztrakciója. A számlaadatokat átküldi a külső
/// rendszernek, amely legenerálja a hiteles, digitálisan aláírt és időbélyegzett PDF-et, jelenti a
/// NAV-nak, majd visszaadja a hivatkozást. A valós API elkészültéig mock szolgálja ki.
/// </summary>
public interface IEInvoiceClient
{
    Task<EInvoiceResult> IssueAsync(EInvoiceRequest request, CancellationToken ct = default);
}
