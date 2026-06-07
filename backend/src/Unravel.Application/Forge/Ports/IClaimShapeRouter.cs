using Unravel.Application.Forge.Llm;
using Unravel.Application.Knowledge.Ports;

namespace Unravel.Application.Forge.Ports;

/// <summary>
/// Decide qual <see cref="QuestionShape"/> melhor se encaixa num
/// <see cref="ClaimCandidate"/>. Heurístico (não-ML) — features do
/// claim definem o shape; nada de chamada externa nem estado.
///
/// <para><b>Por que existe</b>: o pipeline LlmGrounded (PR 31)
/// historicamente gerava só <see cref="QuestionShape.MultipleChoice"/>.
/// PR 34 introduz variedade visual sem mudar o pipeline subjacente —
/// o router é o ponto único de decisão. Trocar a heurística por um
/// classificador ML no futuro não toca os call-sites.</para>
///
/// <para><b>Determinismo</b>: mesma entrada → mesmo shape. Permite que
/// re-execuções (retry de job falho, eval no gold set, replay em testes)
/// sejam reprodutíveis.</para>
/// </summary>
public interface IClaimShapeRouter
{
    /// <summary>Devolve o shape escolhido e a razão (string curta usada
    /// como tag OTel <c>forge.shape.reason</c> — útil pra entender por
    /// que o router decidiu o que decidiu sem precisar reproduzir).</summary>
    ShapeDecision Route(ClaimCandidate claim);
}

/// <summary>Resultado da decisão. <see cref="Reason"/> é livre-forma mas
/// estável (usado em métricas, então não pode mudar a cada release sem
/// quebrar dashboards).</summary>
public sealed record ShapeDecision(QuestionShape Shape, string Reason);
