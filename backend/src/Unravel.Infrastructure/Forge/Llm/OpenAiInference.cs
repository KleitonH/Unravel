using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Unravel.Application.Forge.Ports;

namespace Unravel.Infrastructure.Forge.Llm;

/// <summary>
/// Implementação de <see cref="ILlmInference"/> via OpenAI Chat
/// Completions API (PR 33g). Terceira opção de provider além de
/// LLamaSharp (PR 20) e Ollama (PR 30).
///
/// <para><b>Quando usar este provider</b>:</para>
/// <list type="bullet">
///   <item><b>Produção</b>: yield muito alto (~70%+) com gpt-4o-mini
///   por ~$0.001 por pergunta. Pra Unravel com 1k alunos, custo
///   mensal estimado &lt;$5.</item>
///   <item><b>Eval / calibragem</b>: roda em segundos, dá baseline
///   de yield "se modelo fosse perfeito" — separa problema de
///   pipeline vs problema de modelo.</item>
///   <item><b>Demo TCC</b>: NÃO usar — default config mantém
///   Ollama pra defender argumento de self-contained.</item>
/// </list>
///
/// <para><b>Quando NÃO usar</b>:</para>
/// <list type="bullet">
///   <item>Dev local iterativo (queima crédito à toa — use Ollama)</item>
///   <item>Conteúdo sensível (passa por OpenAI; LGPD/política)</item>
///   <item>Sem conexão / oferecer offline-first</item>
/// </list>
///
/// <para><b>response_format: json_object</b> — equivalente ao
/// <c>format: json</c> do Ollama. Força output JSON válido. Disponível
/// em gpt-4o, gpt-4o-mini, gpt-4-turbo e versões 3.5-turbo-1106+.
/// Pra modelos sem suporte (gpt-3.5-turbo legado), o backend ainda
/// funciona — só sem garantia de JSON estrito.</para>
///
/// <para><b>API key</b>: lida da config <c>Llm:OpenAi:ApiKey</c>.
/// Em dev, usar <c>dotnet user-secrets</c> ou env var
/// <c>OPENAI_API_KEY</c>. NUNCA commitar no appsettings.json.</para>
/// </summary>
public sealed class OpenAiInference : ILlmInference
{
    private const string ChatCompletionsEndpoint = "v1/chat/completions";

    private sealed record ChatCompletionRequest(
        [property: JsonPropertyName("model")]       string Model,
        [property: JsonPropertyName("messages")]    Message[] Messages,
        [property: JsonPropertyName("temperature")] float Temperature,
        [property: JsonPropertyName("max_tokens")]  int MaxTokens,
        [property: JsonPropertyName("response_format")] ResponseFormat? ResponseFormat);

    private sealed record Message(
        [property: JsonPropertyName("role")]    string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ResponseFormat(
        [property: JsonPropertyName("type")] string Type);

    private sealed record ChatCompletionResponse(
        [property: JsonPropertyName("choices")] Choice[] Choices,
        [property: JsonPropertyName("usage")]   Usage? Usage);

    private sealed record Choice(
        [property: JsonPropertyName("message")]       Message Message,
        [property: JsonPropertyName("finish_reason")] string? FinishReason);

    private sealed record Usage(
        [property: JsonPropertyName("prompt_tokens")]     int PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int CompletionTokens,
        [property: JsonPropertyName("total_tokens")]      int TotalTokens);

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly string     _model;
    private readonly float      _temperature;
    private readonly int        _maxTokens;
    private readonly bool       _forceJson;
    private readonly ILogger<OpenAiInference> _log;

    public OpenAiInference(
        HttpClient http,
        string     apiKey,
        string     model,
        float      temperature,
        int        maxTokens,
        bool       forceJson,
        ILogger<OpenAiInference> log)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "Llm:OpenAi:ApiKey está vazio. Configure via " +
                "`dotnet user-secrets set \"Llm:OpenAi:ApiKey\" \"sk-...\"` " +
                "ou env var OPENAI_API_KEY antes de subir a API.");

        _http        = http ?? throw new ArgumentNullException(nameof(http));
        _model       = model;
        _temperature = temperature;
        _maxTokens   = maxTokens;
        _forceJson   = forceJson;
        _log         = log;

        // Bearer auth nos headers — feito uma vez no construtor.
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);

        // Timeout 90s — OpenAI gpt-4o-mini tipicamente responde em
        // 1-3s pra prompts do nosso tamanho. 90s é margem generosa
        // pra picos de rate-limit ou network.
        if (_http.Timeout == default || _http.Timeout == TimeSpan.FromSeconds(100))
            _http.Timeout = TimeSpan.FromSeconds(90);
    }

    public async Task<string?> CompleteAsync(string prompt, CancellationToken ct = default)
    {
        var payload = new ChatCompletionRequest(
            Model:       _model,
            Messages:    new[] { new Message("user", prompt) },
            Temperature: _temperature,
            MaxTokens:   _maxTokens,
            ResponseFormat: _forceJson ? new ResponseFormat("json_object") : null);

        try
        {
            using var response = await _http.PostAsJsonAsync(ChatCompletionsEndpoint, payload, JsonOpts, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _log.LogError("OpenAI HTTP {Status}: {Body}", (int)response.StatusCode, Truncate(body, 500));
                return null;
            }

            var parsed = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(JsonOpts, ct);
            if (parsed is null || parsed.Choices.Length == 0)
            {
                _log.LogWarning("OpenAI retornou sem choices.");
                return null;
            }

            var choice = parsed.Choices[0];
            if (parsed.Usage is { } u)
                _log.LogDebug("OpenAI tokens: {Prompt}→{Completion}={Total}",
                    u.PromptTokens, u.CompletionTokens, u.TotalTokens);

            // finish_reason: "stop" = OK; "length" = truncado (max_tokens);
            // "content_filter" = bloqueado. Pra qualquer não-stop loga warn
            // mas ainda devolve o conteúdo parcial — o JSON pode estar
            // truncado mas o parser tenta.
            if (choice.FinishReason is not null && choice.FinishReason != "stop")
                _log.LogWarning("OpenAI finish_reason={Reason} (esperado 'stop')",
                    choice.FinishReason);

            return choice.Message.Content;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _log.LogError(ex, "OpenAI timeout após {Timeout}s", _http.Timeout.TotalSeconds);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _log.LogError(ex, "OpenAI HTTP unreachable em {BaseAddress}", _http.BaseAddress);
            return null;
        }
        catch (JsonException ex)
        {
            _log.LogError(ex, "OpenAI retornou body não-JSON (inesperado)");
            return null;
        }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
