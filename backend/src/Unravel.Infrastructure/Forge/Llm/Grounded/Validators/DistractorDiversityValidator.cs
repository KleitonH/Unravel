using Unravel.Application.Forge.Ports;
using Unravel.Application.Knowledge.Ports;

namespace Unravel.Infrastructure.Forge.Llm.Grounded.Validators;

/// <summary>
/// Garante qualidade dos distratores:
/// <list type="number">
///   <item><b>Diversidade vs resposta</b>: cada distrator tem
///   Jaccard(tokens) &lt; <c>maxJaccardVsAnswer</c> com a resposta —
///   distratores não podem ser quase-cópias da resposta com leves
///   variações.</item>
///   <item><b>Plausibilidade do domínio</b>: cada distrator tem
///   cosine(distractor↔chunk) ≥ <c>minCosineVsChunk</c> — distratores
///   devem ao menos pertencer ao tema (não "banana" se a pergunta é
///   sobre Angular).</item>
/// </list>
///
/// <para>Ambos opcionais: se <c>IEmbedder</c> não está disponível, só
/// o check de Jaccard roda (Jaccard é puro string).</para>
///
/// <para>Ordem 3 — mais caro (3 embeddings por chamada de validador
/// pra distratores se embedder presente).</para>
/// </summary>
public sealed class DistractorDiversityValidator : IQuestionValidator
{
    private readonly IEmbedder? _embedder;
    private readonly double     _maxJaccardVsAnswer;
    private readonly double     _minCosineVsChunk;

    public DistractorDiversityValidator(
        IEmbedder? embedder            = null,
        double     maxJaccardVsAnswer  = 0.60,
        double     minCosineVsChunk    = 0.35)
    {
        _embedder           = embedder;
        _maxJaccardVsAnswer = maxJaccardVsAnswer;
        _minCosineVsChunk   = minCosineVsChunk;
    }

    public int Order => 3;

    public (GenerationFailureReason Reason, string Detail)? Validate(
        GroundedQuestion question, ClaimCandidate claim)
    {
        var answer       = question.Options[question.CorrectIndex];
        var answerTokens = Tokenize(answer);

        // Materializa o vetor do chunk uma única vez (Encode retorna
        // ReadOnlySpan que não pode ser capturado em loop). float[]
        // é alocação de ~1.5KB pra MiniLM-L12-384 — irrelevante.
        var chunkVec = _embedder?.Encode(claim.ChunkText).ToArray();

        for (var i = 0; i < question.Options.Length; i++)
        {
            if (i == question.CorrectIndex) continue;
            var distractor = question.Options[i];

            // 1) Jaccard vs answer
            var distractorTokens = Tokenize(distractor);
            var jacc = Jaccard(answerTokens, distractorTokens);
            if (jacc > _maxJaccardVsAnswer)
                return (GenerationFailureReason.DistractorsPoor,
                    $"Distrator[{i}] Jaccard={jacc:F2} vs resposta (max {_maxJaccardVsAnswer:F2}): \"{distractor}\"");

            // 2) Cosine vs chunk (se embedder disponível)
            if (_embedder is not null && chunkVec is not null)
            {
                var distractorVec = _embedder.Encode(distractor);
                var cos = IEmbedder.CosineSimilarity(chunkVec, distractorVec);
                if (cos < _minCosineVsChunk)
                    return (GenerationFailureReason.DistractorsPoor,
                        $"Distrator[{i}] cosine={cos:F2} vs chunk (min {_minCosineVsChunk:F2}): \"{distractor}\"");
            }
        }

        return null;
    }

    private static HashSet<string> Tokenize(string text) =>
        text.ToLowerInvariant()
            .Split(new[] { ' ', '.', ',', ';', ':', '!', '?', '(', ')', '"', '\'', '/', '-' },
                   StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 3) // stopwords curtas
            .ToHashSet();

    private static double Jaccard(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 && b.Count == 0) return 1.0;
        var intersection = a.Intersect(b).Count();
        var union        = a.Union(b).Count();
        return union == 0 ? 0 : (double)intersection / union;
    }
}
