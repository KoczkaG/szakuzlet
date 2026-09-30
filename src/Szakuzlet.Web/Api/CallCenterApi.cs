using Szakuzlet.Application.CallCenter;
using Szakuzlet.Application.Calendar;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Web.Api;

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
    }
}

public record IncomingCallRequest(string PhoneNumber, string? IvrMenu);
public record EndCallRequest(bool Answered, string? RecordingReference);
public record RecordingRequest(string State, string? Actor, string? Reason);
public record CallbackRequestDto(string PhoneNumber);
