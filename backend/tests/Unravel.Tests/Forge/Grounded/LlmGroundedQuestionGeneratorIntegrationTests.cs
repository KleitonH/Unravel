using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Unravel.Application.Forge.Ports;
using Unravel.Application.Knowledge.Ports;
using Unravel.Infrastructure.Forge.Llm;
using Unravel.Infrastructure.Forge.Llm.Grounded;
using Unravel.Infrastructure.Forge.Llm.Grounded.Validators;
using Xunit.Abstractions;

namespace Unravel.Tests.Forge.Grounded;

/// <summary>
/// Smoke E2E contra um daemon Ollama real em <c>localhost:11434</c>.
/// Pula automaticamente se Ollama não responder — adapter mockado já
/// cobre lógica unitariamente; este teste valida o pipeline completo
/// (prompt → LLM real → parse → validators) com o modelo configurado.
///
/// <para><b>Modelo esperado:</b> <c>qwen2.5:7b-instruct-q4_K_M</c>.
/// Override via env var <c>UNRAVEL_TEST_OLLAMA_MODEL</c>.</para>
///
/// <para><b>Por que pular se down:</b> CI sem GPU não tem Ollama, e
/// dev local sem o daemon iniciado quer suite verde. Trait
/// <c>Integration=true</c> permite filtrar:</para>
/// <code>dotnet test --filter Integration!=true     # rápido (sem rede)
/// dotnet test --filter Integration=true            # só smoke E2E</code>
/// </summary>
[Trait("Integration", "true")]
public class LlmGroundedQuestionGeneratorIntegrationTests
{
    private readonly ITestOutputHelper _out;
    public LlmGroundedQuestionGeneratorIntegrationTests(ITestOutputHelper @out) => _out = @out;

    private static readonly ClaimCandidate AngularComponentClaim = new(
        ChunkIndex: 0,
        ChunkText: """
            O componente é a unidade básica de construção de qualquer aplicação Angular.
            Um componente é uma classe TypeScript decorada com @Component que controla
            uma porção de tela chamada de view. O decorator @Component marca a classe
            como um componente Angular. O selector define o nome da tag HTML usada
            para inserir o componente em outros templates.
            """,
        ClaimText: "O decorator @Component marca a classe como um componente Angular.",
        Score: 0.94);

    [Fact]
    public async Task Generate_RealOllama_ProducesValidatedQuestion()
    {
        if (!await IsOllamaUpAsync())
        {
            _out.WriteLine("Ollama daemon não respondeu em http://127.0.0.1:11434 — skip.");
            return;
        }

        var model = Environment.GetEnvironmentVariable("UNRAVEL_TEST_OLLAMA_MODEL")
                    ?? "qwen2.5:7b-instruct-q4_K_M";

        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:11434/") };
        var llm = new OllamaInference(
            http,
            model:        model,
            temperature:  0.2f, // baixo pra previsibilidade
            maxTokens:    500,
            contextSize:  4096,
            forceJson:    true,
            log:          new TestOutputLogger<OllamaInference>(_out));

        // Pipeline mínimo: schema + leakage (não dá pra usar embedding
        // aqui sem onnx — esses dois validators são suficientes pro smoke).
        var validators = new IQuestionValidator[]
        {
            new SchemaValidator(),
            new AnswerLeakageValidator(),
        };
        var sut = new LlmGroundedQuestionGenerator(
            llm,
            new ClaimShapeRouter(),   // PR 34a — real router; integração não estuba shape
            validators,
            new TestOutputLogger<LlmGroundedQuestionGenerator>(_out));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await sut.GenerateAsync(AngularComponentClaim, "Componentes Angular");
        sw.Stop();

        _out.WriteLine($"Tempo: {sw.ElapsedMilliseconds}ms");
        _out.WriteLine($"Sucesso: {result.IsSuccess}");
        // Loga sempre, mesmo em falha, pra diagnosticar via output do teste
        if (result.Question is not null)
        {
            _out.WriteLine($"Prompt: {result.Question.Prompt}");
            _out.WriteLine($"Resposta correta: {result.Question.Options[result.Question.CorrectIndex]}");
            _out.WriteLine($"Distratores:");
            for (var i = 0; i < result.Question.Options.Length; i++)
                if (i != result.Question.CorrectIndex)
                    _out.WriteLine($"  - {result.Question.Options[i]}");
            _out.WriteLine($"Explicação: {result.Question.Explanation}");
        }
        if (!result.IsSuccess)
        {
            _out.WriteLine($"Falha: {result.FailureReason} — {result.FailureDetail}");
        }

        Assert.True(result.IsSuccess, $"Geração falhou: {result.FailureReason} — {result.FailureDetail}");
    }

    private static async Task<bool> IsOllamaUpAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var r = await http.GetAsync("http://127.0.0.1:11434/api/tags");
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    /// <summary>Logger mínimo que despeja tudo no test output, pra
    /// erros do OllamaInference aparecerem no resultado do teste.</summary>
    private sealed class TestOutputLogger<T>(ITestOutputHelper @out) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            try { @out.WriteLine($"[{logLevel}] {formatter(state, exception)}"); }
            catch { /* test output já encerrou */ }
        }
    }
}
