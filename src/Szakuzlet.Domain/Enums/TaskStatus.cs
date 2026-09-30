namespace Szakuzlet.Domain.Enums;

/// <summary>Egy pulti/belső feladat állapota.</summary>
public enum PatientTaskStatus
{
    Nyitott = 0,
    Lezart = 1
}

/// <summary>A pulti feladat típusa.</summary>
public enum PatientTaskType
{
    /// <summary>„Zéró hozzájárulás” radar által generált tisztázó telefonhívás.</summary>
    ZeroHozzajarulasTisztazas = 0,

    /// <summary>Egyéb, kézzel vagy más folyamat által generált teendő.</summary>
    Egyeb = 100
}
