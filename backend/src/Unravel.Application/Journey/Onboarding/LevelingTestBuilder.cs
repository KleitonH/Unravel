using System.Text.Json;
using Unravel.Domain.Entities;
using Unravel.Domain.Forge;

namespace Unravel.Application.Journey.Onboarding;

/// <summary>
/// Constrói o teste de nivelamento de uma trilha REUSANDO as perguntas do
/// pipeline forte (LlmGrounded/ModeratorAuthored) já geradas e persistidas.
/// Escolhe <see cref="QuestionsPerTrail"/> perguntas distribuídas ao longo do
/// espectro de dificuldade, priorizando <b>diversidade de conteúdo</b> (uma por
/// conteúdo primeiro; se faltar, completa com mais perguntas dos conteúdos já
/// cobertos). Assim trilhas com muitos conteúdos ganham cobertura ampla e
/// trilhas de conteúdo único ainda rendem um teste cheio.
///
/// <para><b>Determinístico</b>: recebe as perguntas ordenadas por Id e a
/// seleção é uma função pura da entrada, então start e submit escolhem
/// exatamente as mesmas perguntas (não persistimos a sessão).</para>
///
/// <para><b>Por que distribuir por dificuldade</b>: testar só o fácil
/// classifica todo mundo como iniciante; só o difícil ignora o usuário médio.
/// Amostrar ao longo do espectro dá o sinal mínimo para calibrar o planner.</para>
/// </summary>
public sealed class LevelingTestBuilder
{
    /// <summary>Quantas perguntas por trilha no teste de nivelamento.</summary>
    public const int QuestionsPerTrail = 6;

    /// <summary>
    /// Monta os drafts do nivelamento de UMA trilha a partir das suas perguntas
    /// do pipeline forte. <paramref name="contentsById"/> fornece os títulos
    /// dos conteúdos para o DTO.
    /// </summary>
    public IReadOnlyList<LevelingDraft> Build(
        IReadOnlyList<GeneratedChallenge> trailChallenges,
        IReadOnlyDictionary<int, Content> contentsById)
    {
        if (trailChallenges.Count == 0) return Array.Empty<LevelingDraft>();

        var selected = SelectSpread(trailChallenges, QuestionsPerTrail);

        var drafts = new List<LevelingDraft>(selected.Count);
        foreach (var gc in selected)
        {
            if (!contentsById.TryGetValue(gc.ContentId, out var content)) continue;
            var draft = ToDraft(gc);
            if (draft is null) continue;
            drafts.Add(new LevelingDraft(gc.Id, gc.TopicId, content, draft));
        }
        return drafts;
    }

    /// <summary>
    /// Seleciona até <paramref name="count"/> perguntas distribuídas por
    /// dificuldade, priorizando diversidade de conteúdo. Estratégia: agrupa por
    /// conteúdo, ordena cada grupo por dificuldade e faz round-robin entre os
    /// conteúdos (1ª de cada, depois 2ª de cada, ...) até atingir o alvo. Como
    /// as entradas vêm ordenadas por Id, é determinístico.
    /// </summary>
    private static IReadOnlyList<GeneratedChallenge> SelectSpread(
        IReadOnlyList<GeneratedChallenge> challenges, int count)
    {
        if (challenges.Count <= count) return challenges;

        // Grupos por conteúdo, cada um ordenado por dificuldade (depois Id).
        var groups = challenges
            .GroupBy(c => c.ContentId)
            .OrderBy(g => g.Key)
            .Select(g => g.OrderBy(c => c.EstimatedDifficulty).ThenBy(c => c.Id).ToList())
            .ToList();

        var picked = new List<GeneratedChallenge>(count);
        var round  = 0;
        while (picked.Count < count)
        {
            var advanced = false;
            foreach (var g in groups)
            {
                if (round < g.Count)
                {
                    picked.Add(g[round]);
                    advanced = true;
                    if (picked.Count == count) break;
                }
            }
            if (!advanced) break; // esgotou todas as perguntas
            round++;
        }

        // Ordena o resultado por dificuldade para o teste ir do fácil ao difícil.
        return picked.OrderBy(c => c.EstimatedDifficulty).ThenBy(c => c.Id).ToList();
    }

    /// <summary>Converte uma GeneratedChallenge persistida no draft usado pelo
    /// onboarding (parseia options/correctIndex do BodyJson). Retorna null se
    /// o corpo estiver malformado — a pergunta é então pulada.</summary>
    private static GeneratedChallengeDraft? ToDraft(GeneratedChallenge gc)
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
                SourceTopicId:       gc.TopicId,
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
}

/// <summary>Bundle interno: pergunta selecionada para o nivelamento.
/// <c>ChallengeId</c> identifica a resposta; <c>TopicId</c> semeia a Mastery
/// (várias perguntas podem compartilhar o topic/conteúdo).</summary>
public sealed record LevelingDraft(
    int ChallengeId, int TopicId, Content Content, GeneratedChallengeDraft Draft);
