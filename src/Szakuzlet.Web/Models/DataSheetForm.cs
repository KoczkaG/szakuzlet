using System.ComponentModel.DataAnnotations;
using Szakuzlet.Application.DataSheets;

namespace Szakuzlet.Web.Models;

/// <summary>Az adatlap-kitöltő Blazor űrlap kötési modellje, validációval.</summary>
public class DataSheetForm
{
    [Required(ErrorMessage = "A név megadása kötelező.")]
    [StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "A születési dátum megadása kötelező.")]
    public DateTime? DateOfBirth { get; set; }

    [StringLength(20)]
    public string? TajNumber { get; set; }

    [Required(ErrorMessage = "A mobiltelefonszám megadása kötelező.")]
    [StringLength(30)]
    public string? MobilePhone { get; set; }

    [EmailAddress(ErrorMessage = "Érvénytelen e-mail cím.")]
    [StringLength(256)]
    public string? Email { get; set; }

    [StringLength(10)]
    public string? PostalCode { get; set; }

    [StringLength(120)]
    public string? City { get; set; }

    [StringLength(300)]
    public string? AddressLine { get; set; }

    // A 4 kötelező hozzájárulási kérdés.
    public bool ConsentKihordasiIdo { get; set; }
    public bool ConsentHirlevel { get; set; }
    public bool ConsentPostai { get; set; }
    public bool ConsentEmail { get; set; }

    public DataSheetInput ToInput() => new()
    {
        FullName = FullName.Trim(),
        DateOfBirth = DateOfBirth is { } d ? DateOnly.FromDateTime(d) : null,
        TajNumber = TajNumber,
        MobilePhone = MobilePhone,
        Email = Email,
        PostalCode = PostalCode,
        City = City,
        AddressLine = AddressLine,
        Consents = new ConsentAnswers(
            ConsentKihordasiIdo, ConsentHirlevel, ConsentPostai, ConsentEmail)
    };
}
