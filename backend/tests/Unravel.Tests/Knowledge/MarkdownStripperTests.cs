using Unravel.Infrastructure.Knowledge.Chunking;

namespace Unravel.Tests.Knowledge;

public class MarkdownStripperTests
{
    [Fact]
    public void Strip_RemovesFencedCodeBlocks()
    {
        var md = "Antes do código.\n\n```ts\nconst x = 1;\nconsole.log(x);\n```\n\nDepois.";
        var result = MarkdownStripper.Strip(md);

        Assert.Contains("Antes do código", result);
        Assert.Contains("Depois", result);
        Assert.DoesNotContain("const x", result);
        Assert.DoesNotContain("console.log", result);
        Assert.DoesNotContain("```", result);
    }

    [Fact]
    public void Strip_RemovesInlineCode()
    {
        var md = "Use `inject()` para resolver `services` automaticamente.";
        var result = MarkdownStripper.Strip(md);

        Assert.DoesNotContain("`", result);
        // Texto puro deve sobrar (com espaços onde estava o backtick)
        Assert.Contains("Use", result);
        Assert.Contains("para resolver", result);
    }

    [Fact]
    public void Strip_StripsHeadingMarkers()
    {
        var md = "## Título\n\nParágrafo.\n\n### Subtítulo\n\nOutro parágrafo.";
        var result = MarkdownStripper.Strip(md);

        Assert.DoesNotContain("##", result);
        Assert.Contains("Título", result);
        Assert.Contains("Subtítulo", result);
    }

    [Fact]
    public void Strip_PreservesParagraphBreaks()
    {
        var md = "Primeiro parágrafo.\n\nSegundo parágrafo.\n\n\n\nTerceiro com gap maior.";
        var result = MarkdownStripper.Strip(md);

        // Markdig normaliza pra \n\n, MultiNewline regex força ≤ 2
        Assert.DoesNotContain("\n\n\n", result);
        Assert.Contains("Primeiro", result);
        Assert.Contains("Terceiro", result);
    }

    [Fact]
    public void Strip_EmptyOrNull_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, MarkdownStripper.Strip(""));
        Assert.Equal(string.Empty, MarkdownStripper.Strip("   \n\t  "));
        Assert.Equal(string.Empty, MarkdownStripper.Strip(null!));
    }

    [Fact]
    public void Strip_RemovesLinkSyntaxKeepingText()
    {
        var md = "Consulte a [documentação oficial](https://angular.io) para detalhes.";
        var result = MarkdownStripper.Strip(md);

        Assert.Contains("documentação oficial", result);
        Assert.DoesNotContain("https://", result);
        Assert.DoesNotContain("[", result);
        Assert.DoesNotContain("]", result);
    }
}
