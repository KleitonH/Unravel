using Microsoft.Extensions.Logging.Abstractions;
using Unravel.Application.Forge.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.Knowledge;
using Unravel.Infrastructure.Forge.Strategies;

namespace Unravel.Tests.Forge;

public class LlmChallengeStrategyTests
{
    private static Topic SampleTopic() =>
        new(1, contentId: 1, trailId: 1, slug: "t",
            keywords: Array.Empty<Keyword>(), difficultyScore: 0.5, originalOrder: 0);

    private static Content SampleContent() =>
        new() { Id = 1, TrailId = 1, Title = "T", Body = "Body", IsActive = true };

    private static KnowledgeGraph SampleGraph() =>
        new(1, new[] { SampleTopic() }, Array.Empty<PrerequisiteEdge>());

    /// <summary>Stub que devolve um JSON pré-cozido — desacopla testes
    /// da inferência real (não precisa modelo).</summary>
    private sealed class StubInference : ILlmInference
    {
        private readonly Queue<string?> _responses;
        public int Calls { get; private set; }
        public StubInference(params string?[] responses) => _responses = new(responses);

        public Task<string?> CompleteAsync(string prompt, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(_responses.Count > 0 ? _responses.Dequeue() : null);
        }
    }

    [Fact]
    public void Generate_AllValid_ReturnsAllAsDrafts()
    {
        const string good = """
{ "prompt": "Q?", "options": ["a", "b", "c", "d"], "correctIndex": 1, "explanation": "..." }
""";
        var stub  = new StubInference(good, good, good);
        var sut   = new LlmChallengeStrategy(stub, NullLogger<LlmChallengeStrategy>.Instance);

        var drafts = sut.Generate(SampleContent(), SampleTopic(), SampleGraph(), maxDrafts: 3);

        Assert.Equal(3, drafts.Count);
        Assert.Equal(3, stub.Calls);
    }

    [Fact]
    public void Generate_MixedValidAndInvalid_KeepsOnlyValid()
    {
        const string good = """
{ "prompt": "Q?", "options": ["a", "b", "c", "d"], "correctIndex": 0 }
""";
        var stub  = new StubInference(good, "garbage", good, null);
        var sut   = new LlmChallengeStrategy(stub, NullLogger<LlmChallengeStrategy>.Instance);

        var drafts = sut.Generate(SampleContent(), SampleTopic(), SampleGraph(), maxDrafts: 4);

        Assert.Equal(2, drafts.Count);   // dois 'good'; "garbage" + null descartados
    }

    [Fact]
    public void Generate_LlmThrows_StopsEarly()
    {
        var sut = new LlmChallengeStrategy(new ThrowingInference(), NullLogger<LlmChallengeStrategy>.Instance);
        var drafts = sut.Generate(SampleContent(), SampleTopic(), SampleGraph(), maxDrafts: 5);
        Assert.Empty(drafts);
    }

    private sealed class ThrowingInference : ILlmInference
    {
        public Task<string?> CompleteAsync(string prompt, CancellationToken ct = default)
            => throw new InvalidOperationException("boom");
    }
}
