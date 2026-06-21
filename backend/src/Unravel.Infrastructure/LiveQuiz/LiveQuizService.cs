using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Unravel.Application.LiveQuiz.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.Gamification;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.LiveQuiz;

/// <summary>
/// Implementação EF do núcleo do Quiz ao Vivo. Acessa o DbContext direto
/// (mesmo padrão dos demais services). Snapshot das perguntas no Create
/// (independe da pergunta original mudar depois); estado e pontuação são a
/// fonte da verdade — o SignalR só empurra o que acontece aqui.
/// </summary>
public class LiveQuizService(ApplicationDbContext db) : ILiveQuizService
{
    // Alfabeto sem ambíguos (0/O/1/I) pra código fácil de ditar/digitar.
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<LiveQuizSessionDto> CreateAsync(Guid hostUserId, CreateLiveQuizRequest req, CancellationToken ct = default)
    {
        // Carrega as perguntas escolhidas e preserva a ordem do pick.
        var ids = req.QuestionChallengeIds.Distinct().ToList();
        var loaded = await db.GeneratedChallenge.AsNoTracking()
            .Where(g => ids.Contains(g.Id))
            .Select(g => new { g.Id, g.Prompt, g.BodyJson })
            .ToListAsync(ct);
        var byId = loaded.ToDictionary(x => x.Id);

        var ordered = ids.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
        if (req.ShuffleQuestions) Shuffle(ordered);

        var session = new LiveQuizSession
        {
            HostUserId         = hostUserId,
            Mode               = req.Mode,
            JoinCode           = await GenerateUniqueCodeAsync(ct),
            Status             = LiveQuizStatus.Lobby,
            CurrentQuestionIndex = -1,
            SecondsPerQuestion = Math.Clamp(req.SecondsPerQuestion, 5, 120),
            ShowRankBetween    = req.ShowRankBetween,
            CreatedAt          = DateTime.UtcNow,
        };

        var order = 0;
        foreach (var q in ordered)
        {
            var (options, correctIndex, explanation, shape) = ParseBody(q.BodyJson);
            if (options.Count == 0) continue;

            if (req.ShuffleOptions)
                (options, correctIndex) = ShuffleOptions(options, correctIndex);

            session.Questions.Add(new LiveQuizQuestion
            {
                OrderIndex           = order++,
                GeneratedChallengeId = q.Id,
                Prompt               = q.Prompt,
                OptionsJson          = JsonSerializer.Serialize(options),
                CorrectIndex         = correctIndex,
                Explanation          = explanation,
                Shape                = shape,
            });
        }

        if (req.Mode == LiveQuizMode.Turma)
            foreach (var uid in req.AllowedUserIds.Distinct())
                session.AllowedUsers.Add(new LiveQuizAllowedUser { UserId = uid });

        db.LiveQuizSession.Add(session);
        await db.SaveChangesAsync(ct);
        return ToDto(session, session.Questions.Count, 0);
    }

    public async Task<LiveQuizSessionDto?> GetAsync(int sessionId, CancellationToken ct = default)
    {
        var s = await db.LiveQuizSession.AsNoTracking()
            .Where(x => x.Id == sessionId)
            .Select(x => new { x, qc = x.Questions.Count, pc = x.Participants.Count })
            .FirstOrDefaultAsync(ct);
        return s is null ? null : ToDto(s.x, s.qc, s.pc);
    }

