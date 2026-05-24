using Unravel.Application.Forge.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.Forge;
using Unravel.Domain.Knowledge;

namespace Unravel.Application.Journey.Onboarding;

/// <summary>
/// Constrói o teste de nivelamento de uma trilha: escolhe N topics
/// distribuídos ao longo do espectro de dificuldade e gera 1 pergunta
/// por topic via <see cref="IChallengeForge"/>.
///
/// <para><b>Por que distribuir por dificuldade</b>: testar só raízes
/// classifica todo mundo como iniciante; testar só topo do DAG ignora
/// usuários médios. Pegar topics em ~3 níveis (fácil/médio/avançado)
/// dá um sinal mínimo viável de onde calibrar o planner.</para>
///
/// <para><b>Pure-ish</b>: depende do Forge (também puro), mas não toca
/// BD nem clock. Recebe Contents da trilha e devolve drafts + topic
/// mapping para o use case persistir os ContentTitles correspondentes.</para>
/// </summary>
public sealed class LevelingTestBuilder
{
    private readonly IChallengeForge _forge;

    public LevelingTestBuilder(IChallengeForge forge) => _forge = forge;

    /// <summary>Quantas perguntas por trilha. Conservador de propósito:
    /// onboarding longo aumenta abandono. 5 é o sweet spot empírico de
    /// produtos comparáveis (Duolingo arranca em 5-10).</summary>
    public const int QuestionsPerTrail = 5;

    public IReadOnlyList<LevelingDraft> Build(
        KnowledgeGraph graph,
        IReadOnlyDictionary<int, Content> contentsByTopicId)
    {
        if (graph.Topics.Count == 0) return Array.Empty<LevelingDraft>();

        var sampled = SampleByDifficulty(graph.Topics, QuestionsPerTrail);

        var drafts = new List<LevelingDraft>();
        foreach (var topic in sampled)
        {
            if (!contentsByTopicId.TryGetValue(topic.Id, out var content)) continue;

            // targetUserMastery = topic.difficulty - 0.15 → zona proximal centrada
            // na dificuldade do próprio topic. Assim a pergunta servida bate com
            // o nível pretendido para sondar.
            var poolTarget = Math.Clamp(topic.DifficultyScore - 0.15, 0.05, 0.85);
            var pool = _forge.Build(content, graph, targetCount: 1, targetUserMastery: poolTarget);
            if (pool.Count == 0) continue;

            drafts.Add(new LevelingDraft(topic, content, pool[0]));
        }
        return drafts;
    }

    /// <summary>Seleciona até <paramref name="count"/> topics distribuídos
    /// uniformemente pelo espectro de difficulty. Ordenamos por difficulty
    /// e amostramos em índices equidistantes — determinístico.</summary>
    private static IReadOnlyList<Topic> SampleByDifficulty(
        IEnumerable<Topic> topics, int count)
    {
        var ordered = topics.OrderBy(t => t.DifficultyScore).ThenBy(t => t.Id).ToList();
        if (ordered.Count <= count) return ordered;

        var step = (double)(ordered.Count - 1) / (count - 1);
        var result = new List<Topic>(count);
        for (var i = 0; i < count; i++)
            result.Add(ordered[(int)Math.Round(i * step)]);
        return result.Distinct().ToList();
    }
}

/// <summary>Bundle interno: topic + content + draft. Não é DTO de API
/// (o use case mapeia para <see cref="LevelingQuestion"/>); existe pra
/// permitir testes do builder sem ter que mockar conversão de DTO.</summary>
public sealed record LevelingDraft(Topic Topic, Content Content, GeneratedChallengeDraft Draft);
