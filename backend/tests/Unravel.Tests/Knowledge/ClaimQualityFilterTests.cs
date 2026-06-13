using Unravel.Infrastructure.Knowledge;

namespace Unravel.Tests.Knowledge;

/// <summary>
/// PR 34j — cobre o filtro IsQuestionable: descarta claims não-perguntáveis
/// (meta-discurso/conclusão + opinião avaliativa) antes de gastar geração.
/// Conservador: claims técnicos factuais devem PASSAR.
/// </summary>
public class ClaimQualityFilterTests
{
    private static string[] W(string s) => s.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    // ── Devem ser REJEITADOS (não-perguntáveis) ──────────────────────

    [Theory]
    [InlineData("JIT é uma ferramenta poderosa, mas usar bem exige medir o código")]
    [InlineData("A regra prática é simples: meça antes de memoizar")]
    [InlineData("Por fim, abusar de useEffect para sincronizar estado é antipadrão")]
    [InlineData("Em resumo, o Fiber permite trabalho interrompível")]
    [InlineData("Vale a pena usar React.memo em componentes pesados")]
    [InlineData("O mais importante é medir antes de otimizar")]
    [InlineData("A chave é entender como o React renderiza")]
    public void Rejects_NonQuestionableClaims(string sentence)
    {
        Assert.False(ClaimExtractor.IsQuestionable(sentence, W(sentence)));
    }

    // ── Devem PASSAR (factuais perguntáveis) ─────────────────────────

    [Theory]
    [InlineData("O decorator @Component marca a classe como componente Angular")]
    [InlineData("O hook useTransition marca atualizações como não-urgentes")]
    [InlineData("O Fiber é o motor de reconciliação do React desde a versão 16")]
    [InlineData("Os React Server Components rodam exclusivamente no servidor")]
    [InlineData("O Composer instala dependências declaradas no composer.json")]
    [InlineData("A render phase constrói a árvore de trabalho e pode ser interrompida")]
    public void Accepts_FactualClaims(string sentence)
    {
        Assert.True(ClaimExtractor.IsQuestionable(sentence, W(sentence)));
    }
}
