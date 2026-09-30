namespace Szakuzlet.Application.Kvl;

/// <summary>
/// A KVL vállalatirányítási rendszer partner-adatainak leképezése.
/// Ez a szerződés a saját rendszerünk és a KVL API-ja között; a mezők a KVL
/// jövőbeli végpontjaihoz igazíthatók anélkül, hogy a Domain modell változna.
/// </summary>
public record KvlPartnerDto
{
    public string? PartnerCode { get; init; }
    public string FullName { get; init; } = string.Empty;
    public DateOnly? DateOfBirth { get; init; }
    public string? TajNumber { get; init; }
    public string? MobilePhone { get; init; }
    public string? Email { get; init; }
    public string? PostalCode { get; init; }
    public string? City { get; init; }
    public string? AddressLine { get; init; }
}
