using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Unravel.Infrastructure.Forge.Llm;

namespace Unravel.Tests.Forge;

/// <summary>
/// Cobre OpenAiInference (PR 33g) com HttpMessageHandler mock.
/// Espelha cobertura do OllamaInferenceTests pra paridade entre
/// providers — qualquer feature funcional no Ollama tem teste
/// equivalente aqui.
/// </summary>
public class OpenAiInferenceTests
{
    private static OpenAiInference Build(MockHandler handler,
        string apiKey = "sk-test-fake",
        string model = "gpt-4o-mini",
        bool forceJson = true,
        float temp = 0.3f, int maxTok = 500)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/") };
        return new OpenAiInference(http, apiKey, model, temp, maxTok, forceJson,
            NullLogger<OpenAiInference>.Instance);
    }

    [Fact]
    public async Task Complete_HappyPath_ReturnsMessageContent()
    {
        var handler = new MockHandler((_, _) => Task.FromResult(JsonResponse(new
        {
            choices = new[]
            {
                new { message = new { role = "assistant", content = "{\"prompt\":\"Q?\"}" },
                      finish_reason = "stop" }
            },
            usage = new { prompt_tokens = 100, completion_tokens = 50, total_tokens = 150 }
        })));

        var sut = Build(handler);
        var result = await sut.CompleteAsync("test prompt");

        Assert.NotNull(result);
        Assert.Equal("{\"prompt\":\"Q?\"}", result);
        Assert.Single(handler.Requests);
        Assert.EndsWith("v1/chat/completions", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task Complete_SendsBearerAuth()
    {
        AuthenticationHeaderValue? authCaptured = null;
        var handler = new MockHandler((req, _) =>
        {
            authCaptured = req.Headers.Authorization;
            return Task.FromResult(JsonResponse(new
            {
                choices = new[] { new { message = new { role = "assistant", content = "{}" },
                                        finish_reason = "stop" } },
                usage = new { prompt_tokens = 0, completion_tokens = 0, total_tokens = 0 }
            }));
        });
        var sut = Build(handler, apiKey: "sk-my-key-123");
        await sut.CompleteAsync("x");

        Assert.NotNull(authCaptured);
        Assert.Equal("Bearer", authCaptured!.Scheme);
        Assert.Equal("sk-my-key-123", authCaptured.Parameter);
    }

    [Fact]
    public async Task Complete_RequestShape_IncludesModelTemperatureMaxTokensAndJsonFormat()
    {
        string? bodyCaptured = null;
        var handler = new MockHandler(async (req, _) =>
        {
            bodyCaptured = await req.Content!.ReadAsStringAsync();
            return JsonResponse(new
            {
                choices = new[] { new { message = new { role = "assistant", content = "{}" },
                                        finish_reason = "stop" } },
                usage = new { prompt_tokens = 0, completion_tokens = 0, total_tokens = 0 }
            });
        });
        var sut = Build(handler, model: "gpt-4o-mini", temp: 0.3f, maxTok: 500, forceJson: true);
        await sut.CompleteAsync("my prompt");

        Assert.NotNull(bodyCaptured);
        Assert.Contains("\"model\":\"gpt-4o-mini\"", bodyCaptured);
        Assert.Contains("\"messages\":[", bodyCaptured);
        Assert.Contains("\"role\":\"user\"", bodyCaptured);
        Assert.Contains("\"content\":\"my prompt\"", bodyCaptured);
        Assert.Contains("\"temperature\":0.3", bodyCaptured);
        Assert.Contains("\"max_tokens\":500", bodyCaptured);
        Assert.Contains("\"response_format\":{\"type\":\"json_object\"}", bodyCaptured);
    }

    [Fact]
    public async Task Complete_ForceJsonFalse_OmitsResponseFormat()
    {
        string? bodyCaptured = null;
        var handler = new MockHandler(async (req, _) =>
        {
            bodyCaptured = await req.Content!.ReadAsStringAsync();
            return JsonResponse(new
            {
                choices = new[] { new { message = new { role = "assistant", content = "free text" },
                                        finish_reason = "stop" } },
                usage = new { prompt_tokens = 0, completion_tokens = 0, total_tokens = 0 }
            });
        });
        var sut = Build(handler, forceJson: false);
        await sut.CompleteAsync("x");

        Assert.NotNull(bodyCaptured);
        Assert.DoesNotContain("response_format", bodyCaptured);
    }

    [Fact]
    public async Task Complete_HttpErrorStatus_ReturnsNull()
    {
        var handler = new MockHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"error\":{\"message\":\"Invalid API key\"}}")
            }));
        var sut = Build(handler);

        var result = await sut.CompleteAsync("x");
        Assert.Null(result);
    }

    [Fact]
    public async Task Complete_RateLimitError_ReturnsNull()
    {
        var handler = new MockHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("Rate limit exceeded")
            }));
        var sut = Build(handler);

        var result = await sut.CompleteAsync("x");
        Assert.Null(result);
    }

    [Fact]
    public async Task Complete_NetworkUnreachable_ReturnsNull()
    {
        var handler = new MockHandler((_, _) => throw new HttpRequestException("connection refused"));
        var sut = Build(handler);

        var result = await sut.CompleteAsync("x");
        Assert.Null(result);
    }

    [Fact]
    public async Task Complete_NoChoices_ReturnsNull()
    {
        var handler = new MockHandler((_, _) => Task.FromResult(JsonResponse(new
        {
            choices = Array.Empty<object>(),
            usage = new { prompt_tokens = 0, completion_tokens = 0, total_tokens = 0 }
        })));
        var sut = Build(handler);

        var result = await sut.CompleteAsync("x");
        Assert.Null(result);
    }

    [Fact]
    public async Task Complete_FinishReasonLength_StillReturnsContent()
    {
        // Truncated por max_tokens — backend ainda devolve o conteúdo
        // (pode estar truncado); parser à frente decide se aceita.
        var handler = new MockHandler((_, _) => Task.FromResult(JsonResponse(new
        {
            choices = new[]
            {
                new { message = new { role = "assistant", content = "{\"prompt\":\"Quebrad" },
                      finish_reason = "length" }
            },
            usage = new { prompt_tokens = 100, completion_tokens = 500, total_tokens = 600 }
        })));
        var sut = Build(handler);

        var result = await sut.CompleteAsync("x");
        Assert.NotNull(result);
        Assert.Contains("Quebrad", result);
    }

    [Fact]
    public void Constructor_EmptyApiKey_Throws()
    {
        var handler = new MockHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/") };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new OpenAiInference(http, "", "gpt-4o-mini", 0.3f, 500, true,
                NullLogger<OpenAiInference>.Instance));
        Assert.Contains("ApiKey", ex.Message);
        Assert.Contains("user-secrets", ex.Message);
    }

    [Fact]
    public async Task Complete_CancellationToken_Propagates()
    {
        using var cts = new CancellationTokenSource();
        var handler = new MockHandler(async (_, ct) =>
        {
            await Task.Delay(5_000, ct);
            return JsonResponse(new
            {
                choices = new[] { new { message = new { role = "a", content = "x" }, finish_reason = "stop" } },
                usage  = new { prompt_tokens = 0, completion_tokens = 0, total_tokens = 0 }
            });
        });
        var sut = Build(handler);

        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.CompleteAsync("x", cts.Token));
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static HttpResponseMessage JsonResponse(object payload) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(payload) };

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
