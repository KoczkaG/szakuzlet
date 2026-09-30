using Szakuzlet.Application.Billing;

namespace Szakuzlet.Web.Api;

/// <summary>Számlázási, EAN-pool és OEP-egyeztető REST végpontok (II. Modul).</summary>
public static class BillingApi
{
    public static void MapBillingApi(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/billing").WithTags("Billing");

        // EAN-tömb import (hatósági sorszámlista).
        g.MapPost("/ean/import", async (EanImportRequest req, EanPoolService svc, CancellationToken ct) =>
        {
            var added = await svc.ImportCodesAsync(req.Codes ?? Array.Empty<string>(), ct);
            return Results.Ok(new { added, available = await svc.AvailableCountAsync(ct) });
        });

        // Szabad EAN-kódok száma.
        g.MapGet("/ean/available", async (EanPoolService svc, CancellationToken ct) =>
            Results.Ok(new { available = await svc.AvailableCountAsync(ct) }));

        // Egészségpénztárak listája (legördülő).
        g.MapGet("/health-funds", async (BillingService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetHealthFundsAsync(ct)));

        // Napi OEP egyeztető.
        g.MapPost("/oep/reconcile", async (OepReconcileRequest req,
            SettlementService svc, CancellationToken ct) =>
        {
            var result = await svc.ReconcileDayAsync(req.Day, req.MankoCounts, ct);
            return Results.Ok(result);
        });
    }
}

public record EanImportRequest(string[]? Codes);
public record OepReconcileRequest(DateOnly Day, Dictionary<string, int>? MankoCounts);
