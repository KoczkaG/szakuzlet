using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Application.Common;
using Szakuzlet.Domain.Entities;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Application.CallCenter;

/// <summary>
/// A kényszerített hívásvégi jegyzet feldolgozása (I. Modul E). A jegyzet mentésekor:
/// beíródik a beteg Idővonalába, és ha visszahívást/teendőt igényel, automatikusan
/// nyitott feladatot generál a belső Feladatkezelőben.
/// </summary>
public sealed class CallNoteService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly EventRecorder _events;

    public CallNoteService(IAppDbContext db, IClock clock, EventRecorder events)
    {
        _db = db;
        _clock = clock;
        _events = events;
    }

    /// <summary>A hívásvégi jegyzet mentése egy adott híváshoz.</summary>
    public async Task<CallNote> SaveAsync(Guid callId, CallNoteInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Summary))
            throw new InvalidOperationException("A jegyzet szöveges összefoglalása kötelező.");

        var call = await _db.Calls.FirstOrDefaultAsync(c => c.Id == callId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen hívás: {callId}");

        // Egy híváshoz egy jegyzet (a kényszerített ablak egyszer töltődik ki).
        if (await _db.CallNotes.AnyAsync(n => n.CallId == callId, ct))
            throw new InvalidOperationException("Ehhez a híváshoz már készült jegyzet.");

        var now = _clock.UtcNow;
        var note = new CallNote
        {
            Id = Guid.NewGuid(),
            CallId = callId,
            PatientId = call.PatientId,
            Topics = input.Topics,
            ReferralSourceId = input.ReferralSourceId,
            Summary = input.Summary.Trim(),
            FollowUpRequired = input.FollowUpRequired,
            CreatedAtUtc = now
        };
        _db.CallNotes.Add(note);

        // A jegyzet lényege a beteg Idővonalára.
        if (call.PatientId is { } pid)
            _events.Timeline(pid, "HivasvegiJegyzet",
                $"Hívásvégi jegyzet: {note.Summary}");

        // Automata feladatgenerálás visszahívási igény esetén (csak azonosított beteghez).
        if (input.FollowUpRequired && call.PatientId is { } taskPatientId)
        {
            _db.Tasks.Add(new PatientTask
            {
                Id = Guid.NewGuid(),
                PatientId = taskPatientId,
                Type = PatientTaskType.HivasvegiVisszahivas,
                Status = PatientTaskStatus.Nyitott,
                Title = "Visszahívás / teendő hívás után",
                Details = note.Summary,
                CreatedAtUtc = now
            });
        }

        await _db.SaveChangesAsync(ct);
        return note;
    }

    /// <summary>Az aktív küldő intézmények / alváslaborok listája a legördülő menühöz.</summary>
    public Task<List<ReferralSource>> GetReferralSourcesAsync(CancellationToken ct = default)
        => _db.ReferralSources.Where(r => r.IsActive).OrderBy(r => r.Name).ToListAsync(ct);
}
