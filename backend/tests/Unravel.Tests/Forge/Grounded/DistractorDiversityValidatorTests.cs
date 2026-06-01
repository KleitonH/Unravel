using Unravel.Application.Forge.Ports;
using Unravel.Application.Knowledge.Ports;
using Unravel.Infrastructure.Forge.Llm.Grounded.Validators;

namespace Unravel.Tests.Forge.Grounded;

public class DistractorDiversityValidatorTests
{
    private sealed class StubEmbedder : IEmbedder
    {
        private readonly Dictionary<string, float[]> _vectors;
        public int Dimension => 4;
        public StubEmbedder(Dictionary<string, float[]> v) { _vectors = v; }
        public ReadOnlySpan<float> Encode(string text) =>
            _vectors.TryGetValue(text, out var v) ? v : new float[Dimension];
    }

    private static GroundedQuestion Q(string answer, params string[] distractors) =>
        new("Pergunta padrão suficiente.", new[] { answer }.Concat(distractors).ToArray(), 0, "exp", 0);

    private readonly ClaimCandidate _claim = new(0, "chunk corpo", "claim.", 0.5);

    [Fact]
    public void Validate_NoEmbedder_OnlyJaccard()
    {
        var sut = new DistractorDiversityValidator(embedder: null);
        // Distratores muito distintos (Jaccard ~ 0)
        var q = Q(
            "decorator @Component da classe",
            "providers no array de imports",
            "selector com sintaxe de array",
            "renderização condicional");
        Assert.Null(sut.Validate(q, _claim));
    }

    [Fact]
    public void Validate_DistractorTooSimilarToAnswer_Fails()
    {
        var sut = new DistractorDiversityValidator(embedder: null, maxJaccardVsAnswer: 0.40);
        // Distrator copia 3 palavras de 4 → Jaccard alto
        var q = Q(
            "decorator @Component da classe componente Angular moderna",
            "decorator @Component da classe componente Angular básica", // quase igual
            "outro completamente diferente xpto wxyz",
            "outro qualquer wpto ywxz");
        var r = sut.Validate(q, _claim);
        Assert.NotNull(r);
        Assert.Equal(GenerationFailureReason.DistractorsPoor, r!.Value.Reason);
        Assert.Contains("Jaccard", r.Value.Detail);
    }

    [Fact]
    public void Validate_DistractorOffTopic_FailsWhenEmbedderPresent()
    {
        var emb = new StubEmbedder(new()
        {
            ["chunk corpo"] = new float[] { 1, 0, 0, 0 },
            // distrator ortogonal ao chunk
            ["uma banana frita salgada com manteiga"] = new float[] { 0, 1, 0, 0 },
            // outros vão ter cosine 0 com chunk também (default zero vec)
        });
        var sut = new DistractorDiversityValidator(embedder: emb, minCosineVsChunk: 0.5);

        var q = Q(
            "encapsulamento padrão dos estilos",
            "uma banana frita salgada com manteiga",
            "outra opção qualquer aa bb cc dd",
            "terceira opção qualquer xy yz wx");
        var r = sut.Validate(q, _claim);
        Assert.NotNull(r);
        Assert.Equal(GenerationFailureReason.DistractorsPoor, r!.Value.Reason);
    }

    [Fact]
    public void Validate_AllGood_Passes()
    {
        var emb = new StubEmbedder(new()
        {
            ["chunk corpo"]                   = new float[] { 1, 0, 0, 0 },
            ["outra opção qualquer aa bb"]    = new float[] { 0.9f, 0.4f, 0, 0 }, // cos ~0.92
            ["mais uma opção ww xx yy zz"]    = new float[] { 0.85f, 0.5f, 0, 0 }, // cos ~0.86
            ["terceira opção ll mm nn"]       = new float[] { 0.8f, 0.6f, 0, 0 }, // cos ~0.80
        });
        var sut = new DistractorDiversityValidator(emb, maxJaccardVsAnswer: 0.6, minCosineVsChunk: 0.5);
        var q = Q(
            "decorator @Component da classe",
            "outra opção qualquer aa bb",
            "mais uma opção ww xx yy zz",
            "terceira opção ll mm nn");
        Assert.Null(sut.Validate(q, _claim));
    }

    [Fact]
    public void Order_IsThree()
    {
        Assert.Equal(3, new DistractorDiversityValidator().Order);
    }
}
