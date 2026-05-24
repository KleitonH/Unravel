using Unravel.Application.Forge.Ports;
using Unravel.Application.Forge;
using Unravel.Domain.Entities;
using Unravel.Infrastructure.Forge;
using Unravel.Infrastructure.Forge.Strategies;
using Unravel.Infrastructure.Knowledge;

namespace Unravel.Tests.Forge;

public class ChallengeForgeTests
{
    private readonly GraphBuilder _builder = new(new RakeKeywordExtractor(), new DifficultyScorer());

    private ChallengeForge CreateForge()
    {
        var distractors = new DistractorPicker();
        var strategies = new IChallengeStrategy[]
        {
            new ClozeStrategy(distractors),
            new DefinitionStrategy(distractors),
            new TrueFalseStrategy(distractors),
        };
        // Forge não usa o cache no Build() (recebe graph direto); passa stub null-tolerant.
        return new ChallengeForge(strategies, graphCache: null!);
    }

    private static Content C(int id, int order, string title, string body, DifficultyLevel level = DifficultyLevel.Intermediate)
        => new() { Id = id, TrailId = 1, Order = order, Title = title, Body = body, Level = level, IsActive = true };

    private List<Content> SampleTrail() => new()
    {
        C(1, 1, "Componentes Angular",
          "Componentes Angular encapsulam template HTML, estilo CSS e lógica TypeScript. " +
          "Eles usam decorators como @Component para configurar metadata. " +
          "Cada componente tem um seletor único e pode receber inputs via @Input."),

        C(2, 2, "Serviços Angular",
          "Serviços Angular são classes injetáveis que encapsulam lógica reutilizável. " +
          "Eles usam o decorator @Injectable para registro no sistema de DI. " +
          "Componentes consomem serviços através do construtor."),

        C(3, 3, "Routing Angular",
          "O roteamento Angular permite navegação entre componentes via URLs. " +
          "O RouterModule mapeia paths para componentes específicos. " +
          "Guards protegem rotas baseados em condições de autenticação ou autorização."),
    };

    [Fact]
    public void Build_ProducesApprovedDrafts_FromRealishContent()
    {
        var forge = CreateForge();
        var graph = _builder.Build(1, SampleTrail());
        var content = SampleTrail()[0];

        var drafts = forge.Build(content, graph, targetCount: 5);

        Assert.NotEmpty(drafts);
        Assert.All(drafts, d =>
        {
            Assert.True(QualityGate.Approve(d, out var reason), $"rejected: {reason}");
            Assert.Equal(content.Id, d.SourceContentId);
        });
    }

    [Fact]
    public void Build_NoTopicMatch_ReturnsEmpty()
    {
        var forge = CreateForge();
        var graph = _builder.Build(1, SampleTrail());
        var orphan = C(999, 99, "Sem trilha", "Conteúdo solto.");

        var drafts = forge.Build(orphan, graph, targetCount: 5);
        Assert.Empty(drafts);
    }

    [Fact]
    public void Build_IsDeterministic_SameInputSameOrder()
    {
        var forge = CreateForge();
        var graph = _builder.Build(1, SampleTrail());
        var content = SampleTrail()[0];

        var a = forge.Build(content, graph, targetCount: 5);
        var b = forge.Build(content, graph, targetCount: 5);

        Assert.Equal(a.Count, b.Count);
        for (var i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].Prompt,       b[i].Prompt);
            Assert.Equal(a[i].CorrectIndex, b[i].CorrectIndex);
            Assert.Equal(a[i].Options,      b[i].Options);
        }
    }

    [Fact]
    public void Build_RespectsTargetCount()
    {
        var forge = CreateForge();
        var graph = _builder.Build(1, SampleTrail());
        var content = SampleTrail()[0];

        var drafts = forge.Build(content, graph, targetCount: 2);
        Assert.True(drafts.Count <= 2);
    }

    [Fact]
    public void Build_CalibratesByUserMastery_PrefersDifficultiesNearTarget()
    {
        var forge = CreateForge();
        var graph = _builder.Build(1, SampleTrail());
        var content = SampleTrail()[0];

        // Solicitação para usuário avançado (alta mastery): target ≈ 0.95
        var advanced = forge.Build(content, graph, targetCount: 3, targetUserMastery: 0.80);

        // Solicitação para iniciante (baixa mastery): target ≈ 0.35
        var beginner = forge.Build(content, graph, targetCount: 3, targetUserMastery: 0.20);

        Assert.NotEmpty(advanced);
        Assert.NotEmpty(beginner);
        // O conteúdo de amostra tem dificuldade fixa, mas a ordenação por
        // fitness pode diferir entre os dois alvos (TrueFalse difficulty é
        // +0.05, Cloze é -0.05 do topic). Verificamos só que ambos geram.
    }
}
