using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unravel.Application.Journey.Onboarding;

namespace Unravel.API.Controllers;

/// <summary>
/// Onboarding (PR 6): fluxo de duas etapas para inicializar o perfil de
/// domínio de um usuário novo. Etapa 1 devolve o teste de nivelamento;
/// etapa 2 recebe respostas, inicializa Mastery e inscreve nas trilhas.
/// </summary>
[ApiController]
[Route("api/journey/onboarding")]
[Authorize]
public sealed class OnboardingController : ControllerBase
{
    private readonly StartOnboardingUseCase  _start;
    private readonly SubmitOnboardingUseCase _submit;

    private Guid UserId => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                                      ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public OnboardingController(StartOnboardingUseCase start, SubmitOnboardingUseCase submit)
    {
        _start  = start;
        _submit = submit;
    }

    /// <summary>Etapa 1: usuário escolhe trilhas, recebe teste de
    /// nivelamento. 400 se lista vazia; 404 se nenhuma trilha válida;
    /// 409 se já fez onboarding em alguma delas.</summary>
    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] OnboardingStartRequest request, CancellationToken ct)
    {
        if (request.TrailIds is null || request.TrailIds.Count == 0)
            return BadRequest(new { message = "Pelo menos uma trilha é obrigatória." });

        try
        {
            var test = await _start.ExecuteAsync(UserId, request, ct);
            return test is null || test.Trails.Count == 0 ? NotFound() : Ok(test);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>Etapa 2: envia respostas. Retorna estimativa por trilha
    /// + IDs inscritos. O frontend pode chamar
    /// <see cref="JourneyController.Today"/> em seguida para a primeira
    /// jornada já personalizada.</summary>
    [HttpPost("submit")]
    public async Task<IActionResult> Submit(
        [FromQuery] string trailIds,
        [FromBody] OnboardingSubmitRequest request,
        CancellationToken ct)
    {
        var parsed = ParseIds(trailIds);
        if (parsed.Count == 0)
            return BadRequest(new { message = "Query trailIds=1,2,... é obrigatória." });
        if (request.Answers is null || request.Answers.Count == 0)
            return BadRequest(new { message = "Respostas vazias." });

        var result = await _submit.ExecuteAsync(UserId, parsed, request, ct);
        return Ok(result);
    }

    private static List<int> ParseIds(string raw) =>
        (raw ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .Where(s => int.TryParse(s, out _))
                   .Select(int.Parse)
                   .Distinct()
                   .ToList();
}
