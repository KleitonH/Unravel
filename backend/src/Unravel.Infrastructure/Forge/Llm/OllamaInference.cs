using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Unravel.Application.Forge.Ports;

namespace Unravel.Infrastructure.Forge.Llm;

/// <summary>
/// Implementação de <see cref="ILlmInference"/> via API HTTP do Ollama
/// (<c>POST /api/generate</c>). Alternativa ao <see cref="LLamaSharpInference"/>
/// para ambientes com GPU dedicada — Ollama cuida do offload pra CUDA
/// transparentemente, e libera o backend de carregar o modelo na RAM
/// do processo .NET.
///
/// <para><b>Quando usar Ollama vs LLamaSharp:</b></para>
/// <list type="bullet">
///   <item><b>Ollama</b> — máquina dev com GPU (ex: RTX 3060 6GB).
///   Modelos maiores (7B), latência baixa (30-50 tok/s), processo
///   .NET fica leve. Backend só fala HTTP, daemon Ollama gerencia
///   modelos e cache de tensors.</item>
///   <item><b>LLamaSharp</b> — VPS CPU-only ou ambiente sem rede.
///   Modelo embarcado (3B), latência alta mas autocontido.</item>
/// </list>
///
/// <para><b>Format = "json"</b>: equivalente nativo do Ollama ao GBNF
/// grammar do llama.cpp. Força o output a ser JSON válido — o parser
/// no <see cref="LlmJsonParser"/> não precisa lidar com prosa antes
/// ou depois do payload.</para>
///
/// <para><b>Stream = false</b>: a) simplifica o código; b) o cron noturno
/// processa N jobs em série, não precisa de UX de "typing".</para>
///
/// <para><b>Idempotente?</b> Não — temperature &gt; 0 dá variância. Pra
/// reprodutibilidade em testes (PR 33), passe temperature=0 via config.
/// </para>
/// </summary>
public sealed class OllamaInference : ILlmInference
{
    // POST /api/generate request — campos obrigatórios primeiro.
    // Veja https://github.com/ollama/ollama/blob/main/docs/api.md
    private sealed record GenerateRequest(
        [property: JsonPropertyName("model")]   string Model,
        [property: JsonPropertyName("prompt")]  string Prompt,
        [property: JsonPropertyName("stream")]  bool   Stream,
        [property: JsonPropertyName("format")]  string? Format,
        [property: JsonPropertyName("options")] OllamaOptions Options);

    private sealed record OllamaOptions(
        [property: JsonPropertyName("temperature")] float Temperature,
        [property: JsonPropertyName("num_predict")] int   NumPredict,
        [property: JsonPropertyName("num_ctx")]     int   NumCtx);

    private sealed record GenerateResponse(
        [property: JsonPropertyName("response")]   string Response,
        [property: JsonPropertyName("done")]       bool   Done,
        [property: JsonPropertyName("done_reason")] string? DoneReason);

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly string     _model;
    private readonly float      _temperature;
    private readonly int        _maxTokens;
    private readonly int        _contextSize;
    private readonly bool       _forceJson;
    private readonly ILogger<OllamaInference> _log;

    public OllamaInference(
        HttpClient http,
        string     model,
        float      temperature,
        int        maxTokens,
        int        contextSize,
        bool       forceJson,
        ILogger<OllamaInference> log)
    {
        _http        = http ?? throw new ArgumentNullException(nameof(http));
        _model       = model;
        _temperature = temperature;
        _maxTokens   = maxTokens;
        _contextSize = contextSize;
        _forceJson   = forceJson;
        _log         = log;

        // Tempo de inferência típico de qwen2.5:7b-Q4 numa RTX 3060
        // pra ~300 tokens output é 6-12s. Margem generosa pra picos.
        if (_http.Timeout == default || _http.Timeout == TimeSpan.FromSeconds(100))
            _http.Timeout = TimeSpan.FromSeconds(120);
    }

    public async Task<string?> CompleteAsync(string prompt, CancellationToken ct = default)
    {
        var payload = new GenerateRequest(
            Model:  _model,
            Prompt: prompt,
            Stream: false,
            Format: _forceJson ? "json" : null,
            Options: new OllamaOptions(
                Temperature: _temperature,
                NumPredict:  _maxTokens,
                NumCtx:      _contextSize));

        try
        {
            using var response = await _http.PostAsJsonAsync("api/generate", payload, JsonOpts, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _log.LogError("Ollama HTTP {Status}: {Body}", (int)response.StatusCode, body);
                return null;
            }

            var parsed = await response.Content.ReadFromJsonAsync<GenerateResponse>(JsonOpts, ct);
            if (parsed is null || !parsed.Done)
            {
                _log.LogWarning("Ollama returned incomplete response (done_reason={Reason})",
                    parsed?.DoneReason ?? "null");
                return null;
            }

            return parsed.Response;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _log.LogError(ex, "Ollama timeout após {Timeout}s", _http.Timeout.TotalSeconds);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _log.LogError(ex, "Ollama HTTP unreachable em {BaseAddress}", _http.BaseAddress);
            return null;
        }
        catch (JsonException ex)
        {
            _log.LogError(ex, "Ollama returned non-JSON response (unexpected)");
            return null;
        }
    }
}
