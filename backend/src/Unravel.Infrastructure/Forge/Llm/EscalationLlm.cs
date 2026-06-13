using Unravel.Application.Forge.Ports;

namespace Unravel.Infrastructure.Forge.Llm;

/// <summary>
/// PR 34h — implementação simples de <see cref="IEscalationLlm"/>.
/// Carrega (ou não) um <see cref="ILlmInference"/> de tier superior
/// configurado via DI. Quando <see cref="Inference"/> é null, o
/// escalonamento está desligado e o generator opera só com o modelo base.
/// </summary>
public sealed class EscalationLlm : IEscalationLlm
{
    public ILlmInference? Inference { get; }
    public string?        ModelName { get; }
    public int            EscalateAfterPriorAttempts { get; }

    public EscalationLlm(
        ILlmInference? inference,
        string?        modelName,
        int            escalateAfterPriorAttempts)
    {
        Inference                  = inference;
        ModelName                  = modelName;
        EscalateAfterPriorAttempts = escalateAfterPriorAttempts;
    }

    /// <summary>Factory pro caso "desligado" — sem modelo de escalonamento.</summary>
    public static EscalationLlm Disabled => new(null, null, int.MaxValue);
}
