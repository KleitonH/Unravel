using Unravel.Domain.Entities;
using Unravel.Infrastructure.Knowledge;

namespace Unravel.Tests.Knowledge;

public class DifficultyScorerTests
{
    private readonly DifficultyScorer _sut = new();

    [Fact]
    public void Score_IsBoundedInZeroOne()
    {
        var score = _sut.Score("teste", "corpo curto", DifficultyLevel.Beginner);
        Assert.InRange(score, 0, 1);
    }

    [Fact]
    public void Score_AdvancedDeclared_IsHigherThanBeginner_ForSameText()
    {
        var title = "Tópico genérico";
        var body  = "Este é um conteúdo de exemplo com texto simples.";

        var beginner = _sut.Score(title, body, DifficultyLevel.Beginner);
        var advanced = _sut.Score(title, body, DifficultyLevel.Advanced);

        Assert.True(advanced > beginner, $"advanced={advanced} deveria ser > beginner={beginner}");
    }

    [Fact]
    public void Score_TechHeavyContent_ScoresHigherThanProse()
    {
        var prose = _sut.Score("Sobre o mar",
            "O mar é grande e azul. Tem muitas ondas. Surfistas gostam de praia.",
            DifficultyLevel.Beginner);

        var tech = _sut.Score("Hexagonal Architecture",
            """
            A arquitetura hexagonal (Ports and Adapters) isola o `DomainCore`
            de detalhes de infraestrutura via interfaces. O `ApplicationLayer`
            depende somente de `IRepository<T>` e `IUnitOfWork`. Exemplos em C#:
            ```csharp
            public interface IUserRepository { Task<User?> FindByEmailAsync(Email e); }
            ```
            """,
            DifficultyLevel.Beginner);

        Assert.True(tech > prose, $"tech={tech} deveria ser > prose={prose} (mesmo nível declarado)");
    }

    [Fact]
    public void Score_EmptyBody_ReturnsDeclaredPrior()
    {
        var s = _sut.Score("título só", "", DifficultyLevel.Intermediate);
        Assert.Equal(0.50, s, precision: 2);
    }

    [Fact]
    public void Score_IsDeterministic()
    {
        var title = "Joins SQL";
        var body  = "INNER JOIN combina linhas de duas tabelas onde a condição casa.";
        var a = _sut.Score(title, body, DifficultyLevel.Intermediate);
        var b = _sut.Score(title, body, DifficultyLevel.Intermediate);
        Assert.Equal(a, b);
    }
}
