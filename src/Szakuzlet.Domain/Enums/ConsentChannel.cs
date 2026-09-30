namespace Szakuzlet.Domain.Enums;

/// <summary>
/// A GDPR adatlap 4 kötelező hozzájárulási kérdése (I. Modul A, 1. pont).
/// </summary>
public enum ConsentChannel
{
    /// <summary>1. kérdés: Tájékoztatás kérése a kihordási időről (lejárati értesítők).</summary>
    KihordasiIdoTajekoztatas = 1,

    /// <summary>2. kérdés: Hírlevélre / marketingre történő feliratkozás.</summary>
    Hirlevel = 2,

    /// <summary>3. kérdés: Postai úton történő küldeményhez / információhoz hozzájárulás.</summary>
    PostaiKuldemeny = 3,

    /// <summary>4. kérdés: E-mailes küldeményhez / információhoz hozzájárulás.</summary>
    EmailKuldemeny = 4
}