    public async Task<LiveQuizSessionDto?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var norm = (code ?? string.Empty).Trim().ToUpperInvariant();
        var s = await db.LiveQuizSession.AsNoTracking()
            .Where(x => x.JoinCode == norm)
            .Select(x => new { x, qc = x.Questions.Count, pc = x.Participants.Count })
            .FirstOrDefaultAsync(ct);
        return s is null ? null : ToDto(s.x, s.qc, s.pc);
    }

    public async Task<JoinResult> JoinAsync(string code, Guid userId, string displayName, CancellationToken ct = default)
    {
        var norm = (code ?? string.Empty).Trim().ToUpperInvariant();
        var session = await db.LiveQuizSession
            .Include(s => s.AllowedUsers)
            .FirstOrDefaultAsync(s => s.JoinCode == norm, ct);
        if (session is null) return new JoinResult(JoinOutcome.NotFound);
        if (session.Status == LiveQuizStatus.Finished) return new JoinResult(JoinOutcome.Finished);

        if (session.Mode == LiveQuizMode.Turma && session.HostUserId != userId
            && session.AllowedUsers.All(a => a.UserId != userId))
            return new JoinResult(JoinOutcome.NotAllowed);

        var participant = await db.LiveQuizParticipant
            .FirstOrDefaultAsync(p => p.SessionId == session.Id && p.UserId == userId, ct);
        if (participant is null)
        {
            participant = new LiveQuizParticipant
            {
                SessionId   = session.Id,
                UserId      = userId,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Participante" : displayName.Trim(),
                Score       = 0,
                JoinedAt    = DateTime.UtcNow,
            };
            db.LiveQuizParticipant.Add(participant);
            await db.SaveChangesAsync(ct);
        }

        var pc = await db.LiveQuizParticipant.CountAsync(p => p.SessionId == session.Id, ct);
        var qc = await db.LiveQuizQuestion.CountAsync(q => q.SessionId == session.Id, ct);
        return new JoinResult(JoinOutcome.Ok,
            new LiveQuizParticipantDto(participant.Id, participant.UserId, participant.DisplayName, participant.Score),
            ToDto(session, qc, pc));
    }

    public async Task<bool> StartAsync(int sessionId, Guid hostUserId, CancellationToken ct = default)
    {
        var session = await db.LiveQuizSession.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null || session.HostUserId != hostUserId || session.Status != LiveQuizStatus.Lobby) return false;
        if (!await db.LiveQuizQuestion.AnyAsync(q => q.SessionId == sessionId, ct)) return false;

        session.Status                   = LiveQuizStatus.Running;
        session.CurrentQuestionIndex     = 0;
        session.CurrentQuestionStartedAt = DateTime.UtcNow;
        session.StartedAt                = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> AdvanceAsync(int sessionId, Guid hostUserId, CancellationToken ct = default)
    {
        var session = await db.LiveQuizSession.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null || session.HostUserId != hostUserId || session.Status != LiveQuizStatus.Running) return -1;

        var total = await db.LiveQuizQuestion.CountAsync(q => q.SessionId == sessionId, ct);
        var next  = session.CurrentQuestionIndex + 1;
        if (next >= total)
        {
            session.Status  = LiveQuizStatus.Finished;
            session.EndedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return -1;
        }

        session.CurrentQuestionIndex     = next;
        session.CurrentQuestionStartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return next;
    }

    public async Task FinishAsync(int sessionId, Guid hostUserId, CancellationToken ct = default)
    {
        var session = await db.LiveQuizSession.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null || session.HostUserId != hostUserId || session.Status == LiveQuizStatus.Finished) return;
        session.Status  = LiveQuizStatus.Finished;
        session.EndedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<LiveQuizQuestionDto?> CurrentQuestionAsync(int sessionId, CancellationToken ct = default)
    {
        var session = await db.LiveQuizSession.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null || session.Status != LiveQuizStatus.Running) return null;

        var total = await db.LiveQuizQuestion.CountAsync(q => q.SessionId == sessionId, ct);
        var q = await db.LiveQuizQuestion.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SessionId == sessionId && x.OrderIndex == session.CurrentQuestionIndex, ct);
        if (q is null) return null;

        var options = Deserialize(q.OptionsJson);
        return new LiveQuizQuestionDto(q.OrderIndex, total, q.Prompt, options, q.Shape, session.SecondsPerQuestion);
    }

    public async Task<LiveQuizQuestionResultDto?> QuestionResultAsync(int sessionId, int orderIndex, CancellationToken ct = default)
    {
        var q = await db.LiveQuizQuestion.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SessionId == sessionId && x.OrderIndex == orderIndex, ct);
        return q is null ? null : new LiveQuizQuestionResultDto(q.OrderIndex, q.CorrectIndex, q.Explanation);
    }

    public async Task<SubmitLiveAnswerResult> SubmitAnswerAsync(
        int sessionId, Guid userId, int questionOrderIndex, int selectedIndex, DateTime now, CancellationToken ct = default)
    {
        var session = await db.LiveQuizSession.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null || session.Status != LiveQuizStatus.Running || session.CurrentQuestionIndex != questionOrderIndex)
            return new SubmitLiveAnswerResult(false, false, 0, 0, -1);

        var participant = await db.LiveQuizParticipant
            .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == userId, ct);
        if (participant is null) return new SubmitLiveAnswerResult(false, false, 0, 0, -1);

        var question = await db.LiveQuizQuestion.AsNoTracking()
            .FirstOrDefaultAsync(q => q.SessionId == sessionId && q.OrderIndex == questionOrderIndex, ct);
        if (question is null) return new SubmitLiveAnswerResult(false, false, 0, 0, -1);

        // Idempotente: uma resposta por participante por pergunta.
        var existing = await db.LiveQuizAnswer
            .FirstOrDefaultAsync(a => a.SessionId == sessionId && a.ParticipantId == participant.Id
                                   && a.QuestionOrderIndex == questionOrderIndex, ct);
        if (existing is not null)
            return new SubmitLiveAnswerResult(false, existing.IsCorrect, existing.Points, participant.Score, question.CorrectIndex);

        var startedAt  = session.CurrentQuestionStartedAt ?? now;
        var ms         = (int)Math.Max(0, (now - startedAt).TotalMilliseconds);
        var isCorrect  = selectedIndex == question.CorrectIndex;
        var points     = LiveQuizScoring.Points(isCorrect, ms, session.SecondsPerQuestion);

        db.LiveQuizAnswer.Add(new LiveQuizAnswer
        {
            SessionId          = sessionId,
            ParticipantId      = participant.Id,
            QuestionOrderIndex = questionOrderIndex,
            SelectedIndex      = selectedIndex,
            IsCorrect          = isCorrect,
            MsToAnswer         = ms,
            Points             = points,
            AnsweredAt         = now,
        });
        participant.Score += points;
        await db.SaveChangesAsync(ct);

        return new SubmitLiveAnswerResult(true, isCorrect, points, participant.Score, question.CorrectIndex);
    }

    public async Task<IReadOnlyList<LiveQuizLeaderboardRow>> LeaderboardAsync(int sessionId, CancellationToken ct = default)
    {
        var rows = await db.LiveQuizParticipant.AsNoTracking()
            .Where(p => p.SessionId == sessionId)
            .OrderByDescending(p => p.Score).ThenBy(p => p.JoinedAt)
            .Select(p => new { p.UserId, p.DisplayName, p.Score })
            .ToListAsync(ct);

        return rows.Select((p, i) => new LiveQuizLeaderboardRow(i + 1, p.UserId, p.DisplayName, p.Score)).ToList();
    }

    public Task<int> AnsweredCountAsync(int sessionId, int orderIndex, CancellationToken ct = default)
        => db.LiveQuizAnswer.CountAsync(a => a.SessionId == sessionId && a.QuestionOrderIndex == orderIndex, ct);

    // ── helpers ───────────────────────────────────────────────────────

    private static LiveQuizSessionDto ToDto(LiveQuizSession s, int questionCount, int participantCount) =>
        new(s.Id, s.JoinCode, s.Mode.ToString(), s.Status.ToString(), questionCount,
            s.SecondsPerQuestion, s.ShowRankBetween, participantCount, s.CurrentQuestionIndex);

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var code = RandomCode(6);
            // Único entre sessões não encerradas (códigos podem reciclar depois).
            var clash = await db.LiveQuizSession
                .AnyAsync(s => s.JoinCode == code && s.Status != LiveQuizStatus.Finished, ct);
            if (!clash) return code;
        }
        // Fallback improvável: amplia pra 8.
        return RandomCode(8);
    }

    private static string RandomCode(int len)
    {
        var chars = new char[len];
        for (var i = 0; i < len; i++) chars[i] = CodeAlphabet[Random.Shared.Next(CodeAlphabet.Length)];
        return new string(chars);
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static (List<string> options, int correctIndex) ShuffleOptions(List<string> options, int correctIndex)
    {
        var correctText = options[correctIndex];
        var copy = new List<string>(options);
        Shuffle(copy);
        return (copy, copy.IndexOf(correctText));
    }

    private static (List<string> options, int correctIndex, string? explanation, string shape) ParseBody(string bodyJson)
    {
        try
        {
            var root = JsonDocument.Parse(bodyJson).RootElement;
            var options = root.TryGetProperty("options", out var o)
                ? o.EnumerateArray().Select(e => e.GetString() ?? "").ToList()
                : new List<string>();
            var correctIndex = root.TryGetProperty("correctIndex", out var ci) ? ci.GetInt32() : -1;
            var explanation  = root.TryGetProperty("explanation", out var ex) ? ex.GetString() : null;
            var shape        = root.TryGetProperty("shape", out var sh) ? sh.GetString() ?? "MultipleChoice" : "MultipleChoice";
            if (correctIndex < 0 || correctIndex >= options.Count) return (new(), -1, null, shape);
            return (options, correctIndex, explanation, shape);
        }
        catch (JsonException)
        {
            return (new(), -1, null, "MultipleChoice");
        }
    }

    private static List<string> Deserialize(string optionsJson)
    {
        try { return JsonSerializer.Deserialize<List<string>>(optionsJson) ?? new(); }
        catch (JsonException) { return new(); }
    }
}
