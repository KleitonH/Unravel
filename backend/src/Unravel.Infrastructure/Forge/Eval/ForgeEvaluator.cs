using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unravel.Application.Forge.Eval;
using Unravel.Application.Forge.Ports;
using Unravel.Application.Knowledge.Ports;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Forge.Eval;

/// <summary>
/// Orquestra o eval (PR 33):
/// <list type="number">
///   <item>Carrega gold set</item>
///   <item>Pra cada item: extrai claim correspondente do Content via
///   ClaimExtractor; chama GroundedQuestionGenerator</item>
///   <item>Calcula métricas comparando gold ↔ gen (cosine MiniLM)</item>
///   <item>Retorna <see cref="ForgeEvalReport"/> pronto pra render</item>
/// </list>
///
/// <para><b>Custo</b>: 1 call ao LLM por item de gold completo.
/// 50 itens × ~15s warm = ~12-15min de GPU. Roda sequencial (worker
/// single-threaded — vide PR 32).</para>
///
/// <para><b>Quando IEmbedder não está disponível</b> (Embedding:Enabled=false),
/// cosine retorna NaN e AnswerMatches sempre false. O report ainda
/// mostra yield e failure breakdown — só perde a métrica semântica.</para>
/// </summary>
public sealed class ForgeEvaluator(
    ApplicationDbContext           db,
    IClaimExtractor                claimExtractor,
    IGroundedQuestionGenerator     generator,
    ILogger<ForgeEvaluator>        log,
    IEmbedder?                     embedder = null)
{
    /// <summary>Cosine MiniLM mínimo pra considerar "answer match" no report.
    /// Calibrado em 0.75 (PR 33 decisão) — aceita paráfrase moderada.</summary>
    private const double AnswerMatchThreshold = 0.75;

    public async Task<ForgeEvalReport> RunAsync(
        GoldSet  goldSet,
        string   modelName,
        CancellationToken ct = default)
    {
        log.LogInformation(
            "ForgeEvaluator iniciado. Trail={Trail}, gold items={N}, model={Model}",
            goldSet.Trail, goldSet.Items.Count, modelName);

        var pairs = new List<EvalPair>(goldSet.Items.Count);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        foreach (var gold in goldSet.Items)
        {
            ct.ThrowIfCancellationRequested();
            var pair = await EvaluateOneAsync(gold, ct);
            pairs.Add(pair);

            log.LogInformation(
                "[{Done}/{Total}] {Topic} → {Status} (prompt cos={PCos:F2}, answer cos={ACos:F2})",
                pairs.Count, goldSet.Items.Count,
                gold.TopicSlug,
                pair.Generated is not null ? "OK" : pair.GeneratedFailure.ToString(),
                pair.PromptCosine, pair.AnswerCosine);
        }

        sw.Stop();
        log.LogInformation("Eval concluído em {Elapsed}s", sw.Elapsed.TotalSeconds.ToString("F1"));

        var overall = ComputeMetrics(pairs);
        var byTopic = pairs.GroupBy(p => p.TopicSlug)
            .Select(g => new TopicAggregation(g.Key, ComputeMetrics(g.ToList())))
            .OrderBy(t => t.TopicSlug)
            .ToList();

        return new ForgeEvalReport(
            Trail:     goldSet.Trail,
            RunAt:     DateTime.UtcNow,
            ModelName: modelName,
            Overall:   overall,
            Pairs:     pairs,
            ByTopic:   byTopic);
    }

    private async Task<EvalPair> EvaluateOneAsync(GoldItem gold, CancellationToken ct)
    {
        // 1) Resolve Content do topicSlug
        var content = await db.Content
            .Where(c => c.Slug == gold.TopicSlug)
            .Select(c => new { c.Id, c.Title, c.Body })
            .FirstOrDefaultAsync(ct);
        if (content is null)
            return FailPair(gold, GenerationFailureReason.SchemaInvalid,
                $"Content com slug '{gold.TopicSlug}' não existe no DB. Rode o KnowledgeImporter.");

        // 2) Extrai claims. Estratégia: tenta achar claim cujo texto seja
        //    cosine-próximo ao gold.SourceClaim. Se não tiver embedder,
        //    cai pra match por substring lowercase. Se nada bater, usa
        //    o primeiro claim como fallback (ainda assim avalia generator).
        var claims = claimExtractor.Extract(content.Body);
        if (claims.Count == 0)
            return FailPair(gold, GenerationFailureReason.SchemaInvalid,
                $"ClaimExtractor não produziu nada pra '{gold.TopicSlug}'.");

        var chosenClaim = PickClosestClaim(gold.SourceClaim, claims);

        // 3) Chama o generator (essa é a chamada cara — ~15s LLM)
        var result = await generator.GenerateAsync(chosenClaim, content.Title, ct);

        if (!result.IsSuccess)
            return FailPair(gold, result.FailureReason, result.FailureDetail);

        // 4) Calcula métricas semânticas (NaN se sem embedder)
        var promptCos = CosineSafe(gold.Prompt, result.Question!.Prompt);
        var answerCos = CosineSafe(gold.CorrectAnswer,
            result.Question.Options[result.Question.CorrectIndex]);

        return new EvalPair(
            TopicSlug:              gold.TopicSlug,
            Gold:                   gold,
            Generated:              result.Question,
            GeneratedFailure:       GenerationFailureReason.None,
            GeneratedFailureDetail: null,
            PromptCosine:           promptCos,
            AnswerCosine:           answerCos,
            AnswerMatches:          !double.IsNaN(answerCos) && answerCos >= AnswerMatchThreshold);
    }

    private ClaimCandidate PickClosestClaim(string targetClaim, IReadOnlyList<ClaimCandidate> claims)
    {
        // Com embedder: cosine vs gold.SourceClaim
        if (embedder is not null)
        {
            var goldVec = embedder.Encode(targetClaim).ToArray();
            return claims
                .Select(c => (claim: c, sim: IEmbedder.CosineSimilarity(goldVec, embedder.Encode(c.ClaimText))))
                .OrderByDescending(x => x.sim)
                .First().claim;
        }

        // Sem embedder: heurística mais fraca — overlap de palavras de 5+ chars.
        var goldTokens = Tokenize(targetClaim);
        return claims
            .Select(c => (claim: c, overlap: Tokenize(c.ClaimText).Intersect(goldTokens).Count()))
            .OrderByDescending(x => x.overlap)
            .ThenByDescending(x => x.claim.Score)
            .First().claim;
    }

    private static HashSet<string> Tokenize(string text) =>
        text.ToLowerInvariant()
            .Split(new[] { ' ', '.', ',', ';', ':', '!', '?', '(', ')', '\'', '"' },
                   StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 5)
            .ToHashSet();

    private double CosineSafe(string a, string b)
    {
        if (embedder is null) return double.NaN;
        var va = embedder.Encode(a).ToArray();
        var vb = embedder.Encode(b).ToArray();
        return IEmbedder.CosineSimilarity(va, vb);
    }

    private static EvalPair FailPair(GoldItem gold, GenerationFailureReason reason, string? detail) =>
        new(gold.TopicSlug, gold, null, reason, detail, double.NaN, double.NaN, false);

    private static EvalMetrics ComputeMetrics(IReadOnlyList<EvalPair> pairs)
    {
        var generated = pairs.Where(p => p.Generated is not null).ToList();
        var failed    = pairs.Where(p => p.Generated is null).ToList();

        var failureBreakdown = failed
            .GroupBy(p => p.GeneratedFailure)
            .ToDictionary(g => g.Key, g => g.Count());

        double Avg(IEnumerable<double> vals) =>
            vals.Where(v => !double.IsNaN(v)).DefaultIfEmpty(double.NaN).Average();

        var distractorJaccard = generated
            .Where(p => p.Generated!.Options.Length == 4)
            .Select(p => AvgPairwiseJaccard(p.Generated!.Options
                .Where((_, i) => i != p.Generated.CorrectIndex).ToList()))
            .DefaultIfEmpty(double.NaN)
            .Average();

        return new EvalMetrics(
            TotalGold:                   pairs.Count,
            TotalGeneratedSuccessfully:  generated.Count,
            TotalGenerationFailed:       failed.Count,
            YieldPercent:                pairs.Count == 0 ? 0 : 100.0 * generated.Count / pairs.Count,
            AvgPromptCosine:             Avg(generated.Select(p => p.PromptCosine)),
            AvgAnswerCosine:             Avg(generated.Select(p => p.AnswerCosine)),
            AnswerMatchCount:            generated.Count(p => p.AnswerMatches),
            AvgDistractorJaccard:        distractorJaccard,
            FailureBreakdown:            failureBreakdown);
    }

    private static double AvgPairwiseJaccard(List<string> options)
    {
        if (options.Count < 2) return 0;
        var sets = options.Select(Tokenize).ToList();
        var sum = 0.0;
        var count = 0;
        for (var i = 0; i < sets.Count; i++)
        for (var j = i + 1; j < sets.Count; j++)
        {
            var a = sets[i]; var b = sets[j];
            var union = a.Union(b).Count();
            var inter = a.Intersect(b).Count();
            sum += union == 0 ? 0 : (double)inter / union;
            count++;
        }
        return count == 0 ? 0 : sum / count;
    }
}
