using Szakuzlet.Application.CallCenter;
using Szakuzlet.Application.Calendar;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Web.Api;
// (record definíciók a fájl alján)

/// <summary>
/// REST végpontok a külső telefonközpont (VoIP) számára. A Call Center ezeket hívja
/// a hívás életciklusának eseményeinél; a belső rendszer a döntési logikát és az adatokat adja.
/// </summary>
public static class CallCenterApi
{
    public static void MapCallCenterApi(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/callcenter").WithTags("CallCenter");

        // Nyitvatartási zsilip: nyitva van-e most? (IVR időzítő + webshop + Google)
        g.MapGet("/opening-status", async (OpeningHoursService hours, CancellationToken ct) =>
        {
            var s = await hours.GetStatusNowAsync(ct);
            return Results.Ok(new
            {
                isOpen = s.IsOpen,
                overrideLabel = s.OverrideLabel,
                nextOpenAtLocal = s.NextOpenAtLocal
            });
        });

        // Bejövő hívás beérkezése – CRM-találat + menüjelzés + adatlap-megnyitás.
        g.MapPost("/incoming", async (IncomingCallRequest req, CallCenterService svc, CancellationToken ct) =>
        {
            var result = await svc.HandleIncomingAsync(req.PhoneNumber, req.IvrMenu, ct);
            return Results.Ok(result);
        });

        // Hívás fogadva (a pultos felvette) – önüríti a nyitott visszahívást.
        g.MapPost("/{callId:guid}/answered", async (Guid callId, CallCenterService svc, CancellationToken ct) =>
        {
            await svc.MarkAnsweredAsync(callId, ct);
            return Results.NoContent();
        });

        // Hívás vége – ha nem fogadták, önürítő visszahívási listára kerül.
        g.MapPost("/{callId:guid}/end", async (Guid callId, EndCallRequest req,
            CallCenterService svc, CancellationToken ct) =>
        {
            await svc.EndCallAsync(callId, req.Answered, req.RecordingReference, ct);
            return Results.NoContent();
        });

        // Hangrögzítés döntés (9-es gomb / „Hangrögzítés leállítása”) + jogi Audit Trail.
        g.MapPost("/{callId:guid}/recording", async (Guid callId, RecordingRequest req,
            CallCenterService svc, CancellationToken ct) =>
        {
            var state = Enum.Parse<RecordingState>(req.State, ignoreCase: true);
            await svc.ApplyRecordingDecisionAsync(callId,
                new RecordingDecision(state, req.Actor ?? "KEZELO", req.Reason ?? ""), ct);
            return Results.NoContent();
        });

        // Munkaidőn kívüli visszahívás igénylése (5-ös gomb).
        g.MapPost("/callback/after-hours", async (CallbackRequestDto req,
            CallCenterService svc, CancellationToken ct) =>
        {
            var cb = await svc.RequestAfterHoursCallbackAsync(req.PhoneNumber, ct);
            return Results.Ok(new { callbackId = cb.Id, dueAtUtc = cb.DueAtUtc });
        });

        // Foglalt pult miatti azonnali visszahívás igénylése.
        g.MapPost("/callback/busy-desk", async (CallbackRequestDto req,
            CallCenterService svc, CancellationToken ct) =>
        {
            var cb = await svc.RequestBusyDeskCallbackAsync(req.PhoneNumber, ct);
            return Results.Ok(new { callbackId = cb.Id });
        });

        // --- Kimenő hívások (Click-to-Call) ---
        var o = app.MapGroup("/api/outbound").WithTags("Outbound");

        // A beteg összes hívható száma (Click-to-Call ikon mellette).
        o.MapGet("/patients/{patientId:guid}/numbers", async (Guid patientId,
            OutboundCallService svc, CancellationToken ct) =>
        {
            var numbers = await svc.GetDialableNumbersAsync(patientId, ct);
            return Results.Ok(numbers);
        });

        // Kimenő hívás indítása – visszaadja a felugró adatlap-kontextust + GDPR sablont.
        o.MapPost("/start", async (StartOutboundRequest req, OutboundCallService svc, CancellationToken ct) =>
        {
            var kind = Enum.Parse<PhoneKind>(req.Kind, ignoreCase: true);
            try
            {
                var result = await svc.StartCallAsync(req.PatientId, req.Number, kind, req.ServiceWorksheetId, ct);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        // Hangrögzítés leállítása (a hívott fél tiltotta) – Audit Trail.
        o.MapPost("/{callId:guid}/stop-recording", async (Guid callId, StopRecordingRequest req,
            OutboundCallService svc, CancellationToken ct) =>
        {
            await svc.StopRecordingAsync(callId, req.Actor ?? "KEZELO", ct);
            return Results.NoContent();
        });

        // Kimenő hívás vége.
        o.MapPost("/{callId:guid}/end", async (Guid callId, EndCallRequest req,
            OutboundCallService svc, CancellationToken ct) =>
        {
            await svc.EndCallAsync(callId, req.Answered, req.RecordingReference, ct);
            return Results.NoContent();
        });

        // --- Hívásvégi jegyzet és statisztika ---
        var n = app.MapGroup("/api/callnotes").WithTags("CallNotes");

        // Küldő intézmények / alváslaborok a legördülő menühöz.
        n.MapGet("/referral-sources", async (CallNoteService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetReferralSourcesAsync(ct)));

        // Hívásvégi jegyzet mentése.
        n.MapPost("/{callId:guid}", async (Guid callId, SaveNoteRequest req,
            CallNoteService svc, CancellationToken ct) =>
        {
            var topics = (CallTopic)req.Topics;
            try
            {
                var note = await svc.SaveAsync(callId,
                    new CallNoteInput(topics, req.ReferralSourceId, req.Summary, req.FollowUpRequired), ct);
                return Results.Ok(new { note.Id });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // Vezetői hívásstatisztika (időszakra).
        app.MapGet("/api/callstats", async (DateTimeOffset? from, DateTimeOffset? to,
            CallStatisticsService svc, CancellationToken ct) =>
        {
            var toUtc = to ?? DateTimeOffset.UtcNow;
            var fromUtc = from ?? toUtc.AddMonths(-1);
            return Results.Ok(await svc.GetAsync(fromUtc, toUtc, ct));
        }).WithTags("CallStats");
    }
}

public record IncomingCallRequest(string PhoneNumber, string? IvrMenu);
public record EndCallRequest(bool Answered, string? RecordingReference);
public record RecordingRequest(string State, string? Actor, string? Reason);
public record CallbackRequestDto(string PhoneNumber);
public record StartOutboundRequest(Guid PatientId, string Number, string Kind, Guid? ServiceWorksheetId);
public record StopRecordingRequest(string? Actor);
public record SaveNoteRequest(int Topics, Guid? ReferralSourceId, string Summary, bool FollowUpRequired);
