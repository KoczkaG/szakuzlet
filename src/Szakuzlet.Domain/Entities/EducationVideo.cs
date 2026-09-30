namespace Szakuzlet.Domain.Entities;

/// <summary>
/// Egy termékhez (készülék vagy maszk modell) tartozó rövid oktatóvideó (III. Modul E).
/// A dinamikus, termékalapú távoktatás ebből válogatja össze a betegnek kiküldött videókat.
/// </summary>
public class EducationVideo
{
    public Guid Id { get; set; }

    /// <summary>A termék/modell neve, amelyhez a videó tartozik (pl. "AirSense 11").</summary>
    public string ProductModel { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>A videó linkje / QR mögötti hivatkozás.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Általános (több modellnél közös) videó-e, pl. szűrőtisztítás.</summary>
    public bool IsGeneric { get; set; }

    public bool IsActive { get; set; } = true;
}
