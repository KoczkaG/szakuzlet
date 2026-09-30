namespace Szakuzlet.Application.Banking;

/// <summary>Egy visszautalási (kifizetési) tétel.</summary>
public record PayoutItem(string BeneficiaryName, string Iban, string BankName, decimal Amount, string Note);

/// <summary>
/// A visszajáró összegek kifizetésének absztrakciója (III. Modul D). Banki átutalásnál
/// SEPA/XML tömeges exportba gyűjti a tételeket; postai (csekkes) kifizetésnél a Magyar Posta
/// elektronikus utalvány-interfészébe. A valós integrációk elkészültéig mock szolgálja ki.
/// </summary>
public interface IPayoutExporter
{
    /// <summary>Banki átutalási tétel hozzáadása a SEPA/XML exporthoz.</summary>
    Task ExportSepaAsync(PayoutItem item, CancellationToken ct = default);

    /// <summary>Postai (csekkes) kifizetés beküldése a Magyar Posta interfészébe.</summary>
    Task ExportPostalOrderAsync(string beneficiaryName, string address, decimal amount, CancellationToken ct = default);
}
