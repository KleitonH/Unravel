using Microsoft.Extensions.Logging;
using Unravel.Application.Forge.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.Forge;
using Unravel.Domain.Knowledge;
using Unravel.Infrastructure.Forge.Llm;

namespace Unravel.Infrastructure.Forge.Strategies;

/// <summary>
/// Geração de perguntas via LLM local (PR 20). Conversa com
/// <see cref="ILlmInference"/> — abstração — para ficar testável sem
/// modelo real.
///
/// <para><b>Quando entra no pipeline</b>: a estratégia é registrada no DI
/// somente quando <c>Llm:Enabled=true</c>. O <see cref="ChallengeForge"/>
/// já trata <see cref="IEnumerable{IChallengeStrategy}"/> sem mudança.</para>
///
/// <para><b>Uso típico</b>: dentro do hosted service noturno
/// (<c>LlmGenerationHostedService</c>), latência alta (~30 s/pergunta em
/// CPU) é aceitável porque é batch.</para>
///
/// <para><b>Prompt fechado</b>: pede JSON estrito em PT-BR. Parser
/// <see cref="LlmJsonParser"/> é defensivo; <see cref="QualityGate"/>
/// faz o filtro semântico final.</para>
/// </summary>
public sealed class LlmChallengeStrategy : IChallengeStrategy
{
    private readonly ILlmInference _llm;
    private readonly ILogger<LlmChallengeStrategy> _log;

    public ForgeStrategy Kind => ForgeStrategy.Cloze;  // tipo "raiz" — enum não tem Llm dedicado

    public LlmChallengeStrategy(ILlmInference llm, ILogger<LlmChallengeStrategy> log)
    {
        _llm = llm;
        _log = log;
    }

    public IReadOnlyList<GeneratedChallengeDraft> Generate(
        Content content, Topic topic, KnowledgeGraph graph, int maxDrafts)
    {
        // Síncrono por contrato — Forge é puro/in-process. Internamente
        // bloqueamos por inferência (latência alta), mas o orquestrador
        // noturno aceita isso.
        return GenerateAsync(content, topic, maxDrafts).GetAwaiter().GetResult();
    }

    private async Task<IReadOnlyList<GeneratedChallengeDraft>> GenerateAsync(
        Content content, Topic topic, int maxDrafts)
    {
        var drafts = new List<GeneratedChallengeDraft>();

        for (var i = 0; i < maxDrafts; i++)
        {
            var prompt = BuildPrompt(content, attempt: i);
            string? raw;
            try
            {
                raw = await _llm.CompleteAsync(prompt);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "LLM inference failed for content {ContentId} attempt {Attempt}",
                    content.Id, i);
                break;  // sem sentido tentar mais se o modelo travou
            }

            if (string.IsNullOrWhiteSpace(raw)) continue;

            var draft = LlmJsonParser.TryParse(
                raw, topic.Id, content.Id, topic.DifficultyScore);
            if (draft is not null) drafts.Add(draft);
        }

        return drafts;
    }

    /// <summary>Prompt fechado pedindo JSON estrito. Variar pelo <c>attempt</c>
    /// pra estimular o modelo a gerar perguntas distintas em chamadas
    /// sucessivas (mesmo sem variar temperature).</summary>
    private static string BuildPrompt(Content content, int attempt) => $$"""
Você é um gerador de perguntas de quiz para estudantes de TI.

CONTEÚDO:
Título: {{content.Title}}
{{Truncate(content.Body, 800)}}

Gere UMA pergunta de múltipla escolha em PT-BR sobre o conteúdo acima.
Variação #{{attempt + 1}}: foque em {{(attempt % 3 == 0 ? "definição" : attempt % 3 == 1 ? "aplicação prática" : "comparação")}}.

REGRAS RÍGIDAS:
- 4 alternativas
- 1 correta + 3 distratores plausíveis (que façam o aluno hesitar)
- Não copie literalmente o texto na pergunta
- Saída SOMENTE o JSON abaixo, sem markdown, sem texto antes/depois

FORMATO EXATO:
{
  "prompt": "...",
  "options": ["...", "...", "...", "..."],
  "correctIndex": 0,
  "explanation": "..."
}
""";

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? (s ?? "") : s[..max] + "...";
}
