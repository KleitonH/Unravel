namespace Unravel.Domain.Forge;

/// <summary>
/// Pergunta gerada por uma <c>IChallengeStrategy</c> antes da validação do
/// <c>QualityGate</c> e antes de ser persistida como <c>GeneratedChallenge</c>.
/// Imutável; carrega a fonte (topic/content) e o nome da estratégia para
/// auditoria — assim, se uma pergunta confundir o usuário, conseguimos
/// rastrear que regra a produziu.
/// </summary>
public sealed record GeneratedChallengeDraft(
    int                     SourceTopicId,
    int                     SourceContentId,
    ForgeStrategy           Strategy,
    string                  Prompt,
    IReadOnlyList<string>   Options,
    int                     CorrectIndex,
    string?                 Explanation,
    double                  EstimatedDifficulty   // 0..1, alinhada com Topic.DifficultyScore
);

/// <summary>Identifica a regra que produziu o draft. String estável usada
/// na persistência e na análise (taxa de erro por estratégia).</summary>
public enum ForgeStrategy
{
    Cloze        = 1,   // "____ é X" — preencher lacuna
    Definition   = 2,   // "O que é X?" — definição inversa
    TrueFalse    = 3,   // afirmação verdadeira vs. mutação falsa
    Ordering     = 4,   // (PR 5)
    Match        = 5,   // (PR 5)
    Code         = 6,   // (PR 5)
}
