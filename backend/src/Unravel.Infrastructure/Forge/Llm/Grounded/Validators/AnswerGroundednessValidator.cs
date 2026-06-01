using Unravel.Application.Forge.Ports;
using Unravel.Application.Knowledge.Ports;

namespace Unravel.Infrastructure.Forge.Llm.Grounded.Validators;

/// <summary>
/// Garante que a resposta correta tem suporte semântico no chunk-fonte.
/// Usa MiniLM (PR 18) pra calcular cosine similarity entre a resposta
/// e o chunk; rejeita se cosine &lt; threshold.
///
/// <para>Threshold default <b>0.55</b> (PR 31 decisão calibrada):
/// aceita paráfrases moderadas, rejeita respostas inventadas pela LLM
/// que não estejam no chunk. Calibração final acontece no PR 33 com
/// gold set.</para>
///
/// <para>Por que cosine e não exact match: o LLM pode reformular a
/// resposta ("o @Component marca a classe" vira "o decorator anota
/// a classe como componente") — exact match falharia, mas a relação
/// semântica está preservada. MiniLM detecta paráfrase com confiança
/// razoável em PT-BR.</para>
///
/// <para>Ordem 2 — depois de schema+leakage (que são string ops); este
/// faz embedding (~10-30ms por chamada, paga o overhead só se passou
/// nos baratos).</para>
///
/// <para>Quando <see cref="IEmbedder"/> não está registrado no DI
/// (<c>Embedding:Enabled=false</c>), este validador é dispensado pelo
/// gerador — descrito em <see cref="LlmGroundedQuestionGenerator"/>.
/// </para>
/// </summary>
public sealed class AnswerGroundednessValidator : IQuestionValidator
{
    private readonly IEmbedder _embedder;
    private readonly double    _threshold;

    public AnswerGroundednessValidator(IEmbedder embedder, double threshold = 0.55)
    {
        _embedder  = embedder;
        _threshold = threshold;
    }

    public int Order => 2;

    public (GenerationFailureReason Reason, string Detail)? Validate(
        GroundedQuestion question, ClaimCandidate claim)
    {
        var answer = question.Options[question.CorrectIndex];

        var chunkVec  = _embedder.Encode(claim.ChunkText);
        var answerVec = _embedder.Encode(answer);

        var sim = IEmbedder.CosineSimilarity(chunkVec, answerVec);

        if (sim < _threshold)
            return (GenerationFailureReason.AnswerNotGrounded,
                $"Cosine(answer↔chunk)={sim:F3} < threshold {_threshold:F2}");

        return null;
    }
}
