using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unravel.Application.Journey;
using Unravel.Infrastructure.Knowledge;

namespace Unravel.API.Controllers;

/// <summary>
/// Operações administrativas — protegidas por role Moderator.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Moderator")]
public sealed class AdminController(
    DailyReplanService replan,
    KnowledgeImporter  knowledgeImporter,
    IConfiguration     configuration,
    IWebHostEnvironment env) : ControllerBase
{
    /// <summary>Roda o lote de replanejamento <i>agora</i>. Idempotente:
    /// se já rodou hoje, faz upsert dos snapshots (não duplica).
    /// Resposta inclui o relatório do lote.</summary>
    [HttpPost("replan-now")]
    public async Task<IActionResult> ReplanNow(CancellationToken ct)
    {
        var report = await replan.RunAsync(DateTime.UtcNow, ct);
        return Ok(report);
    }

    /// <summary>
    /// PR 28 — re-importa todas as trilhas em <c>backend/knowledge/</c>.
    /// Idempotente (upsert por slug). Útil quando adicionar/editar MDs
    /// sem precisar reiniciar a API.
    /// </summary>
    [HttpPost("knowledge/import")]
    public async Task<IActionResult> ImportKnowledge(CancellationToken ct)
    {
        var configured = configuration["Knowledge:Path"];
        var rootPath   = !string.IsNullOrWhiteSpace(configured) && Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(env.ContentRootPath, configured ?? "../../knowledge"));

        var summary = await knowledgeImporter.ImportAllAsync(rootPath, ct);
        return Ok(new
        {
            rootPath,
            summary.TrailsCreated,
            summary.TrailsUpdated,
            summary.ContentsCreated,
            summary.ContentsUpdated,
        });
    }
}
