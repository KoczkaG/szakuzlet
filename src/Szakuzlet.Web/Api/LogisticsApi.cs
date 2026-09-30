using Szakuzlet.Application.Logistics;
using Szakuzlet.Application.Philips;
using Szakuzlet.Application.Timeline;
using Szakuzlet.Domain.Enums;
using Szakuzlet.Infrastructure.Philips;

namespace Szakuzlet.Web.Api;

/// <summary>
/// Logisztikai (csomag/futár), Timeline és Philips-import végpontok.
/// </summary>
public static class LogisticsApi
{
    public static void MapLogisticsApi(this IEndpointRouteBuilder app)
    {
        var s = app.MapGroup("/api/shipments").WithTags("Shipments");

        // Csomag feladása.
        s.MapPost("/", async (CreateShipmentRequest req, ShipmentService svc, CancellationToken ct) =>
        {
            var courier = Enum.Parse<Courier>(req.Courier, ignoreCase: true);
            var shipment = await svc.CreateAsync(req.PatientId, courier, req.TrackingNumber, ct);
            return Results.Ok(new { shipment.Id, shipment.TrackingNumber });
        });

        // Nyitott csomagok státuszának szinkronja a futár-API-ból.
        s.MapPost("/sync", async (ShipmentService svc, CancellationToken ct) =>
        {
            var changed = await svc.SyncOpenShipmentsAsync(ct);
            return Results.Ok(new { changed });
        });

        // A beteg teljes idővonala.
        app.MapGet("/api/patients/{patientId:guid}/timeline",
            async (Guid patientId, TimelineService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.GetAsync(patientId, ct)); }
            catch (InvalidOperationException ex) { return Results.NotFound(new { error = ex.Message }); }
        }).WithTags("Timeline");

        // Philips-csereprojekt CSV import (admin, egyszeri).
        app.MapPost("/api/philips/import", async (HttpRequest request,
            PhilipsImportService svc, CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body);
            var csv = await reader.ReadToEndAsync(ct);
            var rows = PhilipsCsvParser.Parse(csv);
            var result = await svc.ImportAsync(rows, ct);
            return Results.Ok(result);
        }).WithTags("Philips").Accepts<string>("text/csv");
    }
}

public record CreateShipmentRequest(Guid PatientId, string Courier, string TrackingNumber);
