using Unravel.Infrastructure.Knowledge.Chunking;

namespace Unravel.Tests.Knowledge;

public class ChunkSegmenterTests
{
    private readonly ChunkSegmenter _sut = new();

    [Fact]
    public void Segment_EmptyOrNull_ReturnsEmpty()
    {
        Assert.Empty(_sut.Segment(""));
        Assert.Empty(_sut.Segment("   \n  "));
        Assert.Empty(_sut.Segment(null!));
    }

    [Fact]
    public void Segment_SingleH2Section_ReturnsOneChunk()
    {
        var md = "## Apenas uma seção\n\nConteúdo dela aqui.";
        var chunks = _sut.Segment(md);

        Assert.Single(chunks);
        Assert.Equal(0, chunks[0].Index);
        Assert.Equal("Apenas uma seção", chunks[0].HeadingPath);
        Assert.Contains("Conteúdo dela aqui", chunks[0].PlainText);
    }

    [Fact]
    public void Segment_MultipleH2_ProducesOneChunkPerSection()
    {
        var md = """
        ## Primeira

        Conteúdo da primeira seção.

        ## Segunda

        Conteúdo da segunda.

        ## Terceira

        E mais uma.
        """;
        var chunks = _sut.Segment(md);

        Assert.Equal(3, chunks.Count);
        Assert.Equal(new[] { "Primeira", "Segunda", "Terceira" },
                     chunks.Select(c => c.HeadingPath).ToArray());
        // Index é sequencial
        Assert.Equal(new[] { 0, 1, 2 }, chunks.Select(c => c.Index).ToArray());
    }

    [Fact]
    public void Segment_PreservesRawMarkdownWithHeading()
    {
        var md = "## Componentes\n\n```ts\nclass X {}\n```\n\nTexto após código.";
        var chunks = _sut.Segment(md);

        Assert.Single(chunks);
        // RawMarkdown deve manter os fences pro LLM ter contexto
        Assert.Contains("```", chunks[0].RawMarkdown);
        Assert.Contains("class X", chunks[0].RawMarkdown);
        // PlainText NÃO deve ter código
        Assert.DoesNotContain("class X", chunks[0].PlainText);
        Assert.Contains("Texto após código", chunks[0].PlainText);
    }

    [Fact]
    public void Segment_H3UnderH2_BecomesNestedHeadingPath()
    {
        var md = """
        ## Forms

        Forms são úteis.

        ### Validação

        Validação é importante.
        """;
        var chunks = _sut.Segment(md);

        Assert.Equal(2, chunks.Count);
        Assert.Equal("Forms", chunks[0].HeadingPath);
        Assert.Equal("Forms > Validação", chunks[1].HeadingPath);
    }

    [Fact]
    public void Segment_NoHeadings_SingleChunkWithEmptyHeading()
    {
        var md = "Sem heading nenhum.\n\nDois parágrafos só.";
        var chunks = _sut.Segment(md);

        Assert.Single(chunks);
        Assert.Equal(string.Empty, chunks[0].HeadingPath);
        Assert.Contains("Sem heading", chunks[0].PlainText);
    }

    [Fact]
    public void Segment_LargeSection_SubdividesByParagraph()
    {
        // Gera ~6000 chars (> TargetCharsMax=3000) em parágrafos.
        var paragraphs = string.Join("\n\n",
            Enumerable.Range(1, 20).Select(i =>
                $"Parágrafo {i}. " + new string('x', 250)));
        var md = "## Seção grande\n\n" + paragraphs;

        var chunks = _sut.Segment(md);

        Assert.True(chunks.Count >= 2, "Seção grande deve quebrar em ≥2 chunks");
        // Index sequencial
        for (var i = 0; i < chunks.Count; i++) Assert.Equal(i, chunks[i].Index);
        // Todos têm o mesmo heading path
        Assert.All(chunks, c => Assert.Equal("Seção grande", c.HeadingPath));
    }

    [Fact]
    public void Segment_IntroBeforeFirstH2_IsAlsoChunk()
    {
        var md = "Texto solto antes do primeiro heading.\n\n## Primeiro\n\nConteúdo.";
        var chunks = _sut.Segment(md);

        Assert.Equal(2, chunks.Count);
        Assert.Equal(string.Empty, chunks[0].HeadingPath);
        Assert.Equal("Primeiro", chunks[1].HeadingPath);
    }
}
