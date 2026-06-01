using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Unravel.API.Hubs;
using Unravel.API.Middleware;
using Unravel.Application.Journey.Ports;
using Unravel.Application.Telemetry;
using Unravel.Infrastructure;
using Unravel.Infrastructure.Knowledge;
using Unravel.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
Console.WriteLine($"ENV: {builder.Environment.EnvironmentName}");
Console.WriteLine($"JWT Key: '{builder.Configuration["Jwt:Key"]}'");
Console.WriteLine($"ConnStr: '{builder.Configuration.GetConnectionString("DefaultConnection")}'");


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Unravel API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            []
        }
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var key = builder.Configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT Key is not configured.");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // PR 8 — SignalR usa WebSocket; o header Authorization não é
        // entregue pelo navegador na conexão WS, então aceitamos token
        // via query string `?access_token=` SOMENTE para rotas /hubs/*.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                var path        = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    ctx.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        // 4200 é o default do ng serve; 4201 é o que usamos quando 4200
        // está ocupado por outro projeto local. 127.0.0.1 espelha
        // "localhost" para sandboxes que normalizam diferente.
        policy.WithOrigins(
                  "http://localhost:4200",
                  "http://localhost:4201",
                  "http://127.0.0.1:4200",
                  "http://127.0.0.1:4201")
              .AllowAnyHeader()
              .AllowAnyMethod()
              // SignalR WebSocket exige AllowCredentials para o handshake;
              // origens devem ser explícitas (não "*") quando esse flag está on.
              .AllowCredentials();
    });
});

builder.Services.AddInfrastructure(builder.Configuration);

// PR 19 — OpenTelemetry. Console exporter sempre ligado (visibilidade no
// log durante dev/debug). OTLP exporter opcional via config:
//   "Telemetry": { "Otlp": { "Endpoint": "http://collector:4317" } }
// Sem Endpoint configurado, só console. Sem instrumentação automática
// de EF Core ainda — nossas queries são poucas e curtas; podemos adicionar
// se necessário (`AddSource("Npgsql")`).
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(
        serviceName:    "unravel-api",
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0"))
    .WithMetrics(b =>
    {
        b.AddMeter(UnravelMetrics.MeterName);
        b.AddAspNetCoreInstrumentation();   // duração + status por endpoint

        b.AddConsoleExporter();

        var otlpEndpoint = builder.Configuration["Telemetry:Otlp:Endpoint"];
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            b.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
    });

// PR 7 — cron diário de replanejamento. Registrado aqui (não no
// AddInfrastructure) porque AddHostedService só faz sentido no host;
// testes unitários instanciam o DailyReplanService direto.
builder.Services.AddHostedService<Unravel.Infrastructure.Journey.DailyReplanHostedService>();

// PR 17 — auto-desativador semanal de perguntas com CorrectRate extrema.
builder.Services.AddHostedService<Unravel.Infrastructure.Forge.GeneratedChallengeMaintenanceHostedService>();

// PR 20 — lote noturno de geração via LLM. O serviço inicia sempre,
// mas o orchestrator interno só faz algo se Llm:Enabled=true (caso
// contrário, IServiceProvider.GetRequiredService<ILlmGenerationOrchestrator>
// vai falhar e o try/catch envolvente loga e segue). Mantém estrutura
// uniforme aos outros hosted services.
builder.Services.AddHostedService<Unravel.Infrastructure.Forge.LlmGenerationHostedService>();

// PR 30 — health check do LLM no startup. Loga sucesso/warning sem
// bloquear a API. Só registrado quando Llm:Enabled=true (caso contrário
// ILlmInference não está no DI e GetRequiredService quebra).
if (builder.Configuration.GetValue("Llm:Enabled", false))
{
    builder.Services.AddHostedService<Unravel.Infrastructure.Forge.Llm.LlmHealthCheck>();
}

// PR 8 — SignalR para push real-time. Hub + bus que substitui o
// LoggingJourneyEventBus registrado pelo AddInfrastructure (último
// AddSingleton da mesma interface vence). Mantemos o Logging como
// fallback se SignalR for desligado no futuro.
builder.Services.AddSignalR();
builder.Services.AddSingleton<IJourneyEventBus, SignalRJourneyEventBus>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var connStr = app.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrWhiteSpace(connStr))
    {
        await db.Database.MigrateAsync();
        await TrailSeeder.SeedAsync(db);
        await GamificationSeeder.SeedAsync(db);

        // PR 28 — KnowledgeImporter: importa trilhas em backend/knowledge/.
        // Idempotente (upsert por slug). Pode ser desligado via
        // Knowledge:AutoImport=false em appsettings.
        if (app.Configuration.GetValue("Knowledge:AutoImport", true))
        {
            var importer = scope.ServiceProvider.GetRequiredService<KnowledgeImporter>();
            var rootPath = ResolveKnowledgePath(app.Configuration, app.Environment.ContentRootPath);
            try
            {
                await importer.ImportAllAsync(rootPath);
            }
            catch (Exception ex)
            {
                // Falha de import NÃO deve impedir startup — a app
                // segue funcionando com o conteúdo que já estava no DB.
                app.Logger.LogError(ex, "KnowledgeImporter falhou no startup. Conteúdo prévio preservado.");
            }
        }
    }
}

// Resolve o caminho de Knowledge:Path relativo ao ContentRoot quando
// configurado como caminho relativo. Default: ../../knowledge (pra
// rodar do bin/Debug/net8.0 do projeto API até backend/knowledge).
static string ResolveKnowledgePath(IConfiguration cfg, string contentRoot)
{
    var configured = cfg["Knowledge:Path"];
    if (!string.IsNullOrWhiteSpace(configured) && Path.IsPathRooted(configured))
        return configured;
    var relative = configured ?? "../../knowledge";
    return Path.GetFullPath(Path.Combine(contentRoot, relative));
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionMiddleware>();
app.UseCors("AllowAngular");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// PR 8 — endpoint do hub. /hubs/journey aceita JWT via header ou
// ?access_token= (configurado no JwtBearerEvents acima).
app.MapHub<JourneyHub>("/hubs/journey");

app.Run();
