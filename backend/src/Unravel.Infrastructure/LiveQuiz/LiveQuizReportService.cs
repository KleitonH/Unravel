using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Unravel.Application.LiveQuiz.Ports;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.LiveQuiz;

/// <summary>
/// UC30 — relatório pedagógico da turma. Agrega as respostas já persistidas de
/// uma sessão de Quiz ao Vivo: acertos por questão, lacunas por tópico
/// (conteúdo de origem via GeneratedChallenge.ContentId → Content.Title) e
/// desempenho individual/agregado. Só o professor que hospedou a sessão lê.
/// </summary>
public class LiveQuizReportService(ApplicationDbContext db) : ILiveQuizReportService
{
    public async Task<IReadOnlyList<LiveQuizSessionListItem>> ListHostSessionsAsync(Guid hostUserId, CancellationToken ct = default)
    {
        var sessions = await db.LiveQuizSession.AsNoTracking()
            .Where(s => s.HostUserId == hostUserId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.Id, s.JoinCode, s.Mode, s.Status, s.CreatedAt, s.EndedAt,
                ParticipantCount = db.LiveQuizParticipant.Count(p => p.SessionId == s.Id),
                QuestionCount    = db.LiveQuizQuestion.Count(q => q.SessionId == s.Id),
            })
            .ToListAsync(ct);

        return sessions.Select(s => new LiveQuizSessionListItem(
            s.Id, s.JoinCode, s.Mode.ToString(), s.Status.ToString(),
            s.CreatedAt, s.EndedAt, s.ParticipantCount, s.QuestionCount)).ToList();
    }

    public async Task<LiveQuizReportDto?> GetReportAsync(Guid hostUserId, int sessionId, CancellationToken ct = default)
    {
        var session = await db.LiveQuizSession.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null || session.HostUserId != hostUserId) return null;

        var questions = await db.LiveQuizQuestion.AsNoTracking()
            .Where(q => q.SessionId == sessionId)
            .OrderBy(q => q.OrderIndex)
            .Select(q => new { q.OrderIndex, q.Prompt, q.CorrectIndex, q.OptionsJson, q.GeneratedChallengeId })
            .ToListAsync(ct);

        var participants = await db.LiveQuizParticipant.AsNoTracking()
            .Where(p => p.SessionId == sessionId)
            .Select(p => new { p.Id, p.UserId, p.DisplayName, p.Score })
            .ToListAsync(ct);

        var answers = await db.LiveQuizAnswer.AsNoTracking()
            .Where(a => a.SessionId == sessionId)
            .Select(a => new { a.ParticipantId, a.QuestionOrderIndex, a.SelectedIndex, a.IsCorrect, a.MsToAnswer })
            .ToListAsync(ct);

        // ---- por questão ----
        var byQuestion = answers.ToLookup(a => a.QuestionOrderIndex);
        var questionRows = questions.Select(q =>
        {
            var qa = byQuestion[q.OrderIndex].ToList();
            var total = qa.Count;
            var correct = qa.Count(a => a.IsCorrect);
            var avgMs = total > 0 ? (int)qa.Average(a => a.MsToAnswer) : 0;

            var optionCount = CountOptions(q.OptionsJson);
            var dist = new int[Math.Max(optionCount, 1)];
            foreach (var a in qa)
                if (a.SelectedIndex >= 0 && a.SelectedIndex < dist.Length) dist[a.SelectedIndex]++;

            return new LiveQuizReportQuestionRow(
                q.OrderIndex, q.Prompt, q.CorrectIndex, total, correct,
                total > 0 ? (double)correct / total : 0, avgMs, dist);
        }).ToList();

        // ---- por participante ----
        var byParticipant = answers.ToLookup(a => a.ParticipantId);
        var participantRows = participants.Select(p =>
        {
            var pa = byParticipant[p.Id].ToList();
            var answered = pa.Count;
            var correct = pa.Count(a => a.IsCorrect);
            return new
            {
                p.UserId, p.DisplayName, p.Score, Answered = answered, Correct = correct,
                Accuracy = answered > 0 ? (double)correct / answered : 0,
            };
        })
        .OrderByDescending(p => p.Score)
        .Select((p, i) => new LiveQuizReportParticipantRow(
            i + 1, p.UserId, p.DisplayName, p.Score, p.Answered, p.Correct, p.Accuracy))
        .ToList();

        // ---- por tópico (conteúdo de origem) ----
        var challengeIds = questions.Select(q => q.GeneratedChallengeId).Distinct().ToList();
        var challengeToContent = await db.GeneratedChallenge.AsNoTracking()
            .Where(g => challengeIds.Contains(g.Id))
            .Select(g => new { g.Id, g.ContentId })
            .ToDictionaryAsync(g => g.Id, g => g.ContentId, ct);

        var contentIds = challengeToContent.Values.Distinct().ToList();
        var contentTitles = await db.Content.AsNoTracking()
            .Where(c => contentIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Title })
            .ToDictionaryAsync(c => c.Id, c => c.Title, ct);

        // orderIndex → título do tópico/conteúdo
        var orderToTopic = questions.ToDictionary(
            q => q.OrderIndex,
            q => challengeToContent.TryGetValue(q.GeneratedChallengeId, out var cid)
                 && contentTitles.TryGetValue(cid, out var title) && !string.IsNullOrWhiteSpace(title)
                    ? title
                    : "Sem tópico");

        var topicRows = answers
            .GroupBy(a => orderToTopic.GetValueOrDefault(a.QuestionOrderIndex, "Sem tópico"))
            .Select(g =>
            {
                var total = g.Count();
                var correct = g.Count(a => a.IsCorrect);
                return new LiveQuizReportTopicRow(g.Key, total, correct, total > 0 ? (double)correct / total : 0);
            })
            .OrderBy(t => t.Accuracy) // lacunas primeiro
            .ToList();

        // ---- resumo ----
        var answersCount = answers.Count;
        var overallCorrect = answers.Count(a => a.IsCorrect);
        var summary = new LiveQuizReportSummary(
            session.Id, session.JoinCode, session.Mode.ToString(), session.Status.ToString(),
            session.EndedAt, participants.Count, questions.Count, answersCount,
            answersCount > 0 ? (double)overallCorrect / answersCount : 0,
            participants.Count > 0 ? participants.Average(p => p.Score) : 0);

        return new LiveQuizReportDto(summary, questionRows, participantRows, topicRows);
    }

    private static int CountOptions(string optionsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(optionsJson) ? "[]" : optionsJson);
            return doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.GetArrayLength() : 0;
        }
        catch { return 0; }
    }
}
