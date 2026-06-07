using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unravel.Application.Forge.Ports;
using Unravel.Infrastructure;

namespace Unravel.Tests.Forge;

/// <summary>
/// PR 34e — garante que strategies template-based ficaram fora do DI
/// por default (Forge:UseLegacyStrategies=false) e que a flag de escape
/// reativa quando explicitamente ligada.
///
/// <para>Pipeline LlmGrounded (PR 31+) cobre 100% da geração em
/// produção desde PR 51. Strategies template ficam só pra biseção/debug.
/// Esse test trava a regressão — se alguém re-registrar uma strategy
/// no DI sem a flag, esse teste quebra.</para>
/// </summary>
public class LegacyStrategiesDIFlagTests
{
    private static IServiceProvider BuildProvider(bool useLegacy)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Forge:UseLegacyStrategies"] = useLegacy ? "true" : "false",
                // Desliga LLM pra não exigir env/secret nos testes
                ["Llm:Enabled"] = "false",
                // Desliga embedding (modelo ONNX) — não usado nesses tests
                ["Embedding:Enabled"] = "false",
                // Conn string fake; nao chega a abrir conexao (sem chamada DB)
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Port=1;Database=x;Username=x;Password=x",
                ["Jwt:Key"] = "test-key-test-key-test-key-test-key-test-key=",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Default_DoesNotRegisterTemplateStrategies()
    {
        using var sp = (ServiceProvider)BuildProvider(useLegacy: false);
        var strategies = sp.GetServices<IChallengeStrategy>().ToList();

        // Espera apenas LlmChallengeStrategy quando Llm:Enabled=true; com
        // Llm:Enabled=false a lista é vazia. Nesse test setamos Llm:Enabled=false,
        // então não pode ter NADA registrado.
        Assert.Empty(strategies);
    }

    [Fact]
    public void FlagOn_RegistersAll6TemplateStrategies()
    {
        using var sp = (ServiceProvider)BuildProvider(useLegacy: true);
        var strategies = sp.GetServices<IChallengeStrategy>().ToList();

        // 6 template-based: Cloze, Definition, TrueFalse, Ordering, Match, Code
        Assert.Equal(6, strategies.Count);
        // Cobre todos os enum values esperados
        var kinds = strategies.Select(s => s.Kind).OrderBy(k => k).ToList();
        Assert.Contains(Unravel.Domain.Forge.ForgeStrategy.Cloze,      kinds);
        Assert.Contains(Unravel.Domain.Forge.ForgeStrategy.Definition, kinds);
        Assert.Contains(Unravel.Domain.Forge.ForgeStrategy.TrueFalse,  kinds);
        Assert.Contains(Unravel.Domain.Forge.ForgeStrategy.Ordering,   kinds);
        Assert.Contains(Unravel.Domain.Forge.ForgeStrategy.Match,      kinds);
        Assert.Contains(Unravel.Domain.Forge.ForgeStrategy.Code,       kinds);
    }
}
