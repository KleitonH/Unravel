namespace Unravel.Application.Forge.Ports;

/// <summary>
/// Roda o lote noturno de geração via LLM. Por (Content ativo) com pool
/// abaixo de <c>minPoolSize</c>, pede ao <c>ChallengeForge</c> pra gerar
/// usando todas as estratégias disponíveis (incluindo a LLM, se ativa)
/// e persiste os novos drafts.
///
/// <para>Hosted service (Infrastructure) chama uma vez por noite; também
/// exposto como port para que um endpoint de admin possa disparar
/// manualmente (útil pra demo).</para>
/// </summary>
public interface ILlmGenerationOrchestrator
{
    Task<LlmGenerationReport> RunAsync(int minPoolSize = 5, int targetPerContent = 8, CancellationToken ct = default);
}

public sealed record LlmGenerationReport(
    int ContentsScanned,
    int ContentsAugmented,
    int DraftsAdded,
    int Failures,
    TimeSpan Duration);
