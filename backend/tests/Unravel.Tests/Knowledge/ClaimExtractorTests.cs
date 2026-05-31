using Unravel.Application.Knowledge.Ports;
using Unravel.Infrastructure.Knowledge;

namespace Unravel.Tests.Knowledge;

/// <summary>
/// Cobre o <see cref="ClaimExtractor"/> com casos sintéticos focados
/// nas heurísticas de filtragem + um teste de integração contra o MD
/// real de "Componentes Angular" (PR 28) pra garantir que produzimos
/// claims utilizáveis em conteúdo realista, não só fixtures perfeitas.
/// </summary>
public class ClaimExtractorTests
{
    private readonly IClaimExtractor _sut = new ClaimExtractor();

    // ── Filtros ─────────────────────────────────────────────────────

    [Fact]
    public void Extract_EmptyOrNull_ReturnsEmpty()
    {
        Assert.Empty(_sut.Extract(""));
        Assert.Empty(_sut.Extract("   "));
        Assert.Empty(_sut.Extract(null!));
    }

    [Fact]
    public void Extract_TooShortSentence_Discarded()
    {
        var md = "## Conceito\n\nUse signals. Importante saber.";
        var claims = _sut.Extract(md);
        Assert.Empty(claims); // "Use signals." é 2 palavras + imperativo
    }

    [Fact]
    public void Extract_HedgingSentence_Discarded()
    {
        var md = "## Conceito\n\nO componente talvez seja útil para organizar a tela em pequenas partes reusáveis.";
        var claims = _sut.Extract(md);
        Assert.Empty(claims);
    }

    [Fact]
    public void Extract_QuestionSentence_Discarded()
    {
        var md = "## Conceito\n\nO que é um componente Angular e como ele funciona dentro do framework?";
        var claims = _sut.Extract(md);
        Assert.Empty(claims);
    }

    [Fact]
    public void Extract_NoCoreVerb_Discarded()
    {
        // "Tela" não tem verbo cópula nem técnico — não passa
        var md = "## Conceito\n\nUma tela do navegador organizada em vários pequenos blocos de interface reusáveis.";
        var claims = _sut.Extract(md);
        Assert.Empty(claims);
    }

    [Fact]
    public void Extract_DefinitionalSentence_Passes()
    {
        var md = "## Componentes\n\nO componente é a unidade básica de construção de qualquer aplicação Angular moderna.";
        var claims = _sut.Extract(md);

        Assert.Single(claims);
        Assert.Contains("componente é a unidade", claims[0].ClaimText);
        Assert.True(claims[0].Score >= 0.5,
            $"Definicional simples deveria ter score ≥0.5, teve {claims[0].Score}");
    }

    [Fact]
    public void Extract_ChunkIndexAndTextPreserved()
    {
        var md = """
        ## Primeira

        O componente é a unidade básica de construção de toda aplicação Angular.

        ## Segunda

        A diretiva marca um elemento DOM como portador de comportamento adicional configurável.
        """;
        var claims = _sut.Extract(md);

        Assert.Equal(2, claims.Count);
        // Chunks são 0 e 1, e cada claim referencia seu próprio PlainText
        Assert.Contains(claims, c => c.ChunkIndex == 0 && c.ClaimText.Contains("componente"));
        Assert.Contains(claims, c => c.ChunkIndex == 1 && c.ClaimText.Contains("diretiva"));
    }

    [Fact]
    public void Extract_RespectsMaxClaimsPerChunk()
    {
        var md = "## Chunk único\n\n" + string.Join(" ",
            Enumerable.Range(1, 10).Select(i =>
                $"O componente número {i} é uma unidade de construção que serve para organizar interfaces complexas em partes menores."));

        var claims = _sut.Extract(md, maxClaimsPerChunk: 3);

        Assert.Equal(3, claims.Count);
        Assert.All(claims, c => Assert.Equal(0, c.ChunkIndex));
    }

    [Fact]
    public void Extract_IsDeterministic_SameInputSameOutput()
    {
        var md = """
        ## A

        O componente é a unidade básica de Angular para construir interfaces reusáveis.
        O decorator marca a classe como componente Angular.

        ## B

        A diretiva permite adicionar comportamento ao DOM.
        """;
        var first  = _sut.Extract(md);
        var second = _sut.Extract(md);

        Assert.Equal(first.Count, second.Count);
        for (var i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].ChunkIndex, second[i].ChunkIndex);
            Assert.Equal(first[i].ClaimText,  second[i].ClaimText);
            Assert.Equal(first[i].Score,      second[i].Score);
        }
    }

    [Fact]
    public void Extract_ClaimsOrderedByScoreDescThenPositionAsc()
    {
        var md = """
        ## Conceito

        O componente é a unidade básica de Angular para construir interfaces reusáveis modernas.
        Uma forma comum de organização aceita múltiplas opções de implementação técnica.
        O decorator marca a classe como componente Angular permitindo configurações declarativas.
        """;
        var claims = _sut.Extract(md, maxClaimsPerChunk: 5);

        Assert.True(claims.Count >= 2);
        // Score monotônico decrescente
        for (var i = 1; i < claims.Count; i++)
            Assert.True(claims[i - 1].Score >= claims[i].Score,
                $"Claims devem vir ordenados por score desc; viola: {claims[i-1].Score} < {claims[i].Score}");
    }

    // ── Integração: MD real do PR 28 ────────────────────────────────

    [Fact]
    public void Extract_RealAngularMarkdown_ProducesUsableClaims()
    {
        var mdPath = FindAngularMd("01-componentes.md");
        if (mdPath is null)
        {
            // Soft-skip: o teste só roda em ambientes que têm os MDs
            // (dev local + CI clonado). Não falha em sandbox.
            return;
        }

        var body  = File.ReadAllText(mdPath);
        // Remove frontmatter (acima já testado no KnowledgeImporter)
        var bodyOnly = body[(body.IndexOf("---\n", 4, StringComparison.Ordinal) + 5)..];

        var claims = _sut.Extract(bodyOnly);

        // Garante volume razoável — o MD tem ~900 palavras e várias H2;
        // esperamos pelo menos 8 claims aproveitáveis ao todo.
        Assert.True(claims.Count >= 8,
            $"Esperava ≥8 claims do MD real, obteve {claims.Count}");

        // Garante que cada claim referencia um chunk válido com texto
        Assert.All(claims, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.ChunkText),
                $"Claim '{c.ClaimText}' tem ChunkText vazio");
            Assert.False(string.IsNullOrWhiteSpace(c.ClaimText));
            Assert.InRange(c.Score, 0, 1);
        });

        // Não deve ter claims "lixo" claros:
        Assert.DoesNotContain(claims, c =>
            c.ClaimText.Contains("```") || c.ClaimText.StartsWith("Use ", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Procura o MD subindo até achar a pasta knowledge — flexível
    /// pro diretório de teste (bin/Debug/net8.0) chegar em backend/knowledge.</summary>
    private static string? FindAngularMd(string filename)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 10 && dir != null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "knowledge", "angular-fundamentos", filename);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
