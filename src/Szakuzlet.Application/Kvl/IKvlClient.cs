namespace Szakuzlet.Application.Kvl;

/// <summary>
/// A KVL vállalatirányítási rendszerrel folytatott kommunikáció absztrakciója.
///
/// A KVL fejlesztők jelenleg csak API végpontokat tudnak létrehozni. Amíg ezek nem
/// állnak rendelkezésre, egy mock implementáció szolgálja ki ezt az interfészt.
/// Amint a valós végpontok elkészülnek, elég egy HTTP-alapú implementációt bekötni
/// – az alkalmazás többi része változatlan marad.
/// </summary>
public interface IKvlClient
{
    /// <summary>
    /// Régi ügyfél keresése születési dátum alapján (I. Modul A, feladatlista 3. pont).
    /// Több találat is lehet (névazonosság), ezt a hívó kezeli.
    /// </summary>
    Task<IReadOnlyList<KvlPartnerDto>> FindPartnersByDateOfBirthAsync(
        DateOnly dateOfBirth, CancellationToken ct = default);

    /// <summary>Partner keresése KVL partnerkód alapján.</summary>
    Task<KvlPartnerDto?> GetPartnerByCodeAsync(string partnerCode, CancellationToken ct = default);

    /// <summary>
    /// A frissített törzsadatok és hozzájárulások visszaírása a KVL-be
    /// (I. Modul A, feladatlista 5-6. pont: azonnali profilfrissítés).
    /// Visszaadja a KVL partnerkódot (új partnernél a KVL által generáltat).
    /// </summary>
    Task<string> UpsertPartnerAsync(KvlPartnerDto partner, CancellationToken ct = default);
}
