namespace Szakuzlet.Domain.Enums;

/// <summary>A próbakezeléses szerződés életciklusának állapota.</summary>
public enum ContractStatus
{
    /// <summary>Előkészítve (adatok rögzítve), aláírásra vár.</summary>
    Elokeszitve = 0,

    /// <summary>Aláírva (eIDAS SMS-kód vagy papír-szkennelt), jogilag lezárt.</summary>
    Alairva = 1,

    /// <summary>Próbaidőszak fut (a kézbesítéstől / átadástól).</summary>
    ProbaFut = 2,

    /// <summary>Lezárva (sikeres vásárlás, elutasítás vagy hosszabbítás után véglegesítve).</summary>
    Lezarva = 3
}

/// <summary>Az aláírás módja (III. Modul A).</summary>
public enum SignatureMethod
{
    /// <summary>eIDAS-konform SMS-kódos digitális aláírás (alapértelmezett).</summary>
    SmsKod = 0,

    /// <summary>Papíralapú, tollal aláírt, majd beszkennelt hibrid.</summary>
    PapirSzkennelt = 1
}

/// <summary>Az ügymenet csatornája (személyes / expressz / postai).</summary>
public enum ServiceChannel
{
    SzemelyesPult = 0,
    ExpresszAtvetel = 1,
    PostaiKiszallitas = 2
}
