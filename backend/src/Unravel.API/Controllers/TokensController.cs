using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unravel.Application.Tokens.DTOs;
using Unravel.Application.Tokens.Ports;

namespace Unravel.API.Controllers;

/// <summary>
/// PR 52 — endpoints do saldo de "centímetros de lã" do moderador.
///
/// <para>Welcome bonus é garantido na primeira leitura do saldo
/// (idempotente). Histórico é somente-leitura — créditos/débitos
/// reais acontecem em response a eventos do sistema (gerar perguntas,
/// aluno completar ilha, etc) via service.</para>
/// </summary>
[ApiController]
[Route("api/tokens")]
[Authorize(Roles = "Moderator")]
public sealed class TokensController(IModeratorTokenService tokens) : ControllerBase
{
    private Guid UserId => Guid.Parse(
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("balance")]
    public async Task<IActionResult> Balance(CancellationToken ct)
    {
        // Garante bonus inicial na primeira visita do moderador.
        await tokens.EnsureWelcomeBonusAsync(UserId, ct);
        var cm = await tokens.GetBalanceAsync(UserId, ct);
        return Ok(new TokenBalanceResponse(
            BalanceCm:                  cm,
            Tier:                       ClassifyTier(cm),
            DisplayBalance:             FormatBalance(cm),
            EstimatedQuestionsRemaining: (int)Math.Round(cm * 0.69)));
    }

    [HttpGet("history")]
    public async Task<IActionResult> History(
        [FromQuery] int take = 30,
        [FromQuery] int skip = 0,
        CancellationToken ct = default)
    {
        if (take is < 1 or > 200) return BadRequest(new { message = "take deve estar em [1, 200]" });
        var items = await tokens.GetHistoryAsync(UserId, take, skip, ct);
        return Ok(items.Select(t => new TokenTransactionDto(
            t.Id, t.DeltaCm, t.Reason.ToString(), t.Metadata, t.CreatedAt.ToString("o"))));
    }

    /// <summary>
    /// Tier visual usado pelo &lt;YarnBall /&gt; — 5 buckets pra escolher
    /// SVG/cor. Limites alinhados com a tabela do PR 52.
    /// </summary>
    private static string ClassifyTier(int cm) => cm switch
    {
        0                          => "Empty",
        < 100                      => "Tiny",
        < 500                      => "Small",
        < 2000                     => "Medium",
        _                          => "Giant",
    };

    /// <summary>Formata pra exibição: &lt;100 cm = "X cm", ≥100 = "Xm Ycm".</summary>
    private static string FormatBalance(int cm)
    {
        if (cm < 100) return $"{cm} cm";
        var m = cm / 100;
        var c = cm % 100;
        return c == 0 ? $"{m} m" : $"{m}m {c}cm";
    }
}
