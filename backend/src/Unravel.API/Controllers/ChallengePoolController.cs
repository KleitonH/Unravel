using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unravel.Application.Forge.DTOs;
using Unravel.Application.Forge.Ports;
using Unravel.Application.Forge.UseCases;

namespace Unravel.API.Controllers;

/// <summary>
/// Pool de perguntas geradas pelo Forge para um Content específico,
/// calibrado pelo nível de domínio do usuário. Separado do
/// <see cref="ChallengesController"/> (que serve as perguntas curadas
/// por moderadores, ligadas a Trail) — quando Challenge ganhar
/// ContentId, os dois pools podem mesclar no use case.
/// </summary>
[ApiController]
[Route("api/contents/{contentId:int}/challenge-pool")]
[Authorize]
public sealed class ChallengePoolController : ControllerBase
{
    private readonly GetChallengePoolUseCase             _pool;
    private readonly SubmitPoolChallengeUseCase          _submit;
    private readonly SelectNextAdaptiveChallengeUseCase  _adaptive;

    private Guid UserId => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                                      ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public ChallengePoolController(
        GetChallengePoolUseCase            pool,
        SubmitPoolChallengeUseCase         submit,
        SelectNextAdaptiveChallengeUseCase adaptive)
    {
        _pool     = pool;
        _submit   = submit;
        _adaptive = adaptive;
    }

    /// <summary>
    /// PR 60-a — Conteúdo fatiado em capítulos H2 com perguntas alocadas
    /// adaptativamente (4-7 por capítulo conforme difficulty média). Usado
    /// pelo novo fluxo "Estudo guiado" (modelo Duolingo): aluno lê chunk,
    /// pratica perguntas daquele chunk, segue pro próximo.
    /// 404 se Content não existe.
    /// </summary>
    [HttpGet("/api/contents/{contentId:int}/chapters")]
    public async Task<IActionResult> GetChapters(
        int contentId,
        [FromQuery] int minPerChapter = 4,
        [FromQuery] int maxPerChapter = 7,
        [FromServices] IContentChaptersService? chapters = null,
        CancellationToken ct = default)
    {
        if (chapters is null) return Problem("Serviço de capítulos indisponível.");
        if (minPerChapter < 1 || maxPerChapter < minPerChapter || maxPerChapter > 20)
            return BadRequest(new { message = "min/maxPerChapter inválidos (1 ≤ min ≤ max ≤ 20)." });

        var result = await chapters.GetChaptersAsync(contentId, minPerChapter, maxPerChapter, ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Retorna até <c>targetCount</c> perguntas (default 5) para
    /// o Content. Gera novas se o pool persistido estiver curto. 404 se
    /// Content não existe ou está inativo.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        int contentId,
        [FromQuery] int targetCount = 5,
        CancellationToken ct = default)
    {
        if (targetCount is < 1 or > 20)
            return BadRequest(new { message = "targetCount deve estar entre 1 e 20." });

        var pool = await _pool.ExecuteAsync(UserId, contentId, targetCount, ct);
        return pool is null ? NotFound() : Ok(pool);
    }

    /// <summary>Submete a resposta de uma pergunta do pool: servidor valida
    /// contra o gabarito persistido (cliente nunca decide o acerto),
    /// atualiza a Mastery do tópico e devolve o gabarito + nova mastery.
    /// 404 se o GeneratedChallenge não existe ou não pertence ao Content.</summary>
    [HttpPost("submit")]
    public async Task<IActionResult> Submit(
        int contentId,
        [FromBody] SubmitPoolChallengeRequest request,
        CancellationToken ct = default)
    {
        if (request is null) return BadRequest(new { message = "Body obrigatório." });
        if (request.GeneratedChallengeId <= 0)
            return BadRequest(new { message = "generatedChallengeId é obrigatório." });

        var result = await _submit.ExecuteAsync(UserId, contentId, request, ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// PR 42 — CAT-lite: dado o histórico curto da sessão, retorna a
    /// próxima pergunta calibrada por ability estimate online. Quando
    /// a sessão deve encerrar (cap atingido, convergiu ou pool esgotado),
    /// retorna <c>done=true</c> com <c>stopReason</c> populado.
    /// </summary>
    [HttpPost("adaptive/next")]
    public async Task<IActionResult> AdaptiveNext(
        int contentId,
        [FromBody] AdaptiveNextRequest request,
        CancellationToken ct = default)
    {
        if (request is null) return BadRequest(new { message = "Body obrigatório." });

        var historyDomain = (request.History ?? Array.Empty<AdaptiveHistoryItem>())
            .Select(h => new Unravel.Application.Forge.Adaptive.AdaptiveOutcome(h.ChallengeId, h.WasCorrect))
            .ToList();

        var result = await _adaptive.ExecuteAsync(UserId, contentId, historyDomain, ct);
        return result is null ? NotFound() : Ok(result);
    }
}
