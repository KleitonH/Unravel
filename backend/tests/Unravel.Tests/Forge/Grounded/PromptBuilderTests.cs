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
        // PR 33e: regras críticas calibradas pelo eval real
        Assert.Contains("FIDELIDADE", prompt);
        Assert.Contains("NÃO VAZAMENTO", prompt);
        Assert.Contains("RESPOSTA SUBSTANTIVA", prompt);
        Assert.Contains("DISTRATORES", prompt);
        // Few-shot example presente
        Assert.Contains("PERGUNTA RUIM", prompt);
        Assert.Contains("PERGUNTA BOA", prompt);
    }

    [Fact]
    public void Build_TruncatesLongChunk()
    {
        var bigChunk = new string('x', 5_000);
        var claim = new ClaimCandidate(0, bigChunk, "x.", 0.5);

        var prompt = PromptBuilder.Build("Title", claim);

        Assert.Contains("…", prompt);
        // PR 33e: cap do chunk em 3000 + few-shot ~3000 chars de overhead
        // = ~6500 chars total no pior caso. Antes era 5500 sem few-shot.
        Assert.True(prompt.Length < 7_000,
            $"Prompt deveria caber em <7000 chars com chunk truncado, ficou {prompt.Length}");
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
