using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unravel.Application.Journey.Ports;
using Unravel.Application.Ports;
using Unravel.Application.Services;
using Unravel.Application.UseCases;
using Unravel.Domain.Ports;
using Unravel.Application.Forge.Ports;
using Unravel.Application.Forge.UseCases;
using Unravel.Application.Journey;
using Unravel.Application.Journey.UseCases;
using Unravel.Infrastructure.Forge;
using Unravel.Infrastructure.Forge.Strategies;
using Unravel.Infrastructure.Journey;
using Unravel.Infrastructure.Knowledge;
using Unravel.Infrastructure.Persistence;
using Unravel.Infrastructure.Repositories;
using Unravel.Infrastructure.Services;

namespace Unravel.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
                   .UseSnakeCaseNamingConvention());

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();

        services.AddScoped<CreateUserUseCase>();
        services.AddScoped<AuthenticateUserUseCase>();
        services.AddScoped<GetUserUseCase>();

        services.AddScoped<ITrailService,     TrailService>();
        services.AddScoped<IContentService,  ContentService>();
        services.AddScoped<IChallengeService, ChallengeService>();

        // Knowledge (PR 1) — extração NLP, builder e cache do KnowledgeGraph.
        // Extractor, scorer e builder são stateless e thread-safe → singleton.
        // O cache TEM que ser singleton (é o ponto inteiro de existir).
        services.AddSingleton<IKeywordExtractor, RakeKeywordExtractor>();
        services.AddSingleton<DifficultyScorer>();
        services.AddSingleton<IKnowledgeGraphBuilder, GraphBuilder>();
        services.AddSingleton<IKnowledgeGraphCache, MemoryKnowledgeGraphCache>();

        // Mastery (PR 2) — repositório e topic resolver.
        services.AddScoped<IMasteryRepository, MasteryRepository>();
        services.AddSingleton<ITopicResolver, KeywordTopicResolver>();

        // Journey planner (PR 3) — planner singleton (puro/stateless),
        // read model e use case escopados (dependem de DbContext).
        services.AddSingleton<IJourneyPlanner, JourneyPlanner>();
        services.AddScoped<IJourneyReadModel, JourneyReadModel>();
        services.AddScoped<GetDailyJourneyUseCase>();

        // Forge (PR 4) — gerador de perguntas. Estratégias são stateless,
        // registradas múltiplas vezes na mesma interface (DI resolve como
        // IEnumerable<IChallengeStrategy>). Para plugar uma LlmChallengeStrategy
        // no futuro, basta um services.AddSingleton<IChallengeStrategy, LlmStrategy>().
        services.AddSingleton<IDistractorPicker, DistractorPicker>();
        services.AddSingleton<IChallengeStrategy, ClozeStrategy>();
        services.AddSingleton<IChallengeStrategy, DefinitionStrategy>();
        services.AddSingleton<IChallengeStrategy, TrueFalseStrategy>();
        services.AddSingleton<IChallengeForge, ChallengeForge>();
        services.AddScoped<IGeneratedChallengeRepository, GeneratedChallengeRepository>();
        services.AddScoped<IForgeReadModel, ForgeReadModel>();
        services.AddScoped<GetChallengePoolUseCase>();

        return services;
    }
}
