using LLama;
using LLama.Common;
using Microsoft.Extensions.Logging;
using Unravel.Application.Forge.Ports;

namespace Unravel.Infrastructure.Forge.Llm;

/// <summary>
/// Implementação real de <see cref="ILlmInference"/> via LLamaSharp
/// (binding .NET pro llama.cpp). Carrega o modelo .gguf no construtor;
/// inferência usa <see cref="StatelessExecutor"/> — cada chamada é
/// independente, sem histórico de chat (apropriado pro nosso caso, onde
/// cada pergunta é gerada do zero).
///
/// <para><b>CPU-only por default</b>: pacote LLamaSharp.Backend.Cpu. Para
/// ligar GPU em dev, configurar <c>Llm:GpuLayerCount &gt; 0</c> e trocar
/// o backend NuGet — sem mudança de código.</para>
///
/// <para><b>Singleton</b>: carregar modelo é caro (~5–15 s para Q4 de
/// 3B). Inferência subsequente reusa contexto.</para>
/// </summary>
public sealed class LLamaSharpInference : ILlmInference, IDisposable
{
    private readonly LLamaWeights _weights;
    private readonly StatelessExecutor _executor;
    private readonly InferenceParams _inferenceParams;
    private readonly ILogger<LLamaSharpInference> _log;

    public LLamaSharpInference(
        string modelPath,
        int gpuLayerCount,
        int contextSize,
        int maxTokens,
        float temperature,
        ILogger<LLamaSharpInference> log)
    {
        _log = log;

        if (!File.Exists(modelPath))
            throw new FileNotFoundException(
                $"Modelo LLM não encontrado em {modelPath}. " +
                "Rode scripts/download-llm.sh ou desligue Llm:Enabled.",
                modelPath);

        var modelParams = new ModelParams(modelPath)
        {
            ContextSize   = (uint)contextSize,
            GpuLayerCount = gpuLayerCount,
            Threads       = Environment.ProcessorCount,
        };

        _log.LogInformation(
            "Loading LLM weights from {Path} (gpuLayers={Gpu}, ctx={Ctx})…",
            modelPath, gpuLayerCount, contextSize);

        _weights = LLamaWeights.LoadFromFile(modelParams);
        _executor = new StatelessExecutor(_weights, modelParams);

        _inferenceParams = new InferenceParams
        {
            MaxTokens          = maxTokens,
            // Stop sequences impedem o modelo de continuar verborrágico
            // depois de fechar o JSON — limita custo de tokens descartados.
            AntiPrompts        = new[] { "}\n\n", "```", "\n\n\n" }.ToList(),
            SamplingPipeline   = new LLama.Sampling.DefaultSamplingPipeline
            {
                Temperature = temperature,
            },
        };

        _log.LogInformation("LLM ready.");
    }

    public async Task<string?> CompleteAsync(string prompt, CancellationToken ct = default)
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            await foreach (var chunk in _executor.InferAsync(prompt, _inferenceParams, ct))
            {
                sb.Append(chunk);
                if (ct.IsCancellationRequested) break;
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "LLM inference threw");
            return null;
        }
    }

    public void Dispose()
    {
        _weights.Dispose();
    }
}
