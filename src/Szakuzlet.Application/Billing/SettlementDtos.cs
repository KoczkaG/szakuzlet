namespace Szakuzlet.Application.Billing;

/// <summary>
/// Kaució-elszámoló adatlap: átlátható matematikai kimutatás a betegnek (II. Modul E).
/// A visszajáró = befizetett kaució − a TB-önrész és egyéb levonások.
/// </summary>
public record DepositSettlement(
    decimal DepositPaid,
    decimal TbSelfPart,
    decimal OtherDeductions,
    decimal Refund,
    string CommitmentText);

/// <summary>Egy terméktípus napi OEP darabszáma a KVL szerint, összevetve a Mankó összesítővel.</summary>
public record OepLine(string ProductName, int KvlCount, int? MankoCount)
{
    /// <summary>Van-e eltérés (ha a Mankó darabszám meg van adva).</summary>
    public bool HasDiscrepancy => MankoCount is { } m && m != KvlCount;
}

/// <summary>A napi OEP egyeztető eredménye.</summary>
public record OepReconciliation(DateOnly Date, IReadOnlyList<OepLine> Lines)
{
    public bool AnyDiscrepancy => Lines.Any(l => l.HasDiscrepancy);
}
