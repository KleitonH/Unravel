namespace Unravel.Domain.Forge;

/// <summary>
/// Item de gold set curado por moderador (PR 33d). Complementa o
/// gold YAML (estático, dev-curated) com gold dinâmico vindo de quem
/// modera a plataforma.
///
/// <para><b>Duas origens possíveis</b>:</para>
/// <list type="bullet">
///   <item><b>Marcar pergunta gerada</b>: o moderador olha o pool de
///   <c>GeneratedChallenge</c> de um Content e marca uma como "ouro"
///   — vira <see cref="ModeratorGoldItem"/> linkado via
///   <see cref="SourceGeneratedChallengeId"/>.</item>
///   <item><b>Item manual</b>: moderador escreve do zero (com seu próprio
///   prompt/distratores). <see cref="SourceGeneratedChallengeId"/>
///   fica null.</item>
/// </list>
///
/// <para>O <c>ForgeEvaluator</c> (PR 33) é estendido pra concatenar
/// items de YAML + items deste DB pro Trail/Content avaliado.</para>
///
/// <para><b>Por que tabela própria e não flag em <c>GeneratedChallenge</c></b>:</para>
/// <list type="bullet">
///   <item>Gold tem ciclo de vida diferente: pode ser editado por
///   moderador sem afetar o histórico do challenge servido a alunos</item>
///   <item>Manual items (sem fonte) não cabem na tabela challenge</item>
///   <item>Versionamento e histórico de curadoria independente</item>
/// </list>
/// </summary>
public class ModeratorGoldItem
{
    public int  Id        { get; set; }

    /// <summary>Content que esse gold avalia. FK obrigatória.</summary>
    public int  ContentId { get; set; }
    public Domain.Entities.Content Content { get; set; } = null!;

    /// <summary>Moderador que curou (audit). Não cascateia delete —
    /// item de gold sobrevive ao moderador sair da plataforma.</summary>
    public Guid CuratorUserId { get; set; }

    /// <summary>Se vier de uma pergunta gerada que foi promovida a
    /// gold, aponta pra ela. Pra item manual, fica null.</summary>
    public int? SourceGeneratedChallengeId { get; set; }

    // ── Schema espelha GoldItem do YAML (compatibilidade) ───────────

    /// <summary>Afirmação testável do material-fonte (opcional —
    /// moderador pode pular se for óbvio do prompt).</summary>
    public string? SourceClaim { get; set; }

    public string  Prompt          { get; set; } = string.Empty;
    public string  CorrectAnswer   { get; set; } = string.Empty;

    /// <summary>JSON serializado: array de exatamente 3 strings.
    /// Não normalizamos em tabela filha — distratores são lidos
    /// atomicamente com o item, nunca consultados isolado.</summary>
    public string  DistractorsJson { get; set; } = "[]";

    public string? Explanation     { get; set; }

    /// <summary>0.05–0.95. Sugerido pelo moderador; sem default,
    /// se nulo cai pra 0.50 no consumer.</summary>
    public double? DifficultyHint  { get; set; }

    // ── Audit / soft-disable ────────────────────────────────────────

    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool    IsActive    { get; set; } = true;
}
