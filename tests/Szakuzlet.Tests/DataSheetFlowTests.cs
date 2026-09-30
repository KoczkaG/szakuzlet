using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.DataSheets;
using Szakuzlet.Application.Patients;
using Szakuzlet.Domain.Enums;
using Szakuzlet.Infrastructure.Kvl;

namespace Szakuzlet.Tests;

public class DataSheetFlowTests
{
    private static (DataSheetService sheets, PatientLookupService lookup, FakeClock clock, MockKvlClient kvl, TestDb db)
        Build()
    {
        var db = new TestDb();
        var clock = new FakeClock();
        var kvl = new MockKvlClient();
        var tokens = new FakeTokenGenerator();
        var events = new Szakuzlet.Application.Common.EventRecorder(db.Context, clock);
        var sheets = new DataSheetService(db.Context, clock, tokens, kvl, events);
        var lookup = new PatientLookupService(db.Context, kvl, clock);
        return (sheets, lookup, clock, kvl, db);
    }

    [Fact]
    public async Task Lookup_azonos_datummal_nevazonossagot_ad_vissza()
    {
        var (_, lookup, _, _, db) = Build();
        using var _db = db;

        // 1956-03-22: két seed partner (Kovács Lajosné, Nagy Sándor).
        var result = await lookup.FindByDateOfBirthAsync(new DateOnly(1956, 3, 22));

        Assert.True(result.IsAmbiguous);
        Assert.Equal(2, result.Matches.Count);
        // Az egyik (Nagy Sándor) hiányos kontaktadatokkal rendelkezik.
        Assert.Contains(result.Matches, m => m.HasMissingContact);
    }

    [Fact]
    public async Task Lookup_ismeretlen_datum_ures_eredmenyt_ad()
    {
        var (_, lookup, _, _, db) = Build();
        using var _db = db;

        var result = await lookup.FindByDateOfBirthAsync(new DateOnly(1900, 1, 1));

        Assert.True(result.IsEmpty);
    }

    [Fact]
    public async Task Submit_rogziti_a_jogi_bizonyitekot_es_a_hozzajarulasokat()
    {
        var (sheets, lookup, clock, kvl, db) = Build();
        using var _db = db;

        // Régi ügyfél előhívása és pulti adatlap létrehozása.
        var match = (await lookup.FindByDateOfBirthAsync(new DateOnly(1970, 11, 8))).Matches.Single();
        var sheetId = await sheets.CreateWalkInSheetAsync(match.PatientId);

        var input = new DataSheetInput
        {
            FullName = "Szabó Erzsébet",
            DateOfBirth = new DateOnly(1970, 11, 8),
            TajNumber = "987 654 321",
            MobilePhone = "+36209876543",
            Email = "szabo.e@example.hu",
            PostalCode = "6720",
            City = "Szeged",
            AddressLine = "Kárász utca 1.",
            Consents = new ConsentAnswers(
                KihordasiIdoTajekoztatas: true,
                Hirlevel: false,
                PostaiKuldemeny: true,
                EmailKuldemeny: true)
        };

        await sheets.SubmitAsync(sheetId, input, new SubmissionContext("1.2.3.4", "UnitTest"));

        // Jogi bizonyíték: időbélyeg + IP.
        var sheet = await db.Context.DataSheets.FirstAsync(s => s.Id == sheetId);
        Assert.Equal(DataSheetStatus.Vegleges, sheet.Status);
        Assert.Equal(clock.UtcNow, sheet.SubmittedAtUtc);
        Assert.Equal("1.2.3.4", sheet.SubmittedFromIp);

        // 4 hozzájárulás rögzítve, helyes értékekkel.
        var consents = await db.Context.Consents.Where(c => c.DataSheetId == sheetId).ToListAsync();
        Assert.Equal(4, consents.Count);
        Assert.True(consents.Single(c => c.Channel == ConsentChannel.KihordasiIdoTajekoztatas).Granted);
        Assert.False(consents.Single(c => c.Channel == ConsentChannel.Hirlevel).Granted);

        // Audit napló: a véglegesítés bejegyzése létrejött.
        Assert.Contains(await db.Context.AuditLog.ToListAsync(),
            a => a.Action == "AdatlapVeglegesitve" && a.IpAddress == "1.2.3.4");
    }

    [Fact]
    public async Task Submit_visszairja_a_frissitett_profilt_a_KVL_be()
    {
        var (sheets, lookup, _, kvl, db) = Build();
        using var _db = db;

        // Nagy Sándor: hiányzó mobil és TAJ (KVL-1002).
        var match = (await lookup.FindByDateOfBirthAsync(new DateOnly(1956, 3, 22)))
            .Matches.Single(m => m.HasMissingContact);
        var sheetId = await sheets.CreateWalkInSheetAsync(match.PatientId);

        var input = new DataSheetInput
        {
            FullName = "Nagy Sándor",
            DateOfBirth = new DateOnly(1956, 3, 22),
            TajNumber = "111 222 333",
            MobilePhone = "+36301112222",
            Email = "nagy.sandor@example.hu",
            PostalCode = "4025",
            City = "Debrecen",
            AddressLine = "Piac utca 5."
        };
        await sheets.SubmitAsync(sheetId, input, new SubmissionContext("portal", "UnitTest"));

        // A KVL mockban frissült a partner mobilszáma.
        var kvlPartner = await kvl.GetPartnerByCodeAsync("KVL-1002");
        Assert.NotNull(kvlPartner);
        Assert.Equal("+36301112222", kvlPartner!.MobilePhone);
        Assert.Equal("111 222 333", kvlPartner.TajNumber);
    }

    [Fact]
    public async Task Ketszeri_veglegesites_nem_engedett()
    {
        var (sheets, lookup, _, _, db) = Build();
        using var _db = db;

        var match = (await lookup.FindByDateOfBirthAsync(new DateOnly(1970, 11, 8))).Matches.Single();
        var sheetId = await sheets.CreateWalkInSheetAsync(match.PatientId);
        var input = new DataSheetInput { FullName = "Szabó Erzsébet", DateOfBirth = new DateOnly(1970, 11, 8) };

        await sheets.SubmitAsync(sheetId, input, new SubmissionContext(null, null));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sheets.SubmitAsync(sheetId, input, new SubmissionContext(null, null)));
    }

    [Fact]
    public async Task Lejart_token_nem_toltheto_be()
    {
        var (sheets, lookup, clock, _, db) = Build();
        using var _db = db;

        var match = (await lookup.FindByDateOfBirthAsync(new DateOnly(1970, 11, 8))).Matches.Single();
        var link = await sheets.CreateRemoteLinkAsync(match.PatientId);

        // Az idő túllépi a token lejáratát.
        clock.UtcNow = link.ExpiresAtUtc.AddDays(1);

        var loaded = await sheets.GetByTokenAsync(link.AccessToken);
        Assert.Null(loaded);
    }
}
