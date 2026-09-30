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

        if (!await db.HealthFunds.AnyAsync(ct))
        {
            db.HealthFunds.AddRange(
                Fund("OTP Egészségpénztár", strict: false, null, null),
                Fund("MKB Egészségpénztár", strict: false, null, null),
                // Szigorú EP: kizárólag saját székhelyre és adószámra.
                Fund("Prémium Egészségpénztár", strict: true,
                    "1051 Budapest, Pénztár utca 5.", "18000000-2-41"));
            await db.SaveChangesAsync(ct);
        }

        await SeedEducationVideosAsync(db, ct);
    }

    private static HealthFund Fund(string name, bool strict, string? address, string? taxNumber) => new()
    {
        Id = Guid.NewGuid(), Name = name, StrictBilling = strict,
        OfficialAddress = address, TaxNumber = taxNumber, IsActive = true
    };

    // (a nyitvatartás/EP seed alá kerül, külön AnyAsync-ellenőrzéssel)
    public static async Task SeedEducationVideosAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.EducationVideos.AnyAsync(ct)) return;
        db.EducationVideos.AddRange(
            Video("AirSense 11", "AirSense 11 – üzembe helyezés", "https://video/airsense11-setup", false),
            Video("AirSense 11", "AirSense 11 – tisztítás", "https://video/airsense11-clean", false),
            Video("Orrmaszk M", "Orrmaszk felhelyezése", "https://video/nasalmask-fit", false),
            Video("*", "Baktériumszűrő felhelyezése", "https://video/filter", true),
            Video("*", "Szűrők tisztítása", "https://video/filter-clean", true));
        await db.SaveChangesAsync(ct);
    }

    private static EducationVideo Video(string model, string title, string url, bool generic) => new()
    {
        Id = Guid.NewGuid(), ProductModel = model, Title = title, Url = url,
        IsGeneric = generic, IsActive = true
    };

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
