using Unravel.Application.Forge.Ports;
using Unravel.Application.Knowledge.Ports;

namespace Unravel.Infrastructure.Forge.Llm.Grounded.Validators;

/// <summary>
/// Cada validador da cadeia checa um aspecto específico da pergunta
/// gerada (schema, leak, grounding, distratores). A interface é
/// minimalista pra facilitar composição via DI <c>IEnumerable&lt;...&gt;</c>.
///
/// <para>Convenção: validador retorna <c>null</c> se passou; senão
/// retorna <see cref="GenerationFailureReason"/> + detalhe.
/// </para>
/// </summary>
public interface IQuestionValidator
{
    /// <summary>Ordem de execução na chain — menor primeiro.
    /// Validador barato (sem cosine/embedding) roda antes, pra
    /// short-circuit em caso de schema inválido.</summary>
    int Order { get; }

    (GenerationFailureReason Reason, string Detail)? Validate(
        GroundedQuestion question,
        ClaimCandidate   claim);
}
