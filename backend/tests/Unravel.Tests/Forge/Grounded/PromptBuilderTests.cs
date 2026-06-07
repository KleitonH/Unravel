using Unravel.Application.Forge.Llm;
using Unravel.Application.Knowledge.Ports;
using Unravel.Infrastructure.Forge.Llm.Grounded;

namespace Unravel.Tests.Forge.Grounded;

/// <summary>
/// Cobre o dispatcher <see cref="PromptBuilder"/> (PR 34a) e mantém
/// asserções estruturais do prompt MCQ (PR 33e). Prompts shape-específicos
/// têm assertivas adicionais cobrindo seus marcadores únicos.
/// </summary>
public class PromptBuilderTests
{
    private static ClaimCandidate SampleClaim() => new(
        ChunkIndex: 2,
        ChunkText:  "O decorator @Component marca a classe como componente.",
        ClaimText:  "O decorator @Component marca a classe como componente.",
        Score:      0.9);

    // ─── MultipleChoice (PR 33e) ──────────────────────────────────────

    [Fact]
    public void Build_MCQ_IncludesAllRequiredParts()
    {
        var prompt = PromptBuilder.Build(QuestionShape.MultipleChoice, "Componentes Angular", SampleClaim());

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
    public void Build_MCQ_TruncatesLongChunk()
    {
        var bigChunk = new string('x', 5_000);
        var claim = new ClaimCandidate(0, bigChunk, "x.", 0.5);

        var prompt = PromptBuilder.Build(QuestionShape.MultipleChoice, "Title", claim);

        Assert.Contains("…", prompt);
        Assert.True(prompt.Length < 7_000,
            $"Prompt deveria caber em <7000 chars com chunk truncado, ficou {prompt.Length}");
    }

    [Fact]
    public void Build_MCQ_IsDeterministic()
    {
        var claim = new ClaimCandidate(0, "chunk", "claim.", 0.7);
        var a = PromptBuilder.Build(QuestionShape.MultipleChoice, "Tema", claim);
        var b = PromptBuilder.Build(QuestionShape.MultipleChoice, "Tema", claim);
        Assert.Equal(a, b);
    }

    // ─── FillInTheBlank (PR 34a) ──────────────────────────────────────

    [Fact]
    public void Build_FillBlank_IncludesShapeSpecificMarkers()
    {
        var prompt = PromptBuilder.Build(QuestionShape.FillInTheBlank, "Tema", SampleClaim());

        // Identificador único do prompt fill-blank
        Assert.Contains("preencher a lacuna", prompt);
        Assert.Contains("TERMO-CHAVE", prompt);
        Assert.Contains("LACUNA NO MEIO", prompt);
        Assert.Contains("DISTRATORES SAME-TYPE", prompt);
        // Few-shot
        Assert.Contains("FILL-BLANK BOM", prompt);
        // Schema é o mesmo formato JSON
        Assert.Contains("\"options\"", prompt);
        Assert.Contains("\"correctIndex\"", prompt);
    }

    [Fact]
    public void Build_FillBlank_IsDeterministic()
    {
        var claim = SampleClaim();
        var a = PromptBuilder.Build(QuestionShape.FillInTheBlank, "Tema", claim);
        var b = PromptBuilder.Build(QuestionShape.FillInTheBlank, "Tema", claim);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Build_FillBlank_DiffersFromMcq()
    {
        var claim = SampleClaim();
        var mcq    = PromptBuilder.Build(QuestionShape.MultipleChoice, "Tema", claim);
        var blank  = PromptBuilder.Build(QuestionShape.FillInTheBlank, "Tema", claim);
        Assert.NotEqual(mcq, blank);
    }

    // ─── TrueFalseGrounded (reservado p/ 34a-bis) ─────────────────────

    [Fact]
    public void Build_TrueFalseGrounded_FallsBackToMcq()
    {
        // Decisão arquitetural: shape não-implementado ainda devolve MCQ
        // (yield conhecido) em vez de quebrar. Quando 34a-bis chegar,
        // troca o branch do switch e esse teste passa a checar marker do TF.
        var claim = SampleClaim();
        var tf  = PromptBuilder.Build(QuestionShape.TrueFalseGrounded, "Tema", claim);
        var mcq = PromptBuilder.Build(QuestionShape.MultipleChoice,    "Tema", claim);
        Assert.Equal(mcq, tf);
    }
}
