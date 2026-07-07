using System.Text.Json;
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
    /// <summary>Quantas perguntas por trilha. Conservador de propósito:
    /// onboarding longo aumenta abandono. 5 é o sweet spot empírico de
    /// produtos comparáveis (Duolingo arranca em 5-10).</summary>
    public const int QuestionsPerTrail = 5;

    /// <summary>
    /// Monta o teste de nivelamento REUSANDO as perguntas do pipeline forte
    /// (LlmGrounded/ModeratorAuthored) já geradas para os conteúdos da trilha,
    /// em vez de gerar template na hora. Amostra topics distribuídos por
    /// dificuldade (topic.Id == content.Id neste modelo) e, para cada um, pega
    /// a primeira pergunta ativa do conteúdo (determinístico por Id → start e
    /// submit escolhem a mesma).
    /// </summary>
    public IReadOnlyList<LevelingDraft> Build(
        KnowledgeGraph graph,
        IReadOnlyDictionary<int, Content> contentsByTopicId,
        IReadOnlyDictionary<int, IReadOnlyList<GeneratedChallenge>> challengesByContentId)
    {
        if (graph.Topics.Count == 0) return Array.Empty<LevelingDraft>();

        // Amostra apenas topics cujo conteúdo TEM pergunta do pipeline forte —
        // assim não perdemos slots com topics sem cobertura.
        var covered = graph.Topics
            .Where(t => contentsByTopicId.ContainsKey(t.Id)
                     && challengesByContentId.TryGetValue(t.Id, out var cs) && cs.Count > 0)
            .ToList();
        if (covered.Count == 0) return Array.Empty<LevelingDraft>();

        var sampled = SampleByDifficulty(covered, QuestionsPerTrail);

        var drafts = new List<LevelingDraft>();
        var used   = new HashSet<int>();
        foreach (var topic in sampled)
        {
            var content = contentsByTopicId[topic.Id];
            var pool    = challengesByContentId[content.Id];

            var challenge = pool.FirstOrDefault(c => !used.Contains(c.Id));
            if (challenge is null) continue;

            var draft = ToDraft(challenge, topic);
            if (draft is null) continue;

            used.Add(challenge.Id);
            drafts.Add(new LevelingDraft(topic, content, draft));
        }
        return drafts;
    }

    /// <summary>Converte uma GeneratedChallenge persistida no draft usado pelo
    /// onboarding (parseia options/correctIndex do BodyJson). Retorna null se
    /// o corpo estiver malformado — o topic é então pulado.</summary>
    private static GeneratedChallengeDraft? ToDraft(GeneratedChallenge gc, Topic topic)
    {
        try
        {
            using var doc = JsonDocument.Parse(gc.BodyJson);
            var root = doc.RootElement;
            var options = root.GetProperty("options").EnumerateArray()
                .Select(e => e.GetString() ?? "").ToList();
            var correctIndex = root.GetProperty("correctIndex").GetInt32();
            string? explanation = root.TryGetProperty("explanation", out var ex) ? ex.GetString() : null;
            if (options.Count < 2 || correctIndex < 0 || correctIndex >= options.Count) return null;

            return new GeneratedChallengeDraft(
                SourceTopicId:       topic.Id,
                SourceContentId:     gc.ContentId,
                Strategy:            gc.Strategy,
                Prompt:              gc.Prompt,
                Options:             options,
                CorrectIndex:        correctIndex,
                Explanation:         explanation,
                EstimatedDifficulty: gc.EstimatedDifficulty);
        }
        catch (JsonException) { return null; }
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
