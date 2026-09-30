namespace Szakuzlet.Domain.Enums;

/// <summary>
/// Egy regisztrált telefonszám típusa. A Click-to-Call MINDEN típus mellett elérhető
/// (I. Modul C, feladatlista 1. pont): beteg mobil, házi szám, valamint jogilag jóváhagyott
/// kapcsolattartó / hozzátartozó / megbízott száma.
/// </summary>
public enum PhoneKind
{
    BetegMobil = 0,
    HaziSzam = 1,
    Kapcsolattarto = 2,
    Hozzatartozo = 3,
    Megbizott = 4
}
