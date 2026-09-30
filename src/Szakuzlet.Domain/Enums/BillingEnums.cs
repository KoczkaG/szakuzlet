namespace Szakuzlet.Domain.Enums;

/// <summary>A számla vevő-kategóriája (adónem). Az átváltás nem törölheti a beteg törzsadatait.</summary>
public enum BillingCategory
{
    /// <summary>Magánszemély (a beteg saját neve/lakcíme).</summary>
    Maganszemely = 0,

    /// <summary>Belföldi jogi személy / adóalany (céges számla, adószámmal).</summary>
    BelfoldiAdoalany = 1,

    /// <summary>Egészségpénztári számla (a beteg lakcímével, EP-adatok a névmezőben).</summary>
    Egeszsegpenztar = 2
}

/// <summary>Fizetési mód. A zárási logikai szűrő ezt veti össze a tételekkel.</summary>
public enum PaymentMethod
{
    Keszpenz = 0,
    Bankkartya = 1,
    Atutalas = 2,
    PostaiUtanvet = 3
}
