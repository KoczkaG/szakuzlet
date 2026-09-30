namespace Szakuzlet.Application.Documents;

/// <summary>Egy generált dokumentum hivatkozása a biztonságos tárhelyen.</summary>
public record GeneratedDocument(string Reference);

/// <summary>
/// Dokumentum-generálás absztrakciója (III. Modul A). A szerződés-PDF-et és a jótállási jegyet
/// a KVL adataiból, emberi beavatkozás nélkül generálja – Word-sablonok nélkül. A valós PDF-motor
/// elkészültéig mock szolgálja ki (hivatkozást ad vissza).
/// </summary>
public interface IDocumentGenerator
{
    /// <summary>Hivatalos szerződés-PDF generálása egy szerződéshez.</summary>
    Task<GeneratedDocument> GenerateContractPdfAsync(Guid contractId, CancellationToken ct = default);

    /// <summary>Jótállási jegy generálása gyári szám alapján (ráégetett garanciaévekkel).</summary>
    Task<GeneratedDocument> GenerateWarrantyAsync(Guid contractId, string serialNumber, CancellationToken ct = default);
}
