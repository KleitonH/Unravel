namespace Unravel.Application.Forge.Ports;

/// <summary>
/// Abstração de geração de texto via LLM local. Existe pra desacoplar o
/// <c>LlmChallengeStrategy</c> da implementação concreta (LLamaSharp,
/// ONNX, ou futura troca). Testes injetam um stub.
///
/// <para>Implementação canônica (PR 20) carrega o modelo no construtor
/// (singleton) — instanciar é caro, inferência é barata. Latência típica:
/// 10–60 s por chamada em CPU, dependendo do modelo e tamanho do prompt.</para>
///
/// <para>Por isso o uso é estritamente batch (cron noturno), nunca no
/// caminho síncrono do usuário.</para>
/// </summary>
public interface ILlmInference
{
    /// <summary>Roda o prompt no modelo. Retorna a saída crua (string) ou
    /// <c>null</c> se a inferência falhar/timeout. Implementação aplica
    /// max_tokens + temperature por padrão.</summary>
    Task<string?> CompleteAsync(string prompt, CancellationToken ct = default);
}
