namespace Unravel.Application.Forge.Eval;

/// <summary>
/// Conjunto de perguntas curadas manualmente que serve de "régua" pro
/// <c>forge:eval</c> (PR 33). Cada item descreve UMA pergunta ideal
/// — não é importado pro DB, só usado como referência de comparação.
///
/// <para>Carregado de YAML em <c>backend/knowledge/gold-set/*.yaml</c>
/// pelo <see cref="GoldSetReader"/>.</para>
/// </summary>
public sealed record GoldSet(
    string                  Trail,
    IReadOnlyList<GoldItem> Items);

/// <summary>Uma pergunta curada. Schema espelha o YAML.</summary>
public sealed class GoldItem
{
    /// <summary>Slug do Content correspondente. Tem que existir no DB
    /// após o KnowledgeImporter rodar.</summary>
    public string  TopicSlug     { get; set; } = string.Empty;

    /// <summary>Heading H2/H3 do MD pra ancorar (opcional, debug).</summary>
    public string? ChunkHeading  { get; set; }

    /// <summary>Afirmação testável que essa pergunta exercita.
    /// Usada pra comparar com claims que o ClaimExtractor produziu.</summary>
    public string  SourceClaim   { get; set; } = string.Empty;

    public string  Prompt        { get; set; } = string.Empty;
    public string  CorrectAnswer { get; set; } = string.Empty;

    /// <summary>Exatamente 3 distratores plausíveis. Validar no parser.</summary>
    public List<string> Distractors { get; set; } = new();

    public string  Explanation   { get; set; } = string.Empty;

    /// <summary>0.05–0.95. Quando 0 ou ausente, default 0.50.</summary>
    public double  DifficultyHint { get; set; } = 0.50;

    /// <summary>Item válido = tem todos os campos não-vazios + 3 distratores.
    /// Placeholders TODO retornam false e são ignorados pelo evaluator.</summary>
    public bool IsCompleted() =>
        !string.IsNullOrWhiteSpace(TopicSlug) &&
        !string.IsNullOrWhiteSpace(SourceClaim) &&
        !string.IsNullOrWhiteSpace(Prompt) &&
        !string.IsNullOrWhiteSpace(CorrectAnswer) &&
        Distractors.Count == 3 &&
        Distractors.All(d => !string.IsNullOrWhiteSpace(d)) &&
        !string.IsNullOrWhiteSpace(Explanation);
}
