namespace Unravel.Application.Forge.Ports;

/// <summary>
/// PR 34h — provê (opcionalmente) um <see cref="ILlmInference"/> de tier
/// superior pra usar na última tentativa de geração, quando o modelo
/// padrão (gpt-4o-mini) falhou repetidamente mesmo com reflexion (PR 34g).
///
/// <para><b>Estratégia híbrida</b>: tentativas 1-2 usam o modelo barato;
/// só a cauda difícil (~5-10% dos claims) escala pro modelo melhor
/// (gpt-4o). Custo extra fica restrito a essa fração, evitando rodar o
/// modelo caro pra tudo (15-20x mais caro por token).</para>
///
/// <para><see cref="Inference"/> é <c>null</c> quando não há modelo de
/// escalonamento configurado (<c>Llm:OpenAi:EscalationModel</c> vazio) —
/// nesse caso o generator simplesmente não escala e o comportamento é
/// idêntico ao pré-PR-34h.</para>
/// </summary>
public interface IEscalationLlm
{
    /// <summary>Inferência do modelo superior, ou null se não configurado.</summary>
    ILlmInference? Inference { get; }

    /// <summary>Nome do modelo escalado (telemetria/log). Null se desligado.</summary>
    string? ModelName { get; }

    /// <summary>A partir de qual número de tentativa-anterior escalar.
    /// Default 2 → escala na 3ª tentativa (última, se MaxAttempts=3).</summary>
    int EscalateAfterPriorAttempts { get; }
}
