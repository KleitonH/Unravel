using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unravel.Application.Forge.Ports;
using Unravel.Application.Journey;
using Unravel.Application.Knowledge.Ports;
using Unravel.Domain.Forge;
using Unravel.Infrastructure.Knowledge;
using Unravel.Infrastructure.Persistence;

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

    /// <summary>
    /// PR 32 — enfileira jobs de geração LLM-grounded pra um Content.
    /// Extrai claims do conteúdo e cria 1 job por claim (até <c>max</c>).
    /// Worker BackgroundService processa em batch (1 por vez na GPU).
    /// </summary>
    [HttpPost("forge/{contentId:int}")]
    public async Task<IActionResult> EnqueueForge(
        int contentId,
        [FromQuery] int max = 20,
        [FromQuery] bool urgent = false,
        [FromServices] ApplicationDbContext db = null!,
        [FromServices] IClaimExtractor extractor = null!,
        [FromServices] IQuestionForgeQueue queue = null!,
        CancellationToken ct = default)
    {
        var content = await db.Content.Where(c => c.Id == contentId)
            .Select(c => new { c.Id, c.Title, c.Body })
            .FirstOrDefaultAsync(ct);
        if (content is null) return NotFound(new { message = $"Content {contentId} não existe." });

        var claims = extractor.Extract(content.Body)
            .OrderByDescending(c => c.Score)
            .Take(max)
            .ToList();

        if (claims.Count == 0)
            return Ok(new { contentId, contentTitle = content.Title, enqueued = 0, message = "Nenhuma claim extratível desse conteúdo." });

        var enqueued = await queue.EnqueueForContentAsync(
            contentId, claims,
            urgent ? ForgeJobPriority.Urgent : ForgeJobPriority.Normal,
            ct);

        return Ok(new { contentId, contentTitle = content.Title, claimsCandidates = claims.Count, enqueued });
    }

    /// <summary>
    /// PR 32 — snapshot da fila pra dashboard admin / monitoring.
    /// </summary>
    [HttpGet("forge/status")]
    public async Task<IActionResult> ForgeStatus(
        [FromServices] IQuestionForgeQueue queue,
        CancellationToken ct)
    {
        var status = await queue.GetStatusAsync(ct);
        return Ok(status);
    }
}
