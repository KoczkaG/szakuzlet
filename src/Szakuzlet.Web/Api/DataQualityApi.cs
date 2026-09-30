using Szakuzlet.Application.DataQuality;

namespace Szakuzlet.Web.Api;

/// <summary>Az „ADATLAP HIÁNYOS” protokoll REST végpontjai (I. Modul F).</summary>
public static class DataQualityApi
{
    public static void MapDataQualityApi(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/dataquality").WithTags("DataQuality");

        // Feltételes riasztás: kötelező mezők ellenőrzése adatlap-megnyitáskor.
        g.MapGet("/patients/{patientId:guid}/check", async (Guid patientId,
            DataQualityService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.CheckAsync(patientId, ct)); }
            catch (InvalidOperationException ex) { return Results.NotFound(new { error = ex.Message }); }
        });

        // „A” opció: helyszíni frissítés.
        g.MapPost("/patients/{patientId:guid}/update", async (Guid patientId,
            ContactUpdateRequest req, DataQualityService svc, CancellationToken ct) =>
        {
            await svc.UpdateInPersonAsync(patientId,
                new ContactUpdate(req.Email, req.MobilePhone, req.TajNumber), ct);
            return Results.NoContent();
        });

        // „B” opció: önkiszolgáló link kiküldése.
        g.MapPost("/patients/{patientId:guid}/send-link", async (Guid patientId,
            DataQualityService svc, CancellationToken ct) =>
        {
            var req = await svc.SendSelfServiceLinkAsync(patientId, ct);
            return Results.Ok(new { req.Id, req.Token, req.ExpiresAtUtc });
        });

        // A beteg beküldi az adatokat a linken keresztül.
        g.MapPost("/complete/{token}", async (string token,
            ContactUpdateRequest req, DataQualityService svc, CancellationToken ct) =>
        {
            try
            {
                await svc.CompleteSelfServiceAsync(token,
                    new ContactUpdate(req.Email, req.MobilePhone, req.TajNumber), ct);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });
    }
}

public record ContactUpdateRequest(string? Email, string? MobilePhone, string? TajNumber);
