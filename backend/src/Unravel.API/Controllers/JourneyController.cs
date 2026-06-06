using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unravel.Application.Forge.UseCases;
using Unravel.Application.Journey.Ports;
using Unravel.Application.Journey.UseCases;

namespace Unravel.API.Controllers;

/// <summary>
/// Endpoints do algoritmo de organização de jornadas (PR 3). A jornada é
/// recalculada a cada request — o <c>JourneyPlanner</c> é puro/in-memory
/// e o cache do KnowledgeGraph + masteries é barato. Não persistimos o
/// plano em si (snapshot diário é trabalho do PR 7, cron).
/// </summary>
[ApiController]
[Route("api/journey")]
[Authorize]
public sealed class JourneyController : ControllerBase
{
    private readonly GetDailyJourneyUseCase            _getDaily;
    private readonly BuildReinforcementQuizUseCase     _reinforcement;

    private Guid UserId => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                                      ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private readonly ITrailProgressService _progress;

    public JourneyController(
        GetDailyJourneyUseCase        getDaily,
        BuildReinforcementQuizUseCase reinforcement,
        ITrailProgressService         progress)
    {
        _getDaily      = getDaily;
        _reinforcement = reinforcement;
        _progress      = progress;
    }

    /// <summary>Plano do dia para o usuário autenticado numa trilha. Calcula
    /// no momento da chamada usando o instante atual como <c>asOf</c>.
    /// Retorna 404 se a trilha não existe / está inativa, ou se o usuário
    /// não existe.</summary>
    [HttpGet("today")]
    public async Task<IActionResult> Today([FromQuery] int trailId, CancellationToken ct)
    {
        if (trailId <= 0) return BadRequest(new { message = "trailId é obrigatório." });

        var plan = await _getDaily.ExecuteAsync(UserId, trailId, DateTime.UtcNow, ct);
        return plan is null ? NotFound() : Ok(plan);
    }

    /// <summary>Força recálculo da jornada. Hoje é idêntico a
    /// <see cref="Today"/> (o planner sempre roda na chamada); o endpoint
    /// existe como contrato pro frontend quando o cron diário (PR 7) e o
    /// snapshot persistido entrarem em cena.</summary>
    [HttpPost("replan")]
    public async Task<IActionResult> Replan([FromQuery] int trailId, CancellationToken ct)
    {
        if (trailId <= 0) return BadRequest(new { message = "trailId é obrigatório." });

        var plan = await _getDaily.ExecuteAsync(UserId, trailId, DateTime.UtcNow, ct);
        return plan is null ? NotFound() : Ok(plan);
    }

    /// <summary>
    /// PR 37 — "Treinar fraquezas". Retorna até <paramref name="count"/>
    /// perguntas focadas nos tópicos com mastery efetiva &lt; 0.6, excluindo
    /// perguntas que o aluno já respondeu. Se algum tópico fraco tem pool
    /// fresco insuficiente, dispara replenishment urgent no forge.
    ///
    /// <para>Response inclui <c>weakTopics</c> (lista das fraquezas com seus
    /// scores), <c>moreComing</c> (true se jobs foram enfileirados) e
    /// <c>reason</c> populado quando <c>challenges</c> volta vazio
    /// (<c>no_weaknesses</c> | <c>pool_exhausted</c> | <c>no_content_for_weakness</c>).</para>
    /// </summary>
    [HttpPost("reinforce/{trailId:int}")]
    public async Task<IActionResult> Reinforce(
        int trailId,
        [FromQuery] int count = 5,
        CancellationToken ct = default)
    {
        if (trailId <= 0) return BadRequest(new { message = "trailId é obrigatório." });
        if (count is < 1 or > 20)
            return BadRequest(new { message = "count deve estar entre 1 e 20." });

        var result = await _reinforcement.ExecuteAsync(UserId, trailId, count, ct);
        return Ok(result);
    }

    /// <summary>
    /// PR 40 — mapa de progressão da trilha pro aluno autenticado.
    /// Retorna lista ordenada de Contents (ilhas) com Status calculado
    /// (Locked/Available/InProgress/Completed) + progresso de desafios.
    ///
    /// <para>Bootstrap automático: se o aluno ainda não tem nenhum
    /// UserContent na trilha (caso típico após enroll antigo, antes do
    /// PR 40), cria UserContent pra 1ª ilha como Available e retorna.</para>
    /// </summary>
    [HttpGet("trails/{trailId:int}/map")]
    public async Task<IActionResult> GetMap(int trailId, CancellationToken ct)
    {
        if (trailId <= 0) return BadRequest(new { message = "trailId é obrigatório." });

        await _progress.BootstrapAccessAsync(UserId, trailId, ct);
        var map = await _progress.GetTrailMapAsync(UserId, trailId, ct);
        return map is null ? NotFound() : Ok(map);
    }

    /// <summary>
    /// PR 41 — radar de fraquezas: lista de tópicos da trilha com mastery
    /// efetiva (com decay), confidence, SRS state e severity. Ordenado
    /// por severidade (Weak primeiro) pra UI rankear sem reprocessar.
    /// </summary>
    [HttpGet("trails/{trailId:int}/mastery")]
    public async Task<IActionResult> GetMastery(int trailId, CancellationToken ct)
    {
        if (trailId <= 0) return BadRequest(new { message = "trailId é obrigatório." });

        var report = await _progress.GetTrailMasteryAsync(UserId, trailId, ct);
        return report is null ? NotFound() : Ok(report);
    }
}
