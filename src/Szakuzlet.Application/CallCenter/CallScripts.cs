namespace Szakuzlet.Application.CallCenter;

/// <summary>
/// A kimenő híváshoz tartozó, jogilag ellenőrzött pulti sablonszövegek (I. Modul C, 4. és 6. pont).
/// </summary>
public static class CallScripts
{
    /// <summary>A kezelő által kötelezően bemondandó GDPR sablon a kimenő hívás elején.</summary>
    public const string GdprPrompt =
        "Tájékoztatom, hogy a hívást minőségbiztosítási okokból rögzítjük. " +
        "Amennyiben ehhez nem járul hozzá, kérem jelezze, és azonnal leállítom a felvételt!";

    /// <summary>Belső érvkészlet a gyanakvás kezelésére, ha a beteg megkérdőjelezi a rögzítést.</summary>
    public const string SuspicionScript =
        "Teljesen megértem az óvatosságát, a mai világban Önnek teljesen igaza van, hogy rákérdezett! " +
        "Hadd nyugtassam meg: mi a SOMNO SHOP szaküzletéből keressük a folyamatban lévő ügye miatt, " +
        "és a hívásrögzítés valójában az Ön biztonságát szolgálja. Ha Ön most telefonon egyeztet velünk " +
        "egy maszkot, méretet vagy szervizbeállítást, a felvétel garantálja, hogy pontosan azt teljesítsük, " +
        "amit kért. Ez egy esetleges félreértésnél az Ön legfőbb védelme, hiszen így a szavait bármikor " +
        "vissza tudjuk keresni. Hozzájárul így a rögzítéshez, vagy inkább állítsam le a felvételt és " +
        "beszéljünk privát vonalon?";
}
