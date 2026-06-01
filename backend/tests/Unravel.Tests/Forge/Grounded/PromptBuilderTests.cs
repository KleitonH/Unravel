using Unravel.Application.Knowledge.Ports;
using Unravel.Infrastructure.Forge.Llm.Grounded;

namespace Unravel.Tests.Forge.Grounded;

public class PromptBuilderTests
{
    [Fact]
    public void Build_IncludesAllRequiredParts()
    {
        var claim = new ClaimCandidate(
            ChunkIndex: 2,
            ChunkText:  "O decorator @Component marca a classe como componente.",
            ClaimText:  "O decorator @Component marca a classe como componente.",
            Score:      0.9);

        var prompt = PromptBuilder.Build("Componentes Angular", claim);

        // Tema
        Assert.Contains("Componentes Angular", prompt);
        // Chunk como fonte
        Assert.Contains("O decorator @Component marca a classe", prompt);
        // Claim alvo
        Assert.Contains("CONCEITO ALVO", prompt);
        // Schema JSON instruído
        Assert.Contains("\"prompt\"", prompt);
        Assert.Contains("\"options\"", prompt);
        Assert.Contains("\"correctIndex\"", prompt);
        Assert.Contains("\"explanation\"", prompt);
        // Instruções críticas
        Assert.Contains("APENAS as informações", prompt);
        Assert.Contains("NÃO repita a resposta", prompt);
    }

    [Fact]
    public void Build_TruncatesLongChunk()
    {
        var bigChunk = new string('x', 5_000);
        var claim = new ClaimCandidate(0, bigChunk, "x.", 0.5);

        var prompt = PromptBuilder.Build("Title", claim);

        Assert.Contains("…", prompt);
        // Prompt total deve ficar abaixo do limite + overhead
        Assert.True(prompt.Length < 5_500,
            $"Prompt deveria caber em <5500 chars com chunk truncado, ficou {prompt.Length}");
    }

    [Fact]
    public void Build_IsDeterministic()
    {
        var claim = new ClaimCandidate(0, "chunk", "claim.", 0.7);
        var a = PromptBuilder.Build("Tema", claim);
        var b = PromptBuilder.Build("Tema", claim);
        Assert.Equal(a, b);
    }
}
