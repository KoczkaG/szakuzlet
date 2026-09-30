namespace Szakuzlet.Application.DataQuality;

/// <summary>Az „ADATLAP HIÁNYOS” protokoll fix szövegei (I. Modul F).</summary>
public static class DataQualityMessages
{
    /// <summary>Beépített webshopos marketing-terelés (mindkét opció záróüzenete).</summary>
    public const string MarketingClosing =
        "Köszönjük, hogy naprakészen tartja adatait! Örömmel értesítjük, hogy megújult a " +
        "SOMNO SHOP Webáruház, ahol mostantól még gyorsabban, kényelmesebben és sorban állás " +
        "nélkül intézheti tartozék-rendeléseit vagy vénybeváltásait. Nézzen szét nálunk!";

    /// <summary>Az „A” opció GDPR adatfrissítési igazolásának törzse.</summary>
    public const string GdprConfirmationBody =
        "Tájékoztatjuk, hogy kapcsolattartási adatait az Ön szóbeli jóváhagyásával " +
        "frissítettük rendszerünkben. Az adatkezelés jogalapja az Ön hozzájárulása; " +
        "a módosítás tényét és időpontját rögzítettük.";

    /// <summary>A „B” opció önkiszolgáló adatpótló felkérésének szövege.</summary>
    public const string SelfServicePrompt =
        "Kérjük, a linkre kattintva néhány másodperc alatt pótolja a hiányzó adatait, " +
        "hogy a jövőben gyorsabban és kényelmesebben szolgálhassuk ki.";
}
