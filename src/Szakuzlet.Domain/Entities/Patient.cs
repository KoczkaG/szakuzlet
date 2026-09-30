namespace Szakuzlet.Domain.Entities;

/// <summary>
/// A páciens (partner) központi törzsadata. Ez az entitás szinkronizál majd a KVL-lel;
/// a <see cref="KvlPartnerCode"/> a KVL-beli partnerkód, ami az összekötő azonosító.
/// </summary>
public class Patient
{
    public Guid Id { get; set; }

    /// <summary>KVL partnerkód (a régi ügyfelek a KVL-ben már léteznek). Null, ha még nincs KVL-párja.</summary>
    public string? KvlPartnerCode { get; set; }

    // --- Személyes törzsadatok ---
    public string FullName { get; set; } = string.Empty;

    /// <summary>Születési dátum – a régi ügyfelek diszkrét előhívásának kulcsa (feladatlista 3. pont).</summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>TAJ-szám – kötelező kontaktadat az "ADATLAP HIÁNYOS" ellenőrzéshez (I. Modul F).</summary>
    public string? TajNumber { get; set; }

    // --- Kapcsolattartási adatok (a hiányzó mezők ellenőrzésének tárgya) ---
    public string? MobilePhone { get; set; }
    public string? Email { get; set; }

    // --- Cím (intelligens cím-adatbázis tölti a települést irányítószámból) ---
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? AddressLine { get; set; }

    // --- Metaadatok ---
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>Régi (a KVL-ből importált) ügyfél-e. Befolyásolja a portál viselkedését.</summary>
    public bool IsLegacy { get; set; }

    // --- Navigációk ---
    public ICollection<DataSheet> DataSheets { get; set; } = new List<DataSheet>();
    public ICollection<ConsentRecord> Consents { get; set; } = new List<ConsentRecord>();

    /// <summary>
    /// Igaz, ha bármelyik kötelező kontaktmező (e-mail, mobil, TAJ) hiányzik.
    /// Az I. Modul F "ADATLAP HIÁNYOS" riasztás alapja.
    /// </summary>
    public bool HasMissingRequiredContact =>
        string.IsNullOrWhiteSpace(Email)
        || string.IsNullOrWhiteSpace(MobilePhone)
        || string.IsNullOrWhiteSpace(TajNumber);
}
