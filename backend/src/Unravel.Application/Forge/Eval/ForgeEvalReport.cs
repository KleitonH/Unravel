using Unravel.Application.Forge.Ports;

namespace Unravel.Application.Forge.Eval;

/// <summary>
/// Resultado completo do <c>forge:eval</c> (PR 33) — alimenta o
/// <c>HtmlReportRenderer</c>.
/// </summary>
public sealed record ForgeEvalReport(
    string                          Trail,
    DateTime                        RunAt,
    string                          ModelName,
    EvalMetrics                     Overall,
    IReadOnlyList<EvalPair>         Pairs,
    IReadOnlyList<TopicAggregation> ByTopic);

/// <summary>Métricas agregadas (overall ou por tópico).</summary>
public sealed record EvalMetrics(
    int    TotalGold,
    int    TotalGeneratedSuccessfully,
    int    TotalGenerationFailed,
    double YieldPercent,             // 0–100
    double AvgPromptCosine,          // 0–1, NaN se sem pares
    double AvgAnswerCosine,          // 0–1
    int    AnswerMatchCount,         // # de pares cuja resposta cosseno ≥ threshold
    double AvgDistractorJaccard,     // 0–1, menor = mais diverso
    Dictionary<GenerationFailureReason, int> FailureBreakdown);

/// <summary>Par gold↔gen pra renderizar lado-a-lado.</summary>
public sealed record EvalPair(
    string             TopicSlug,
    GoldItem           Gold,
    GroundedQuestion?  Generated,
    GenerationFailureReason GeneratedFailure,
    string?            GeneratedFailureDetail,
    double             PromptCosine,         // NaN se gen failed
    double             AnswerCosine,         // NaN se gen failed
    bool               AnswerMatches);       // true se AnswerCosine >= threshold

/// <summary>Agregado por tópico — pra dashboard ranqueado.</summary>
public sealed record TopicAggregation(
    string      TopicSlug,
    EvalMetrics Metrics);
