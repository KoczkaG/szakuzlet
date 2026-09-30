using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Application.Billing;

/// <summary>Egy validációs eredmény (sikeres, vagy hibaüzenettel).</summary>
public record ValidationResult(bool Ok, string? Error)
{
    public static readonly ValidationResult Success = new(true, null);
    public static ValidationResult Fail(string error) => new(false, error);
}

/// <summary>
/// Pulti védőháló – kód-szintű elírás-gátló és kétirányú zárási szűrő (II. Modul D).
/// Tiszta, tesztelhető validációs logika; a pulti UI és a lezárás ezt hívja.
/// </summary>
public static class BillingValidator
{
    /// <summary>Vénykód: szigorúan „27”-tel kezdődik.</summary>
    public static ValidationResult ValidatePrescriptionCode(string? code)
        => Starts(code, "27", "Vénykód / vény száma");

    /// <summary>Matrica egyedi azonosító / gyári szám: szigorúan „21”-gyel kezdődik.</summary>
    public static ValidationResult ValidateStickerCode(string? code)
        => Starts(code, "21", "Egyedi azonosító / gyári szám");

    /// <summary>Hatósági orvoskód: szigorúan „99”-cel kezdődik.</summary>
    public static ValidationResult ValidateDoctorCode(string? code)
        => Starts(code, "99", "Orvoskód");

    /// <summary>
    /// Ctrl+V duplikáció-szűrő: ugyanaz a kód nem szerepelhet kétszer (pl. vágólapon maradt adat).
    /// </summary>
    public static ValidationResult ValidateNoDuplicate(IEnumerable<string> codes)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in codes)
        {
            if (string.IsNullOrWhiteSpace(c)) continue;
            if (!seen.Add(c.Trim()))
                return ValidationResult.Fail($"Duplikált kód: {c.Trim()} (vágólap-hiba?).");
        }
        return ValidationResult.Success;
    }

    /// <summary>
    /// Kétirányú logikai zárási szűrő a fizetési mód és a postaköltség-tétel összeférhetőségére:
    ///   - ha van postaköltség tétel: csak Utánvét/Átutalás engedélyezett,
    ///   - ha Postai utánvét a mód: kötelező a postaköltség tétel.
    /// </summary>
    public static ValidationResult ValidatePaymentConsistency(PaymentMethod method, bool hasPostageLine)
    {
        if (hasPostageLine && method is PaymentMethod.Keszpenz or PaymentMethod.Bankkartya)
            return ValidationResult.Fail(
                "Postaköltség tétel esetén csak Utánvét vagy Átutalás választható.");

        if (method == PaymentMethod.PostaiUtanvet && !hasPostageLine)
            return ValidationResult.Fail(
                "HIÁNYZÓ TÉTEL: Postai utánvétes fizetésnél a postaköltséget kötelező hozzáadni a számlához!");

        return ValidationResult.Success;
    }

    private static ValidationResult Starts(string? code, string prefix, string fieldName)
    {
        var c = code?.Trim() ?? "";
        if (c.Length == 0)
            return ValidationResult.Fail($"{fieldName}: a kód megadása kötelező.");
        if (!c.StartsWith(prefix))
            return ValidationResult.Fail($"{fieldName}: a kódnak „{prefix}”-tel kell kezdődnie.");
        return ValidationResult.Success;
    }
}
