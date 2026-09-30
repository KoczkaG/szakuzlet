namespace Szakuzlet.Domain.Enums;

/// <summary>
/// A hívás fő oka(i). Több is jelölhető egy híváshoz (gyors checkboxok).
/// Flags, hogy egyetlen mezőben tárolható legyen több témakör (I. Modul E, 2. pont).
/// </summary>
[Flags]
public enum CallTopic
{
    None = 0,
    UjUgyfelInfo = 1 << 0,
    RendelesLeadas = 1 << 1,
    Venybevaltas = 1 << 2,
    MaszkbeallitasTerapiasKerdes = 1 << 3,
    SzamlazasPenzugy = 1 << 4,
    SzervizGarancia = 1 << 5,
    Panaszkezeles = 1 << 6
}
