using Unravel.Application.Forge.Ports;
using Unravel.Application.Knowledge.Ports;
using Unravel.Infrastructure.Forge.Llm.Grounded.Validators;

namespace Unravel.Tests.Forge.Grounded;

public class AnswerGroundednessValidatorTests
{
    /// <summary>Stub determinístico: hashea a string em N vetores fixos
    /// pra simular embeddings sem precisar de ONNX nos testes.</summary>
    private sealed class StubEmbedder : IEmbedder
    {
        private readonly Dictionary<string, float[]> _vectors;
        public int Dimension => 4;

        public StubEmbedder(Dictionary<string, float[]> vectors) { _vectors = vectors; }

        public ReadOnlySpan<float> Encode(string text)
        {
            if (_vectors.TryGetValue(text, out var v)) return v;
            // Default = zero vector (cosine 0 com tudo)
            return new float[Dimension];
        }
    }

    private static GroundedQuestion Q(string answer) =>
        new("Pergunta longa o suficiente?", new[] { answer, "B", "C", "D" }, 0, "exp", 0);

    [Fact]
    public void Validate_HighCosine_ReturnsNull()
    {
        // Vetores idênticos = cosine 1.0
        var v = new float[] { 1, 0, 0, 0 };
        var emb = new StubEmbedder(new()
        {
            ["chunk text aqui"] = v,
            ["resposta perfeita"] = v,
        });
        var sut = new AnswerGroundednessValidator(emb, threshold: 0.55);

        var claim = new ClaimCandidate(0, "chunk text aqui", "x", 0.5);
        var q     = Q("resposta perfeita");
        Assert.Null(sut.Validate(q, claim));
    }

    [Fact]
    public void Validate_LowCosine_Fails()
    {
        // Vetores ortogonais = cosine 0
        var emb = new StubEmbedder(new()
        {
            ["chunk text aqui"] = new float[] { 1, 0, 0, 0 },
            ["resposta sem nada a ver"] = new float[] { 0, 1, 0, 0 },
        });
        var sut = new AnswerGroundednessValidator(emb, threshold: 0.55);

        var claim = new ClaimCandidate(0, "chunk text aqui", "x", 0.5);
        var q     = Q("resposta sem nada a ver");
        var r = sut.Validate(q, claim);
        Assert.NotNull(r);
        Assert.Equal(GenerationFailureReason.AnswerNotGrounded, r!.Value.Reason);
        Assert.Contains("threshold", r.Value.Detail);
    }

    [Fact]
    public void Validate_AtBoundary_Passes()
    {
        // Cosine = 0.6 (passa o threshold 0.55)
        var emb = new StubEmbedder(new()
        {
            ["chunk"]    = new float[] { 1, 0, 0, 0 },
            ["resposta"] = new float[] { 0.6f, 0.8f, 0, 0 }, // cos(a,b) = 0.6
        });
        var sut = new AnswerGroundednessValidator(emb, threshold: 0.55);
        var claim = new ClaimCandidate(0, "chunk", "x", 0.5);
        Assert.Null(sut.Validate(Q("resposta"), claim));
    }

    [Fact]
    public void Validate_BelowBoundary_Fails()
    {
        var emb = new StubEmbedder(new()
        {
            ["chunk"]    = new float[] { 1, 0, 0, 0 },
            ["resposta"] = new float[] { 0.5f, 0.866f, 0, 0 }, // cos ≈ 0.5
        });
        var sut = new AnswerGroundednessValidator(emb, threshold: 0.55);
        var claim = new ClaimCandidate(0, "chunk", "x", 0.5);
        Assert.NotNull(sut.Validate(Q("resposta"), claim));
    }

    [Fact]
    public void Order_IsTwo()
    {
        var emb = new StubEmbedder(new());
        Assert.Equal(2, new AnswerGroundednessValidator(emb).Order);
    }
}
