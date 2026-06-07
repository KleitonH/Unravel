namespace Unravel.Domain.Tokens;

/// <summary>
/// PR 52 — saldo de "centímetros de lã" do moderador. Singleton por
/// <c>UserId</c>. Unidade temática alinhada com o NAVI cat (gato → novelo).
///
/// <para><b>Conversão</b>: 1 centímetro = 1 job de geração LLM no forge.
/// Saldo típico de moderador ativo: 1000-2000 cm (10-20 metros). Pra
/// exibição, &lt;100 cm mostra em "cm" e ≥100 mostra em "m" (com decimais).</para>
///
/// <para><b>5 tiers visuais</b> usados pelo &lt;YarnBall /&gt; no frontend:</para>
/// <list type="bullet">
///   <item>Empty: 0 cm — novelo desfiado</item>
///   <item>Tiny: 1-99 cm — bege claro, pequeno</item>
///   <item>Small: 100-499 cm — rosa-poeira</item>
///   <item>Medium: 500-1999 cm — rosa-coral cheio</item>
///   <item>Giant: 2000+ cm — dourado, gigante</item>
/// </list>
/// </summary>
public sealed class ModeratorTokenBalance
{
    public Guid     UserId    { get; set; }
    public int      BalanceCm { get; set; }
    public DateTime UpdatedAt { get; set; }
}
