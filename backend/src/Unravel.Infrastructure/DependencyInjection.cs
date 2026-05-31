using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Unravel.Application.Journey.Ports;
using Unravel.Application.Ports;
using Unravel.Application.Services;
using Unravel.Application.UseCases;
using Unravel.Domain.Ports;
using Unravel.Application.Forge.Ports;
using Unravel.Application.Forge.UseCases;
using Unravel.Application.Gamification.Ports;
using Unravel.Application.Journey;
using Unravel.Application.Journey.Onboarding;
using Unravel.Application.Journey.UseCases;
using Unravel.Application.Knowledge.Ports;
using Unravel.Infrastructure.Forge;
using Unravel.Infrastructure.Forge.Llm;
using Unravel.Infrastructure.Forge.Strategies;
using Unravel.Infrastructure.Gamification;
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
        // PR 18 — opcionalmente, embedder semântico substitui DistractorPicker
        // lexical. Lê config "Embedding:Enabled" (default false). Quando ligado,
        // exige Embedding:ModelPath e Embedding:TokenizerPath apontando para
        // arquivos baixados via scripts/download-minilm.sh.
        var embeddingEnabled = configuration.GetValue("Embedding:Enabled", false);
        if (embeddingEnabled)
        {
            var modelPath     = configuration["Embedding:ModelPath"]
                                ?? throw new InvalidOperationException("Embedding:ModelPath obrigatório quando Embedding:Enabled=true.");
            var tokenizerPath = configuration["Embedding:TokenizerPath"]
                                ?? throw new InvalidOperationException("Embedding:TokenizerPath obrigatório quando Embedding:Enabled=true.");
            services.AddSingleton<IEmbedder>(sp => new MiniLmEmbedder(modelPath, tokenizerPath));
            services.AddSingleton<IDistractorPicker, SemanticDistractorPicker>();
        }
        else
        {
            services.AddSingleton<IDistractorPicker, DistractorPicker>();
        }

        services.AddSingleton<IChallengeStrategy, ClozeStrategy>();
        services.AddSingleton<IChallengeStrategy, DefinitionStrategy>();
        services.AddSingleton<IChallengeStrategy, TrueFalseStrategy>();
        // PR 5 — estratégias avançadas. Mesma interface, registradas em
        // paralelo; o ChallengeForge resolve como IEnumerable<IChallengeStrategy>
        // e roteia automaticamente.
        services.AddSingleton<IChallengeStrategy, OrderingStrategy>();
        services.AddSingleton<IChallengeStrategy, MatchStrategy>();
        services.AddSingleton<IChallengeStrategy, CodeStrategy>();

        // PR 20 + PR 30 — LLM strategy opcional. Lê Llm:Enabled (default
        // false). Quando ligada, escolhe entre dois providers via
        // Llm:Provider:
        //   • "Ollama"     → HTTP pra daemon local com GPU offload (dev/lab)
        //   • "LLamaSharp" → modelo .gguf embarcado no processo (VPS CPU)
        //                    [default, retrocompat com PR 20]
        // Ambos implementam ILlmInference; LlmChallengeStrategy não sabe
        // qual está em uso. Health check no boot do hosted service.
        var llmEnabled = configuration.GetValue("Llm:Enabled", false);
        if (llmEnabled)
        {
            var provider = configuration.GetValue("Llm:Provider", "LLamaSharp");
            var maxTok   = configuration.GetValue("Llm:MaxTokens", 400);
            var temp     = configuration.GetValue("Llm:Temperature", 0.7f);
            var ctxSize  = configuration.GetValue("Llm:ContextSize", 2048);

            if (provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
            {
                // Ollama: HTTP client + factory de OllamaInference. HttpClient
                // gerenciado pelo IHttpClientFactory (lifecycle + socket reuse).
                var baseUrl = configuration["Llm:Ollama:BaseUrl"] ?? "http://127.0.0.1:11434";
                var model   = configuration["Llm:Ollama:Model"]
                              ?? throw new InvalidOperationException("Llm:Ollama:Model obrigatório (ex: 'qwen2.5:7b-instruct-q4_K_M').");
                var forceJson = configuration.GetValue("Llm:Ollama:ForceJson", true);

                services.AddHttpClient<OllamaInference>(c =>
                {
                    c.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
                });

                services.AddSingleton<ILlmInference>(sp =>
                {
                    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(OllamaInference));
                    http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
                    return new OllamaInference(http, model, temp, maxTok, ctxSize, forceJson,
                        sp.GetRequiredService<ILogger<OllamaInference>>());
                });
            }
            else
            {
                var modelPath = configuration["Llm:ModelPath"]
                                ?? throw new InvalidOperationException("Llm:ModelPath obrigatório quando Llm:Provider=LLamaSharp.");
                var gpuLayers = configuration.GetValue("Llm:GpuLayerCount", 0);

                services.AddSingleton<ILlmInference>(sp =>
                    new LLamaSharpInference(modelPath, gpuLayers, ctxSize, maxTok, temp,
                        sp.GetRequiredService<ILogger<LLamaSharpInference>>()));
            }

            services.AddSingleton<IChallengeStrategy, LlmChallengeStrategy>();
            services.AddScoped<ILlmGenerationOrchestrator, LlmGenerationOrchestrator>();
        }

        services.AddSingleton<IChallengeForge, ChallengeForge>();
        services.AddScoped<IGeneratedChallengeRepository, GeneratedChallengeRepository>();
        services.AddScoped<IForgeReadModel, ForgeReadModel>();
        services.AddScoped<GetChallengePoolUseCase>();
        // PR 13 — submit do quiz: valida no servidor, propaga p/ Mastery.
        services.AddScoped<SubmitPoolChallengeUseCase>();

        // PR 15 — gamificação: XP/Coins/Stars/Vidas + streak no submit do quiz.
        services.AddScoped<IUserGamificationGateway, UserGamificationGateway>();

        // PR 17 — auto-desativador de perguntas com CorrectRate extremo.
        // O hosted service correspondente é registrado em Program.cs.
        services.AddScoped<IGeneratedChallengeMaintenance, GeneratedChallengeMaintenance>();

        // Onboarding (PR 6) — cold-start com nivelamento.
        // LevelingTestBuilder é stateless → singleton.
        // Read model e enroller dependem de DbContext → scoped.
        services.AddSingleton<LevelingTestBuilder>();
        services.AddScoped<IOnboardingReadModel, OnboardingReadModel>();
        services.AddScoped<IUserTrailEnroller, UserTrailEnroller>();
        services.AddScoped<StartOnboardingUseCase>();
        services.AddScoped<SubmitOnboardingUseCase>();

        // Cron diário (PR 7) — orquestrador é scoped (depende de repos),
        // event bus é singleton (sem estado mutável), read model + repo
        // são scoped (DbContext). O hosted service é registrado pelo
        // chamador via AddHostedService (Program.cs).
        services.AddSingleton<IJourneyEventBus, LoggingJourneyEventBus>();
        services.AddScoped<IDailyReplanReadModel, DailyReplanReadModel>();
        services.AddScoped<IJourneySnapshotRepository, JourneySnapshotRepository>();
        services.AddScoped<DailyReplanService>();

        // PR 28 — KnowledgeImporter: lê backend/knowledge/* e popula
        // Trail+Content via upsert por Slug. Scoped (usa DbContext).
        // Execução automática no startup é decidida em Program.cs.
        services.AddScoped<KnowledgeImporter>();

        // PR 29 — ClaimExtractor: segmenta Content.Body em chunks +
        // extrai atomic claims testáveis. Stateless após construção;
        // singleton seguro. Alimenta o gerador LLM grounded (PR 31).
        services.AddSingleton<IClaimExtractor, ClaimExtractor>();

        return services;
    }
}
