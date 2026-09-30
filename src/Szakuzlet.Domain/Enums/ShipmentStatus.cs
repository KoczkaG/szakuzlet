namespace Szakuzlet.Domain.Enums;

/// <summary>Futárcég (GLS/MPL) csomagstátusz a Timeline-on (I. Modul D, 2. pont).</summary>
public enum ShipmentStatus
{
    Feladva = 0,
    KezbesitesAlatt = 1,
    SikertelenKezbesites = 2,
    Atveve = 3
}

/// <summary>A logisztikai partner.</summary>
public enum Courier
{
    Gls = 0,
    Mpl = 1
}
