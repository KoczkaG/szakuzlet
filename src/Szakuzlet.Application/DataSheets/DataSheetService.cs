using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Application.Common;
using Szakuzlet.Application.Kvl;
using Szakuzlet.Domain.Entities;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Application.DataSheets;

/// <summary>
/// Az online (páciens-portál) és helyszíni (tabletes) digitális adatlap-kitöltés magja.
/// Kezeli a link kiküldését, a kitöltés véglegesítését jogi bizonyítékkal (időbélyeg + IP),
/// és a KVL felé történő profil-visszaírást.
/// </summary>
public sealed class DataSheetService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly ITokenGenerator _tokens;
    private readonly IKvlClient _kvl;
    private readonly EventRecorder _events;

    // A kiküldött link alapértelmezett élettartama.
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(14);

    public DataSheetService(IAppDbContext db, IClock clock, ITokenGenerator tokens,
        IKvlClient kvl, EventRecorder events)
    {
        _db = db;
        _clock = clock;
        _tokens = tokens;
        _kvl = kvl;
        _events = events;
    }

    /// <summary>
    /// Távoli (online) adatlap-link létrehozása egy páciens számára. A visszaadott token
    /// az SMS-ben/e-mailben kiküldendő linkbe kerül (I. Modul A, feladatlista 1. pont).
    /// </summary>
    public async Task<DataSheetLink> CreateRemoteLinkAsync(Guid patientId, CancellationToken ct = default)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen páciens: {patientId}");

        var now = _clock.UtcNow;
        var sheet = new DataSheet
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Status = DataSheetStatus.Kikuldve,
            Channel = DataSheetChannel.TavoliOnline,
            AccessToken = _tokens.CreateToken(),
            TokenExpiresAtUtc = now.Add(TokenLifetime),
            CreatedAtUtc = now
        };
        _db.DataSheets.Add(sheet);
        AddAudit("DataSheet", sheet.Id.ToString(), "TavoliLinkLetrehozva",
            $"Páciens: {patient.Id}", "RENDSZER", null, now);

        await _db.SaveChangesAsync(ct);
        return new DataSheetLink(sheet.Id, sheet.AccessToken!, sheet.TokenExpiresAtUtc!.Value);
    }

    /// <summary>
    /// Helyszíni (tabletes) adatlap létrehozása azonnali kitöltésre. Token nélkül,
    /// mert a pulti tableten közvetlenül nyílik meg (I. Modul A, feladatlista 2. pont).
    /// </summary>
    public async Task<Guid> CreateWalkInSheetAsync(Guid patientId, CancellationToken ct = default)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen páciens: {patientId}");

        var now = _clock.UtcNow;
        var sheet = new DataSheet
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Status = DataSheetStatus.Folyamatban,
            Channel = DataSheetChannel.HelysziniTablet,
            CreatedAtUtc = now
        };
        _db.DataSheets.Add(sheet);
        await _db.SaveChangesAsync(ct);
        return sheet.Id;
    }

    /// <summary>Adatlap betöltése azonosító alapján, a pácienssel együtt (pulti kitöltéshez).</summary>
    public Task<DataSheet?> GetByIdWithPatientAsync(Guid dataSheetId, CancellationToken ct = default)
        => _db.DataSheets.Include(s => s.Patient).FirstOrDefaultAsync(s => s.Id == dataSheetId, ct);

    /// <summary>
    /// Token alapú betöltés az online kitöltéshez. Null, ha a token érvénytelen vagy lejárt.
    /// </summary>
    public async Task<DataSheet?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        var sheet = await _db.DataSheets
            .Include(s => s.Patient)
            .FirstOrDefaultAsync(s => s.AccessToken == token, ct);

        if (sheet is null) return null;
        if (sheet.TokenExpiresAtUtc is { } exp && exp < _clock.UtcNow)
            return null;

        return sheet;
    }

    /// <summary>
    /// Az adatlap véglegesítése: a törzsadatok és a 4 hozzájárulás rögzítése,
    /// jogi bizonyítékkal (időbélyeg + IP), majd visszaírás a KVL-be.
    /// </summary>
    public async Task SubmitAsync(
        Guid dataSheetId, DataSheetInput input, SubmissionContext context, CancellationToken ct = default)
    {
        var sheet = await _db.DataSheets
            .Include(s => s.Patient)
            .FirstOrDefaultAsync(s => s.Id == dataSheetId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen adatlap: {dataSheetId}");

        if (sheet.Status == DataSheetStatus.Vegleges)
            throw new InvalidOperationException("Ez az adatlap már véglegesítve lett.");

        var now = _clock.UtcNow;
        var patient = sheet.Patient;

        // Törzsadatok frissítése.
        patient.FullName = input.FullName;
        patient.DateOfBirth = input.DateOfBirth;
        patient.TajNumber = Normalize(input.TajNumber);
        patient.MobilePhone = Normalize(input.MobilePhone);
        patient.Email = Normalize(input.Email);
        patient.PostalCode = Normalize(input.PostalCode);
        patient.City = Normalize(input.City);
        patient.AddressLine = Normalize(input.AddressLine);
        patient.UpdatedAtUtc = now;

        // Hozzájárulások rögzítése (a korábbiakat lecseréljük erre a kitöltésre).
        AddConsent(sheet, patient, ConsentChannel.KihordasiIdoTajekoztatas, input.Consents.KihordasiIdoTajekoztatas, now);
        AddConsent(sheet, patient, ConsentChannel.Hirlevel, input.Consents.Hirlevel, now);
        AddConsent(sheet, patient, ConsentChannel.PostaiKuldemeny, input.Consents.PostaiKuldemeny, now);
        AddConsent(sheet, patient, ConsentChannel.EmailKuldemeny, input.Consents.EmailKuldemeny, now);

        // Jogi bizonyíték.
        sheet.Status = DataSheetStatus.Vegleges;
        sheet.SubmittedAtUtc = now;
        sheet.SubmittedFromIp = context.IpAddress;
        sheet.SubmittedUserAgent = context.UserAgent;

        AddAudit("DataSheet", sheet.Id.ToString(), "AdatlapVeglegesitve",
            "Aktív jelölőnégyzet-kiválasztás; jogilag egyenértékű a kézi aláírással.",
            "PACIENS", context.IpAddress, now);
        _events.Timeline(patient.Id, "AdatlapVeglegesitve",
            $"Digitális adatlap véglegesítve ({sheet.Channel}).");

        // Visszaírás a KVL-be (mock most, valós API később).
        var kvlDto = new KvlPartnerDto
        {
            PartnerCode = patient.KvlPartnerCode,
            FullName = patient.FullName,
            DateOfBirth = patient.DateOfBirth,
            TajNumber = patient.TajNumber,
            MobilePhone = patient.MobilePhone,
            Email = patient.Email,
            PostalCode = patient.PostalCode,
            City = patient.City,
            AddressLine = patient.AddressLine
        };
        var code = await _kvl.UpsertPartnerAsync(kvlDto, ct);
        if (patient.KvlPartnerCode != code)
        {
            patient.KvlPartnerCode = code;
            AddAudit("Patient", patient.Id.ToString(), "KvlSzinkron",
                $"KVL partnerkód: {code}", "RENDSZER", null, now);
        }

        await _db.SaveChangesAsync(ct);
    }

    private void AddConsent(DataSheet sheet, Patient patient, ConsentChannel channel, bool granted, DateTimeOffset now)
    {
        _db.Consents.Add(new ConsentRecord
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            DataSheetId = sheet.Id,
            Channel = channel,
            Granted = granted,
            RecordedAtUtc = now
        });
    }

    private void AddAudit(string entityType, string entityId, string action,
        string? details, string actor, string? ip, DateTimeOffset now)
    {
        _db.AuditLog.Add(new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Details = details,
            Actor = actor,
            IpAddress = ip,
            TimestampUtc = now
        });
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
