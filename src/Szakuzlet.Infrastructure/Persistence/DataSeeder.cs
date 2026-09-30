using Microsoft.EntityFrameworkCore;
using Szakuzlet.Domain.Entities;

namespace Szakuzlet.Infrastructure.Persistence;

/// <summary>
/// Alap törzsadatok feltöltése induláskor. Jelenleg az alapértelmezett heti nyitvatartás
/// (a spec szerint: H-Sze 8-17, Cs 8-18, P 8-16, Szo-V zárva).
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (!await db.BusinessHours.AnyAsync(ct))
        {
            TimeOnly at(int h) => new(h, 0);
            db.BusinessHours.AddRange(
                Open(DayOfWeek.Monday, at(8), at(17)),
                Open(DayOfWeek.Tuesday, at(8), at(17)),
                Open(DayOfWeek.Wednesday, at(8), at(17)),
                Open(DayOfWeek.Thursday, at(8), at(18)),
                Open(DayOfWeek.Friday, at(8), at(16)),
                Closed(DayOfWeek.Saturday),
                Closed(DayOfWeek.Sunday));
            await db.SaveChangesAsync(ct);
        }

        if (!await db.ReferralSources.AnyAsync(ct))
        {
            db.ReferralSources.AddRange(
                Referral("Városi Kórház Alváslabor", "Dr. Kis Péter"),
                Referral("Fővárosi Tüdőgyógyászat", "Dr. Nagy Éva"),
                Referral("Megyei Kórház Alvásközpont", null),
                Referral("Magánrendelő – SomnoMed", "Dr. Tóth Gábor"));
            await db.SaveChangesAsync(ct);
        }
    }

    private static ReferralSource Referral(string name, string? doctor) => new()
    {
        Id = Guid.NewGuid(), Name = name, DoctorName = doctor, IsActive = true
    };

    private static BusinessHour Open(DayOfWeek day, TimeOnly opens, TimeOnly closes) => new()
    {
        Id = Guid.NewGuid(), Day = day, IsClosed = false, OpensAt = opens, ClosesAt = closes
    };

    private static BusinessHour Closed(DayOfWeek day) => new()
    {
        Id = Guid.NewGuid(), Day = day, IsClosed = true
    };
}
