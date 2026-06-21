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
using Unravel.Infrastructure.Tokens;
using Unravel.Application.Tokens.Ports;

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

        // PR 63 — loja cosmética (catálogo/compra/equip).
        services.AddScoped<Application.Gamification.Ports.ICosmeticShopService,
                           Gamification.CosmeticShopService>();

        // PR 64 — mecânicas sociais (Amigos/Parcerias).
        services.AddScoped<Application.Social.Ports.IFriendshipService,
                           Social.FriendshipService>();

        // PR 65 — Caixinha de Gatos (clã/grupo).
        services.AddScoped<Application.Social.Ports.ICaixinhaService,
                           Social.CaixinhaService>();

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

            // PR 33b — paths relativos resolvidos contra os dois cwds
            // mais comuns (raiz do repo OU backend/). Encontra o
            // primeiro que existe. Sem isso, `./models/...` quebrava
            // quando rodava de subpasta.
            modelPath     = ResolveExistingPath(modelPath);
            tokenizerPath = ResolveExistingPath(tokenizerPath);

            services.AddSingleton<IEmbedder>(sp => new MiniLmEmbedder(modelPath, tokenizerPath));
            services.AddSingleton<IDistractorPicker, SemanticDistractorPicker>();
        }
        else
        {
            services.AddSingleton<IDistractorPicker, DistractorPicker>();
        }

        // PR 34e — strategies template-based marcadas [Obsolete] em PR 34.
        // O pipeline LlmGrounded (PR 31+) cobre 100% das geracoes em
        // producao desde PR 51 (matou fallback inline) e PR 33h (calibrou
        // validators); essas strategies sobravam so no cron noturno legado
        // do PR 20 e estavam gerando perguntas de qualidade baixa
        // ("O que e O componente?", PR 51 root cause). Default = nao
        // registra; cron noturno opera sobre o pool LlmGrounded existente.
        //
        // Flag de escape: Forge:UseLegacyStrategies=true reativa pra
        // bisecao/debug ou conteudo onde o pipeline LLM esta caro/indisponivel.
        var useLegacyStrategies = configuration.GetValue("Forge:UseLegacyStrategies", false);
        if (useLegacyStrategies)
        {
#pragma warning disable CS0618 // Obsolete intencional aqui — flag de escape
            services.AddSingleton<IChallengeStrategy, ClozeStrategy>();
            services.AddSingleton<IChallengeStrategy, DefinitionStrategy>();
            services.AddSingleton<IChallengeStrategy, TrueFalseStrategy>();
            services.AddSingleton<IChallengeStrategy, OrderingStrategy>();
            services.AddSingleton<IChallengeStrategy, MatchStrategy>();
            services.AddSingleton<IChallengeStrategy, CodeStrategy>();
#pragma warning restore CS0618
        }

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
            else if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            {
                // PR 33g — OpenAI Chat Completions. API key via user-secrets
                // ou env var OPENAI_API_KEY (NUNCA commitar em appsettings).
                var baseUrl = configuration["Llm:OpenAi:BaseUrl"] ?? "https://api.openai.com/";
                var model   = configuration["Llm:OpenAi:Model"] ?? "gpt-4o-mini";
                // Ordem invertida pra OPENAI_API_KEY ganhar de secrets.json
                // (que pode estar travado por sync/OneDrive em alguns devs).
                // Quando moderador seta env var deliberadamente, ela é
                // sinal explícito de "use essa chave AGORA"; secrets fica
                // como fallback de desenvolvimento.
                var apiKey  = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                              ?? configuration["Llm:OpenAi:ApiKey"]
                              ?? throw new InvalidOperationException(
                                  "Llm:OpenAi:ApiKey ausente. Configure via env var " +
                                  "OPENAI_API_KEY (preferida) ou " +
                                  "`dotnet user-secrets set \"Llm:OpenAi:ApiKey\" \"sk-...\"` " +
                                  "(em backend/src/Unravel.API/).");
                var forceJson = configuration.GetValue("Llm:OpenAi:ForceJson", true);

                services.AddHttpClient<OpenAiInference>(c =>
                {
                    c.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
                });

                services.AddSingleton<ILlmInference>(sp =>
                {
                    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(OpenAiInference));
                    http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
                    return new OpenAiInference(http, apiKey, model, temp, maxTok, forceJson,
                        sp.GetRequiredService<ILogger<OpenAiInference>>());
                });

                // PR 34h — modelo de escalonamento (tier superior) pra cauda
                // difícil. Opcional: só registra "ligado" se
                // Llm:OpenAi:EscalationModel estiver setado (ex: "gpt-4o").
                // Reusa a MESMA apiKey/baseUrl — gpt-4o é so outro model id.
                var escalationModel = configuration["Llm:OpenAi:EscalationModel"];
                var escalateAfter   = configuration.GetValue("Llm:OpenAi:EscalateAfterPriorAttempts", 2);
                if (!string.IsNullOrWhiteSpace(escalationModel))
                {
                    services.AddSingleton<IEscalationLlm>(sp =>
                    {
                        var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(OpenAiInference));
                        http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
                        // Escalado usa temperatura levemente menor (mais
                        // determinístico/cuidadoso) e mais tokens (modelo
                        // melhor produz respostas mais completas).
                        var escalated = new OpenAiInference(http, apiKey, escalationModel,
                            Math.Max(0.1f, temp - 0.2f), maxTok + 150, forceJson,
                            sp.GetRequiredService<ILogger<OpenAiInference>>());
                        return new Unravel.Infrastructure.Forge.Llm.EscalationLlm(
                            escalated, escalationModel, escalateAfter);
                    });
                }
                else
                {
                    services.AddSingleton<IEscalationLlm>(
                        Unravel.Infrastructure.Forge.Llm.EscalationLlm.Disabled);
                }
            }
            else // LLamaSharp (default — retrocompat com PR 20)
            {
                var modelPath = configuration["Llm:ModelPath"]
                                ?? throw new InvalidOperationException("Llm:ModelPath obrigatório quando Llm:Provider=LLamaSharp.");
                var gpuLayers = configuration.GetValue("Llm:GpuLayerCount", 0);

                services.AddSingleton<ILlmInference>(sp =>
                    new LLamaSharpInference(modelPath, gpuLayers, ctxSize, maxTok, temp,
                        sp.GetRequiredService<ILogger<LLamaSharpInference>>()));
            }

            // PR 32 — LlmChallengeStrategy NÃO é mais registrada como
            // IChallengeStrategy pro Forge síncrono. Geração LLM é
            // assíncrona via QuestionForgeWorker → grava em
            // GeneratedChallenge, e o pool puxa de lá naturalmente.
            // Mantemos o ILlmGenerationOrchestrator pro cron noturno
            // legado do PR 20 (será migrado pra usar a queue futuramente).
            services.AddScoped<ILlmGenerationOrchestrator, LlmGenerationOrchestrator>();

            // PR 31 — Grounded question generator: prompt builder + validators
            // em cadeia + parser JSON robusto. Threshold da grounding default
            // 0.55 (calibrado, ajustar com PR 33 gold set).
            services.AddSingleton<Unravel.Infrastructure.Forge.Llm.Grounded.Validators.IQuestionValidator,
                Unravel.Infrastructure.Forge.Llm.Grounded.Validators.SchemaValidator>();
            services.AddSingleton<Unravel.Infrastructure.Forge.Llm.Grounded.Validators.IQuestionValidator,
                Unravel.Infrastructure.Forge.Llm.Grounded.Validators.AnswerLeakageValidator>();
            // Validators que dependem do embedder só são registrados se
            // Embedding:Enabled=true (caso contrário IEmbedder não está no DI).
            if (embeddingEnabled)
            {
                // PR 33e — threshold calibrado em 0.45 (era 0.55). O eval
                // real com 50 items mostrou que ~8 perguntas legítimas
                // ficavam em 0.45-0.55: respostas curtas ("O selector") ou
                // de exclusão ("qual NÃO é"). 0.45 mantém rejeição de
                // alucinações (<0.30) e aceita paráfrases moderadas.
                var groundingThreshold = configuration.GetValue("Llm:Grounding:Threshold", 0.45);
                services.AddSingleton<Unravel.Infrastructure.Forge.Llm.Grounded.Validators.IQuestionValidator>(sp =>
                    new Unravel.Infrastructure.Forge.Llm.Grounded.Validators.AnswerGroundednessValidator(
                        sp.GetRequiredService<IEmbedder>(), groundingThreshold));
                // PR 33e — maxJaccardVsAnswer 0.60 → 0.75. Perguntas de
                // ordenação/lista têm distratores que são reorderings dos
                // mesmos termos (Jaccard naturalmente alto). 0.75 ainda
                // bloqueia distratores que são literal-cópias da resposta.
                //
                // PR 33h — minCosineVsChunk 0.35 → 0.20. Eval gpt-4o-mini
                // mostrou 17/50 DistractorsPoor por distratores levemente
                // off-topic (~0.25-0.35 cosine). Esses são distratores
                // VÁLIDOS pedagógicamente — testam se aluno distingue
                // conceito principal vs conceitos relacionados de outras
                // áreas. 0.20 ainda bloqueia distratores que são banana.
                services.AddSingleton<Unravel.Infrastructure.Forge.Llm.Grounded.Validators.IQuestionValidator>(sp =>
                    new Unravel.Infrastructure.Forge.Llm.Grounded.Validators.DistractorDiversityValidator(
                        sp.GetService<IEmbedder>(), maxJaccardVsAnswer: 0.75, minCosineVsChunk: 0.20));
            }
            else
            {
                services.AddSingleton<Unravel.Infrastructure.Forge.Llm.Grounded.Validators.IQuestionValidator>(sp =>
                    new Unravel.Infrastructure.Forge.Llm.Grounded.Validators.DistractorDiversityValidator(
                        embedder: null, maxJaccardVsAnswer: 0.75, minCosineVsChunk: 0.20));
            }
            // PR 34a — router de shape (heurístico, sem estado). Singleton
            // porque é puro CPU/regex; nenhuma dependência scoped.
            services.AddSingleton<IClaimShapeRouter,
                Unravel.Infrastructure.Forge.Llm.Grounded.ClaimShapeRouter>();

            // PR 34b — validators específicos pra FillBlank. Cada um faz
            // early-return pra shapes != FillInTheBlank, então registrá-los
            // pra todos não impacta o pipeline MCQ.
            services.AddSingleton<Unravel.Infrastructure.Forge.Llm.Grounded.Validators.IQuestionValidator,
                Unravel.Infrastructure.Forge.Llm.Grounded.Validators.BlankPlacementValidator>();
            services.AddSingleton<Unravel.Infrastructure.Forge.Llm.Grounded.Validators.IQuestionValidator,
                Unravel.Infrastructure.Forge.Llm.Grounded.Validators.DistractorGrammarValidator>();

            services.AddSingleton<IGroundedQuestionGenerator,
                Unravel.Infrastructure.Forge.Llm.Grounded.LlmGroundedQuestionGenerator>();
        }

        services.AddSingleton<IChallengeForge, ChallengeForge>();
        services.AddScoped<IGeneratedChallengeRepository, GeneratedChallengeRepository>();
        services.AddScoped<IForgeReadModel, ForgeReadModel>();
        services.AddScoped<GetChallengePoolUseCase>();
        // PR 13 — submit do quiz: valida no servidor, propaga p/ Mastery.
        services.AddScoped<SubmitPoolChallengeUseCase>();

        // PR 42 — CAT-lite stateless: seleção adaptativa da próxima pergunta
        // baseada em ability estimate online (EWMA + zona proximal).
        services.AddScoped<SelectNextAdaptiveChallengeUseCase>();

        // PR 50 — Boss Fight: algoritmo combinatorial (BossFightSelector
        // puro) + use cases start/submit. Reusa mastery/gamificação/seen.
        services.AddScoped<IBossFightRepository, BossFightRepository>();
        services.AddScoped<StartBossFightUseCase>();
        services.AddScoped<SubmitBossFightUseCase>();

        // PR 52 — tokens "centímetros de lã" do moderador. Debita ao
        // disparar forge; credita em eventos de engagement futuros (PR 53).
        services.AddScoped<IModeratorTokenService, ModeratorTokenService>();

        // PR 37 — rastreio de "perguntas geradas já vistas pelo usuário"
        // pra anti-join no Reinforcement Quiz. UPSERT idempotente.
        services.AddScoped<IUserSeenChallengeRepository, UserSeenChallengeRepository>();
        services.AddScoped<BuildReinforcementQuizUseCase>();

        // PR 40 — service de progressão SMW (incrementa ChallengesCompleted,
        // gerencia transições de Status, desbloqueia próximo content).
        // Plugado no SubmitPoolChallengeUseCase via ctor "rico".
        services.AddScoped<ITrailProgressService, TrailProgressService>();

        // PR 15 — gamificação: XP/Coins/Stars/Vidas + streak no submit do quiz.
        services.AddScoped<IUserGamificationGateway, UserGamificationGateway>();

        // PR 17 — auto-desativador de perguntas com CorrectRate extremo.
        // O hosted service correspondente é registrado em Program.cs.
        services.AddScoped<IGeneratedChallengeMaintenance, GeneratedChallengeMaintenance>();

        // PR 60-a — Content fatiado em capítulos H2 (modelo Duolingo).
        services.AddScoped<IContentChaptersService, Unravel.Infrastructure.Forge.ContentChaptersService>();

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

        // PR 32 — Fila persistida de jobs de geração LLM. Scoped (DbContext).
        // O worker BackgroundService é registrado em Program.cs (só quando
        // Llm:Enabled=true).
        services.AddScoped<IQuestionForgeQueue,
            Unravel.Infrastructure.Forge.Queue.QuestionForgeQueueService>();

        // PR 29 — ClaimExtractor: segmenta Content.Body em chunks +
        // extrai atomic claims testáveis. Stateless após construção;
        // singleton seguro. Alimenta o gerador LLM grounded (PR 31).
        services.AddSingleton<IClaimExtractor, ClaimExtractor>();

        return services;
    }

    /// <summary>
    /// PR 33b — resolve um path possivelmente relativo procurando em
    /// ordem: (1) absoluto/cwd, (2) base do executável, (3) subindo até
    /// 4 níveis a partir do AppContext.BaseDirectory (cobre rodar
    /// de bin/Debug/net8.0/, backend/, raiz do repo).
    ///
    /// Falha cedo se nada existir, com mensagem útil.
    /// </summary>
    private static string ResolveExistingPath(string path)
    {
        if (Path.IsPathRooted(path) && File.Exists(path)) return path;
        if (File.Exists(path)) return Path.GetFullPath(path);

        // Tenta relativo ao AppContext.BaseDirectory subindo níveis.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
        {
            // path pode ser "./x/y.onnx" ou "x/y.onnx" — TrimStart elimina ./
            var trimmed = path.TrimStart('.', '/', '\\');
            var candidate = Path.Combine(dir.FullName, trimmed);
            if (File.Exists(candidate)) return candidate;
        }

        throw new FileNotFoundException(
            $"Path '{path}' não encontrado. Procurei: cwd ({Directory.GetCurrentDirectory()}) " +
            $"e subindo até 6 níveis a partir de {AppContext.BaseDirectory}. " +
            $"Para o MiniLM, rode scripts/download-minilm.sh primeiro.",
            path);
    }
}
