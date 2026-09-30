using Szakuzlet.Application.Assistant;
using Szakuzlet.Application.Contracts;
using Szakuzlet.Application.Express;
using Szakuzlet.Application.Postal;
using Szakuzlet.Application.Telemedicine;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Web.Api;

/// <summary>Szerződés, expressz, postai próba, telemedicina és asszisztens REST végpontok (III. Modul).</summary>
public static class ContractsApi
{
    public static void MapContractsApi(this IEndpointRouteBuilder app)
    {
        // --- Szerződés (III/A) ---
        var c = app.MapGroup("/api/contracts").WithTags("Contracts");

        c.MapPost("/prepare", async (PrepareContractRequest req, ContractService svc, CancellationToken ct) =>
        {
            var channel = Enum.Parse<ServiceChannel>(req.Channel, ignoreCase: true);
            var summary = await svc.PrepareAsync(req.PatientId, new ContractDraftInput(
                channel, req.DeviceModel, req.DeviceSerialNumber, req.MaskModel,
                req.PressureCmH2O, req.DepositAmount), ct);
            return Results.Ok(summary);
        });

        c.MapPost("/{contractId:guid}/sign/sms/start", async (Guid contractId, SmsStartRequest req,
            ContractService svc, CancellationToken ct) =>
        {
            var challenge = await svc.StartSmsSignatureAsync(contractId, req.PhoneNumber, ct);
            return Results.Ok(challenge);
        });

        c.MapPost("/{contractId:guid}/sign/sms/verify", async (Guid contractId, SmsVerifyRequest req,
            ContractService svc, CancellationToken ct) =>
        {
            var ok = await svc.CompleteSmsSignatureAsync(contractId, req.ChallengeId, req.Code, req.Ip, ct);
            return ok ? Results.NoContent() : Results.BadRequest(new { error = "Érvénytelen kód." });
        });

        // --- Digitális Asszisztens + riport (III/F) ---
        var a = app.MapGroup("/api/assistant").WithTags("Assistant");

        a.MapGet("/workflow", async (AssistantService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetWorkflowAsync(ct)));

        a.MapGet("/trial-report", async (DateTimeOffset? from, DateTimeOffset? to,
            AssistantService svc, CancellationToken ct) =>
        {
            var toUtc = to ?? DateTimeOffset.UtcNow;
            var fromUtc = from ?? toUtc.AddMonths(-1);
            return Results.Ok(await svc.GetTrialReportAsync(fromUtc, toUtc, ct));
        });

        // --- Telemedicina oktatóvideók (III/E) ---
        app.MapGet("/api/education/videos", async (string models, TelemedicineService svc, CancellationToken ct) =>
        {
            var list = (models ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return Results.Ok(await svc.GetVideosForProductsAsync(list, ct));
        }).WithTags("Education");
    }
}

public record PrepareContractRequest(Guid PatientId, string Channel, string DeviceModel,
    string DeviceSerialNumber, string? MaskModel, decimal? PressureCmH2O, decimal DepositAmount);
public record SmsStartRequest(string PhoneNumber);
public record SmsVerifyRequest(string ChallengeId, string Code, string? Ip);
