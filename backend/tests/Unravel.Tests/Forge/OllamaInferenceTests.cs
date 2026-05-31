using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Unravel.Infrastructure.Forge.Llm;

namespace Unravel.Tests.Forge;

/// <summary>
/// Cobre o <see cref="OllamaInference"/> com um <c>HttpMessageHandler</c>
/// que intercepta requests sem precisar de daemon Ollama rodando.
/// Valida shape do request, comportamento em sucesso/erro/timeout, e
/// a flag <c>format: "json"</c>.
/// </summary>
public class OllamaInferenceTests
{
    private static OllamaInference Build(MockHandler handler, string model = "qwen2.5:7b-instruct-q4_K_M",
        bool forceJson = true, float temp = 0.7f, int maxTok = 200, int ctx = 2048)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        return new OllamaInference(http, model, temp, maxTok, ctx, forceJson,
            NullLogger<OllamaInference>.Instance);
    }

    [Fact]
    public async Task Complete_HappyPath_ReturnsResponseField()
    {
        var handler = new MockHandler((req, _) => Task.FromResult(JsonResponse(new
        {
            response = "{\"pergunta\":\"Qual a função do @Component?\"}",
            done     = true,
        })));

        var sut = Build(handler);
        var result = await sut.CompleteAsync("prompt qualquer");

        Assert.NotNull(result);
        Assert.Contains("pergunta", result);
        Assert.Single(handler.Requests);
        Assert.EndsWith("api/generate", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task Complete_SendsCorrectRequestShape_WithFormatJsonWhenEnabled()
    {
        string? bodyCaptured = null;
        var handler = new MockHandler(async (req, _) =>
        {
            bodyCaptured = await req.Content!.ReadAsStringAsync();
            return JsonResponse(new { response = "{}", done = true });
        });
        var sut = Build(handler, forceJson: true, temp: 0.3f, maxTok: 250, ctx: 4096);
        await sut.CompleteAsync("meu prompt");

        Assert.NotNull(bodyCaptured);
        Assert.Contains("\"model\":\"qwen2.5:7b-instruct-q4_K_M\"", bodyCaptured);
        Assert.Contains("\"prompt\":\"meu prompt\"", bodyCaptured);
        Assert.Contains("\"stream\":false", bodyCaptured);
        Assert.Contains("\"format\":\"json\"", bodyCaptured);
        Assert.Contains("\"temperature\":0.3", bodyCaptured);
        Assert.Contains("\"num_predict\":250", bodyCaptured);
        Assert.Contains("\"num_ctx\":4096", bodyCaptured);
    }

    [Fact]
    public async Task Complete_ForceJsonDisabled_OmitsFormatField()
    {
        string? bodyCaptured = null;
        var handler = new MockHandler(async (req, _) =>
        {
            bodyCaptured = await req.Content!.ReadAsStringAsync();
            return JsonResponse(new { response = "free-form text", done = true });
        });
        var sut = Build(handler, forceJson: false);
        await sut.CompleteAsync("prompt");

        // Quando format=null, a serialização do nullable string deve
        // omitir o campo (não enviar "format":null) ou enviar null —
        // qualquer dos dois é OK do lado Ollama, basta não enviar "json".
        Assert.NotNull(bodyCaptured);
        Assert.DoesNotContain("\"format\":\"json\"", bodyCaptured);
    }

    [Fact]
    public async Task Complete_HttpErrorStatus_ReturnsNull()
    {
        var handler = new MockHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("server exploded")
            }));
        var sut = Build(handler);

        var result = await sut.CompleteAsync("anything");
        Assert.Null(result);
    }

    [Fact]
    public async Task Complete_DaemonUnreachable_ReturnsNull()
    {
        var handler = new MockHandler((_, _) => throw new HttpRequestException("connection refused"));
        var sut = Build(handler);

        var result = await sut.CompleteAsync("anything");
        Assert.Null(result);
    }

    [Fact]
    public async Task Complete_NonJsonResponse_ReturnsNull()
    {
        var handler = new MockHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not valid json at all", Encoding.UTF8, "application/json")
        }));
        var sut = Build(handler);

        var result = await sut.CompleteAsync("anything");
        Assert.Null(result);
    }

    [Fact]
    public async Task Complete_ResponseWithDoneFalse_ReturnsNull()
    {
        var handler = new MockHandler((_, _) => Task.FromResult(JsonResponse(new
        {
            response    = "partial",
            done        = false,
            done_reason = "interrupted",
        })));
        var sut = Build(handler);

        var result = await sut.CompleteAsync("anything");
        Assert.Null(result);
    }

    [Fact]
    public async Task Complete_CancellationToken_Propagates()
    {
        using var cts = new CancellationTokenSource();
        var handler = new MockHandler(async (_, ct) =>
        {
            // Simula daemon devagar
            await Task.Delay(5_000, ct);
            return JsonResponse(new { response = "{}", done = true });
        });
        var sut = Build(handler);

        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.CompleteAsync("x", cts.Token));
    }

    // ── Helper: HttpClient mock ────────────────────────────────────

    private static HttpResponseMessage JsonResponse(object payload)
    {
        var msg = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(payload),
        };
        return msg;
    }

    private sealed class MockHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _impl;
        public List<HttpRequestMessage> Requests { get; } = new();

        public MockHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> impl) => _impl = impl;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return await _impl(request, cancellationToken);
        }
    }
}
