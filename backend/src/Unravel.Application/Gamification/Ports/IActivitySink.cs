using Unravel.Domain.Gamification;

namespace Unravel.Application.Gamification.Ports;

/// <summary>
/// Recebe eventos de atividade do aluno (respondeu pergunta, acertou, encarou
/// Boss…) e avança as missões diárias que casam com o tipo. Cada missão que
/// <b>fecha</b> credita +1 no novelo das parcerias e pontos na meta da caixinha
/// — as missões são a "unidade" de progresso social.
///
/// <para>Best-effort por contrato: a implementação nunca deve lançar — uma
/// falha aqui não pode quebrar o fluxo de estudo (submit do quiz).</para>
/// </summary>
public interface IActivitySink
{
    Task RecordAsync(Guid userId, ActivityKind kind, int count, DateTime asOfUtc, CancellationToken ct = default);
}
