namespace Szakuzlet.Application.Abstractions;

/// <summary>
/// Egyedi számlasorszám generálása. A duplikáció-mentesség garantálja, hogy a postai
/// utánvétes folyamatoknál se keletkezzenek fantomszámlák (II. Modul A).
/// </summary>
public interface IInvoiceNumberGenerator
{
    Task<string> NextAsync(CancellationToken ct = default);
}
