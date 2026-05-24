using Unravel.Infrastructure.Knowledge;

#pragma warning disable IDE0005 // TextNormalizer é internal, visível via InternalsVisibleTo

namespace Unravel.Tests.Knowledge;

public class RakeKeywordExtractorTests
{
    private readonly RakeKeywordExtractor _sut = new();

    [Fact]
    public void Extract_EmptyText_ReturnsEmpty()
    {
        Assert.Empty(_sut.Extract(""));
        Assert.Empty(_sut.Extract("   \n\t  "));
        Assert.Empty(_sut.Extract(null!));
    }

    [Fact]
    public void Extract_TechnicalText_PrefersMultiwordTerms()
    {
        var text = "Redes neurais artificiais são modelos computacionais inspirados no cérebro. " +
                   "Redes neurais profundas são compostas por múltiplas camadas. " +
                   "O treinamento de redes neurais usa retropropagação do erro.";

        var keywords = _sut.Extract(text, topN: 5);

        Assert.NotEmpty(keywords);
        // O termo composto "redes neurais" deve estar entre os top resultados.
        Assert.Contains(keywords, k => k.Term.Contains("redes neurais"));
    }

    [Fact]
    public void Extract_IsDeterministic_SameInputSameOutput()
    {
        var text = "Entity Framework é um ORM da Microsoft. " +
                   "ORM permite mapear classes em tabelas relacionais.";

        var a = _sut.Extract(text);
        var b = _sut.Extract(text);

        Assert.Equal(a.Count, b.Count);
        for (var i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].Term, b[i].Term);
            Assert.Equal(a[i].Score, b[i].Score, precision: 10);
        }
    }

    [Fact]
    public void Extract_FiltersStopwordsAndShortTokens()
    {
        var text = "O que é um banco de dados? Um banco é uma coleção de dados.";
        var keywords = _sut.Extract(text);

        // "o", "que", "é", "um", "de", "é", "uma" são stopwords/curtos demais.
        Assert.DoesNotContain(keywords, k => k.Term is "o" or "que" or "um" or "de" or "é");
        Assert.Contains(keywords, k => k.Term.Contains("banco"));
    }

    [Fact]
    public void Extract_NormalizesDiacritics_FusesCaseAndAccentVariants()
    {
        // RAKE agrupa por *frase-candidata*; o stem só normaliza a chave usada
        // para fundir. A garantia que queremos: variações puramente
        // case/diacrítico da mesma frase NÃO aparecem como entradas separadas.
        var text = "PROGRAMAÇÃO orientada a objetos. " +
                   "Programação Orientada a objetos. " +
                   "programacao orientada a objetos.";

        var keywords = _sut.Extract(text);
        var canonicalKeys = keywords.Select(k =>
            string.Join(' ', k.Term.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                   .Select(TextNormalizer.CanonicalKey))).ToList();

        Assert.Equal(canonicalKeys.Distinct().Count(), canonicalKeys.Count);
    }
}
