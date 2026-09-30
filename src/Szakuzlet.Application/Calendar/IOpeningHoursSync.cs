namespace Szakuzlet.Application.Calendar;

/// <summary>Egy nap tényleges nyitvatartása a szinkronizált csatornák számára.</summary>
public record DayOpening(DateOnly Date, bool IsClosed, TimeOnly? OpensAt, TimeOnly? ClosesAt, string? Note);

/// <summary>
/// A nyitvatartás kifelé történő szinkronizálásának absztrakciója (webshop „Kapcsolat” oldal,
/// Google Cégem / Google Térkép). A valós API-k (Google Business Profile, webshop) elkészültéig
/// mock implementáció naplózza a szinkront. A központi KVL Naptár az egyetlen igazságforrás.
/// </summary>
public interface IOpeningHoursSync
{
    /// <summary>A megadott (általában közeljövőbeli) napok nyitvatartásának kiküldése a külső csatornákra.</summary>
    Task SyncAsync(IReadOnlyList<DayOpening> days, CancellationToken ct = default);
}
