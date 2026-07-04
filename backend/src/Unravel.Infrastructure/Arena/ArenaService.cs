using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Unravel.Application.Arena.Ports;
using Unravel.Application.Notifications.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.Gamification;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Arena;

/// <summary>
/// Implementação EF do núcleo da Arena (PvP). Snapshot das questões da
/// trilha-tema no início da partida; pontuação por acerto+velocidade
/// (<see cref="LiveQuizScoring"/>); ranking acumulado. Acesso direto ao
/// DbContext, mesmo padrão dos demais services.
/// </summary>
public class ArenaService(ApplicationDbContext db, INotificationService notifications) : IArenaService
{
    private const int RoundsPerMatch = 5;
    private const int DefaultSeconds = 25;
    private const int ExpiryGraceMs  = 1500; // folga antes de resolver por timeout

    // ── batalha por dano ──
    private const int MaxHp        = 100; // vida inicial
    private const int HitDamage    = 20;  // dano-base de um golpe (acerto sobre quem não acertou)
    private const int CritBonus    = 10;  // dano extra por carga de crítico (ambos acertam → +carga p/ o + rápido)
    private const int ReconnectSec = 30;  // janela pra voltar após cair, senão o oponente vence

    public async Task<EnqueueResult> EnqueueAsync(Guid userId, int trailId, CancellationToken ct = default)
    {
        // 1 batalha ativa por vez: se já houver, volta pra ela (não inicia outra).
        var active = await db.ArenaMatch
            .Where(m => (m.Player1Id == userId || m.Player2Id == userId) && m.Status == ArenaMatchStatus.Active)
            .Select(m => (int?)m.Id).FirstOrDefaultAsync(ct);
        if (active is int existing)
        {
            await RemoveQueueAsync(userId, ct);
            await db.SaveChangesAsync(ct);
            return new EnqueueResult(true, existing);
        }

        // Candidatos esperando no mesmo tema, já com pontos de Arena (ranking)
        // e XP (nível) — pra parear pelo mais próximo, não mais FIFO. Ignora quem
        // já está numa batalha ativa (fila residual).
        var candidates = await (
            from q in db.ArenaQueueEntry
            where q.TrailId == trailId && q.UserId != userId
               && !db.ArenaMatch.Any(m => (m.Player1Id == q.UserId || m.Player2Id == q.UserId) && m.Status == ArenaMatchStatus.Active)
            join u in db.User on q.UserId equals u.Id
            join r in db.ArenaRanking on q.UserId equals r.UserId into rr
            from r in rr.DefaultIfEmpty()
            select new { q.UserId, q.CreatedAt, Points = r != null ? r.Points : 0, u.Xp }
        ).ToListAsync(ct);

        if (candidates.Count > 0)
        {
            // Rating do solicitante (pontos de Arena + XP como nível).
            var mePoints = await db.ArenaRanking.Where(r => r.UserId == userId)
                .Select(r => (int?)r.Points).FirstOrDefaultAsync(ct) ?? 0;
            var meXp = await db.User.Where(u => u.Id == userId).Select(u => u.Xp).FirstOrDefaultAsync(ct);

            // Mais próximo em pontos de ranking; desempata por XP (nível) e, por
            // fim, por tempo de espera (quem espera há mais tempo entra antes).
            var best = candidates
                .OrderBy(c => Math.Abs(c.Points - mePoints))
                .ThenBy(c => Math.Abs(c.Xp - meXp))
                .ThenBy(c => c.CreatedAt)
                .First();

            await RemoveQueueAsync(best.UserId, ct);
            await RemoveQueueAsync(userId, ct);

            var match = new ArenaMatch
            {
                Player1Id = best.UserId,
                Player2Id = userId,
                TrailId   = trailId,
                IsDirectChallenge = false,
            };
            if (!await SnapshotAndStartAsync(match, trailId, ct))
            {
                // Sem questões: devolve o oponente pra fila e aborta.
                db.ArenaQueueEntry.Add(new ArenaQueueEntry { UserId = best.UserId, TrailId = trailId });
                await db.SaveChangesAsync(ct);
                return new EnqueueResult(false);
            }
            db.ArenaMatch.Add(match);
            await db.SaveChangesAsync(ct);
            return new EnqueueResult(true, match.Id);
        }

        // Senão entra na fila (1 entrada por usuário).
        await RemoveQueueAsync(userId, ct);
        db.ArenaQueueEntry.Add(new ArenaQueueEntry { UserId = userId, TrailId = trailId });
        await db.SaveChangesAsync(ct);
        return new EnqueueResult(false);
    }

    public async Task LeaveQueueAsync(Guid userId, CancellationToken ct = default)
    {
        await RemoveQueueAsync(userId, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task RemoveQueueAsync(Guid userId, CancellationToken ct)
    {
        var mine = await db.ArenaQueueEntry.Where(q => q.UserId == userId).ToListAsync(ct);
        if (mine.Count > 0) db.ArenaQueueEntry.RemoveRange(mine);
    }

    public async Task<ArenaActionResult> ChallengeAsync(Guid challengerId, Guid opponentId, int trailId, CancellationToken ct = default)
    {
        if (challengerId == opponentId) return new ArenaActionResult(ArenaActionOutcome.CannotSelf);

        // 1 batalha ativa por vez pra quem desafia.
        var mine = await db.ArenaMatch
            .Where(m => (m.Player1Id == challengerId || m.Player2Id == challengerId) && m.Status == ArenaMatchStatus.Active)
            .Select(m => (int?)m.Id).FirstOrDefaultAsync(ct);
        if (mine is int active) return new ArenaActionResult(ArenaActionOutcome.AlreadyInMatch, active);

        var opponent = await db.User.AsNoTracking().FirstOrDefaultAsync(u => u.Id == opponentId && u.IsActive, ct);
        if (opponent is null) return new ArenaActionResult(ArenaActionOutcome.OpponentNotFound);

        if (!await db.GeneratedChallenge.AnyAsync(g => g.TrailId == trailId && g.IsActive, ct))
            return new ArenaActionResult(ArenaActionOutcome.NoQuestions);

        var match = new ArenaMatch
        {
            Player1Id = challengerId,
            Player2Id = opponentId,
            TrailId   = trailId,
            Status    = ArenaMatchStatus.Pending,
            IsDirectChallenge = true,
            SecondsPerQuestion = DefaultSeconds,
        };
        db.ArenaMatch.Add(match);
        await db.SaveChangesAsync(ct);

        var challengerName = await NameAsync(challengerId, ct);
        await NotifySafe(opponentId, NotificationType.ArenaChallenge, "Desafio na Arena! ⚔️",
            $"{challengerName} te desafiou pra um duelo.", "/arena", ct);

        return new ArenaActionResult(ArenaActionOutcome.Ok, match.Id);
    }

    public async Task<ArenaActionResult> RespondChallengeAsync(int matchId, Guid userId, bool accept, CancellationToken ct = default)
    {
        var match = await db.ArenaMatch.FirstOrDefaultAsync(m => m.Id == matchId, ct);
        if (match is null) return new ArenaActionResult(ArenaActionOutcome.NotFound);
        if (!match.IsDirectChallenge || match.Status != ArenaMatchStatus.Pending || match.Player2Id != userId)
            return new ArenaActionResult(ArenaActionOutcome.NotAuthorized);

        if (!accept)
        {
            match.Status = ArenaMatchStatus.Declined;
            await db.SaveChangesAsync(ct);
            return new ArenaActionResult(ArenaActionOutcome.Ok, match.Id);
        }

        if (!await SnapshotAndStartAsync(match, match.TrailId, ct))
            return new ArenaActionResult(ArenaActionOutcome.NoQuestions);
        await db.SaveChangesAsync(ct);

        var responderName = await NameAsync(userId, ct);
        await NotifySafe(match.Player1Id, NotificationType.ArenaChallenge, "Desafio aceito! ⚔️",
            $"{responderName} aceitou seu duelo. Boa sorte!", "/arena", ct);

        return new ArenaActionResult(ArenaActionOutcome.Ok, match.Id);
    }

    public async Task<SubmitArenaResult> SubmitAnswerAsync(
        int matchId, Guid userId, int roundIndex, int selectedIndex, DateTime now, CancellationToken ct = default)
    {
        var match = await db.ArenaMatch.FirstOrDefaultAsync(m => m.Id == matchId, ct);
        if (match is null || match.Status != ArenaMatchStatus.Active || match.CurrentRoundIndex != roundIndex)
            return new SubmitArenaResult(false, false, 0, false, false, -1);

        var isP1 = match.Player1Id == userId;
        var isP2 = match.Player2Id == userId;
        if (!isP1 && !isP2) return new SubmitArenaResult(false, false, 0, false, false, -1);

        var round = await db.ArenaRound.FirstOrDefaultAsync(r => r.MatchId == matchId && r.OrderIndex == roundIndex, ct);
        if (round is null) return new SubmitArenaResult(false, false, 0, false, false, -1);

        // Idempotente: 1 resposta por jogador por rodada.
        if ((isP1 && round.SelectedIndex1 is not null) || (isP2 && round.SelectedIndex2 is not null))
            return new SubmitArenaResult(false, false, 0, false, false, round.CorrectIndex);

        var startedAt = match.CurrentRoundStartedAt ?? now;
        var ms        = (int)Math.Max(0, (now - startedAt).TotalMilliseconds);
        // selectedIndex < 0 = "pulou / não sei" (normaliza pra -1 = SKIP; -2 = TIMEOUT só na expiração).
        var sel       = selectedIndex < 0 ? -1 : selectedIndex;
        var isCorrect = sel >= 0 && sel == round.CorrectIndex;
        var points    = LiveQuizScoring.Points(isCorrect, ms, match.SecondsPerQuestion);

        if (isP1) { round.SelectedIndex1 = sel; round.MsToAnswer1 = ms; round.Points1 = points; match.Score1 += points; }
        else      { round.SelectedIndex2 = sel; round.MsToAnswer2 = ms; round.Points2 = points; match.Score2 += points; }

        var bothAnswered = round.SelectedIndex1 is not null && round.SelectedIndex2 is not null;
        var finished = false;
        if (bothAnswered)
            finished = await ResolveRoundAsync(match, round, now, ct);

        await db.SaveChangesAsync(ct);
        return new SubmitArenaResult(true, isCorrect, points, bothAnswered, finished, round.CorrectIndex);
    }

    private enum Rk { Correct, Wrong, Skip, Timeout }
    private static Rk Outcome(int? sel, int correctIndex) => sel switch
    {
        null => Rk.Timeout,
        -2   => Rk.Timeout,
        -1   => Rk.Skip,
        var s when s == correctIndex => Rk.Correct,
        _    => Rk.Wrong,
    };

    /// <summary>Apura a rodada pelo modelo de dano/crítico e avança/encerra.
    /// Regra: você toma dano se estourou o tempo OU se o oponente acertou e você
    /// não. Ambos acertam → sem dano, +carga de crítico pro mais rápido. Cada
    /// carga soma <see cref="CritBonus"/> ao próximo golpe (e zera ao usar).
    /// Vence por KO (HP 0) ou, no teto de rodadas, por mais HP.</summary>
    private async Task<bool> ResolveRoundAsync(ArenaMatch match, ArenaRound round, DateTime now, CancellationToken ct)
    {
        var o1 = Outcome(round.SelectedIndex1, round.CorrectIndex);
        var o2 = Outcome(round.SelectedIndex2, round.CorrectIndex);

        // Ambos acertam: ninguém toma dano; carga de crítico pro que respondeu primeiro.
        if (o1 == Rk.Correct && o2 == Rk.Correct)
        {
            var m1 = round.MsToAnswer1 ?? int.MaxValue;
            var m2 = round.MsToAnswer2 ?? int.MaxValue;
            if (m1 < m2) match.Crit1++;
            else if (m2 < m1) match.Crit2++;
        }

        // Dano ao P1
        int dmg1 = 0, dmg2 = 0;
        if (o2 == Rk.Correct && o1 != Rk.Correct) { dmg1 = HitDamage + CritBonus * match.Crit2; match.Crit2 = 0; }
        else if (o1 == Rk.Timeout)                { dmg1 = HitDamage; }
        // Dano ao P2
        if (o1 == Rk.Correct && o2 != Rk.Correct) { dmg2 = HitDamage + CritBonus * match.Crit1; match.Crit1 = 0; }
        else if (o2 == Rk.Timeout)                { dmg2 = HitDamage; }

        match.Hp1 = Math.Max(0, match.Hp1 - dmg1);
        match.Hp2 = Math.Max(0, match.Hp2 - dmg2);
        round.Damage1 = dmg1;
        round.Damage2 = dmg2;

        var total = await db.ArenaRound.CountAsync(r => r.MatchId == match.Id, ct);
        var next  = match.CurrentRoundIndex + 1;
        var ko    = match.Hp1 <= 0 || match.Hp2 <= 0;
        if (ko || next >= total) { await FinishByHpAsync(match, ct); return true; }
        match.CurrentRoundIndex = next;
        match.CurrentRoundStartedAt = now;
        return false;
    }

    public async Task<ArenaResolveResult> ResolveExpiredRoundAsync(
        int matchId, int roundIndex, DateTime now, CancellationToken ct = default)
    {
        var match = await db.ArenaMatch.FirstOrDefaultAsync(m => m.Id == matchId, ct);
        if (match is null || match.Status != ArenaMatchStatus.Active || match.CurrentRoundIndex != roundIndex)
            return new ArenaResolveResult(false, false);

        var startedAt  = match.CurrentRoundStartedAt ?? now;
        var deadlineMs = match.SecondsPerQuestion * 1000.0 + ExpiryGraceMs;
        if ((now - startedAt).TotalMilliseconds < deadlineMs)
            return new ArenaResolveResult(false, false); // ainda dentro do prazo

        var round = await db.ArenaRound.FirstOrDefaultAsync(r => r.MatchId == matchId && r.OrderIndex == roundIndex, ct);
        if (round is null) return new ArenaResolveResult(false, false);

        // Quem não respondeu = TIMEOUT (-2) → toma dano na apuração.
        var limitMs = match.SecondsPerQuestion * 1000;
        if (round.SelectedIndex1 is null) { round.SelectedIndex1 = -2; round.MsToAnswer1 = limitMs; round.Points1 = 0; }
        if (round.SelectedIndex2 is null) { round.SelectedIndex2 = -2; round.MsToAnswer2 = limitMs; round.Points2 = 0; }

        var finished = await ResolveRoundAsync(match, round, now, ct);
        await db.SaveChangesAsync(ct);
        return new ArenaResolveResult(true, finished);
    }

    public async Task<ArenaMatchDto?> GetMatchAsync(int matchId, CancellationToken ct = default)
    {
        var m = await db.ArenaMatch.AsNoTracking().FirstOrDefaultAsync(x => x.Id == matchId, ct);
        if (m is null) return null;
        var total = await db.ArenaRound.CountAsync(r => r.MatchId == matchId, ct);
        return await ToDtoAsync(m, total, ct);
    }

    public async Task<ArenaRoundDto?> CurrentRoundAsync(int matchId, CancellationToken ct = default)
    {
        var m = await db.ArenaMatch.AsNoTracking().FirstOrDefaultAsync(x => x.Id == matchId, ct);
        if (m is null || m.Status != ArenaMatchStatus.Active) return null;
        var total = await db.ArenaRound.CountAsync(r => r.MatchId == matchId, ct);
        var r = await db.ArenaRound.AsNoTracking().FirstOrDefaultAsync(x => x.MatchId == matchId && x.OrderIndex == m.CurrentRoundIndex, ct);
        if (r is null) return null;
        return new ArenaRoundDto(r.OrderIndex, total, r.Prompt, Deserialize(r.OptionsJson), r.Shape, m.SecondsPerQuestion);
    }

    public async Task<ArenaRoundResultDto?> RoundResultAsync(int matchId, int orderIndex, CancellationToken ct = default)
    {
        var m = await db.ArenaMatch.AsNoTracking().FirstOrDefaultAsync(x => x.Id == matchId, ct);
        var r = await db.ArenaRound.AsNoTracking().FirstOrDefaultAsync(x => x.MatchId == matchId && x.OrderIndex == orderIndex, ct);
        if (m is null || r is null) return null;

        // Crítico concedido nesta rodada: ambos acertaram → quem respondeu primeiro.
        Guid? critTo = null;
        if (Outcome(r.SelectedIndex1, r.CorrectIndex) == Rk.Correct && Outcome(r.SelectedIndex2, r.CorrectIndex) == Rk.Correct)
        {
            var m1 = r.MsToAnswer1 ?? int.MaxValue; var m2 = r.MsToAnswer2 ?? int.MaxValue;
            if (m1 < m2) critTo = m.Player1Id; else if (m2 < m1) critTo = m.Player2Id;
        }

        return new ArenaRoundResultDto(r.OrderIndex, r.CorrectIndex, m.Score1, m.Score2,
            m.Status == ArenaMatchStatus.Finished, m.WinnerId,
            m.Hp1, m.Hp2, r.Damage1, r.Damage2, m.Crit1, m.Crit2, critTo);
    }

    public async Task<IReadOnlyList<ArenaRankingRow>> RankingAsync(int top, CancellationToken ct = default)
    {
        var rows = await db.ArenaRanking.AsNoTracking()
            .OrderByDescending(r => r.Points).ThenByDescending(r => r.Wins)
            .Take(top <= 0 ? 20 : Math.Min(top, 100))
            .Select(r => new { r.UserId, Name = r.User!.Name, r.Points, r.Wins, r.Losses, r.Draws })
            .ToListAsync(ct);
        return rows.Select((r, i) => new ArenaRankingRow(i + 1, r.UserId, r.Name, r.Points, r.Wins, r.Losses, r.Draws)).ToList();
    }

    public async Task<IReadOnlyList<ArenaMatchDto>> MyOpenMatchesAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await db.ArenaMatch.AsNoTracking()
            .Where(m => (m.Player1Id == userId || m.Player2Id == userId)
                     && (m.Status == ArenaMatchStatus.Pending || m.Status == ArenaMatchStatus.Active))
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);
        var list = new List<ArenaMatchDto>();
        foreach (var m in rows)
        {
            var total = await db.ArenaRound.CountAsync(r => r.MatchId == m.Id, ct);
            list.Add(await ToDtoAsync(m, total, ct));
        }
        return list;
    }

    // ── helpers ───────────────────────────────────────────────────────

    private async Task<bool> SnapshotAndStartAsync(ArenaMatch match, int trailId, CancellationToken ct)
    {
        var pool = await db.GeneratedChallenge.AsNoTracking()
            .Where(g => g.TrailId == trailId && g.IsActive)
            .Select(g => new { g.Id, g.Prompt, g.BodyJson })
            .ToListAsync(ct);

        var picked = pool.OrderBy(_ => Random.Shared.Next()).Take(RoundsPerMatch).ToList();
        var order = 0;
        foreach (var q in picked)
        {
            var (options, correctIndex, explanation, shape) = ParseBody(q.BodyJson);
            if (options.Count == 0) continue;
            match.Rounds.Add(new ArenaRound
            {
                OrderIndex = order++, GeneratedChallengeId = q.Id, Prompt = q.Prompt,
                OptionsJson = JsonSerializer.Serialize(options), CorrectIndex = correctIndex,
                Explanation = explanation, Shape = shape,
            });
        }
        if (match.Rounds.Count == 0) return false;

        match.Status = ArenaMatchStatus.Active;
        match.CurrentRoundIndex = 0;
        match.CurrentRoundStartedAt = DateTime.UtcNow;
        match.StartedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>Encerra por KO (HP 0) ou no teto de rodadas (mais HP vence;
    /// empate de HP desempata por cargas de crítico; senão empate real).</summary>
    private async Task FinishByHpAsync(ArenaMatch match, CancellationToken ct)
    {
        Guid? winner;
        if (match.Hp1 <= 0 && match.Hp2 <= 0) winner = null;              // duplo KO
        else if (match.Hp2 <= 0)              winner = match.Player1Id;    // P2 nocauteado
        else if (match.Hp1 <= 0)              winner = match.Player2Id;    // P1 nocauteado
        else if (match.Hp1 != match.Hp2)      winner = match.Hp1 > match.Hp2 ? match.Player1Id : match.Player2Id;
        else if (match.Crit1 != match.Crit2)  winner = match.Crit1 > match.Crit2 ? match.Player1Id : match.Player2Id;
        else                                  winner = null;              // empate real
        await SettleAsync(match, winner, ct);
    }

    /// <summary>Aplica status Finished + WinnerId e atualiza o ranking dos dois.</summary>
    private async Task SettleAsync(ArenaMatch match, Guid? winner, CancellationToken ct)
    {
        match.Status   = ArenaMatchStatus.Finished;
        match.EndedAt  = DateTime.UtcNow;
        match.WinnerId = winner;

        var p1 = match.Player1Id;
        var p2 = match.Player2Id!.Value;
        if (winner is null) { await BumpAsync(p1, "draw", ct); await BumpAsync(p2, "draw", ct); }
        else if (winner == p1) { await BumpAsync(p1, "win", ct); await BumpAsync(p2, "loss", ct); }
        else { await BumpAsync(p2, "win", ct); await BumpAsync(p1, "loss", ct); }
    }

    public async Task MarkDisconnectedAsync(int matchId, Guid userId, DateTime now, CancellationToken ct = default)
    {
        var m = await db.ArenaMatch.FirstOrDefaultAsync(x => x.Id == matchId, ct);
        if (m is null || m.Status != ArenaMatchStatus.Active) return;
        if (m.Player1Id != userId && m.Player2Id != userId) return;
        // só marca se ninguém já está no relógio de abandono
        if (m.DisconnectedUserId is null)
        {
            m.DisconnectedUserId = userId;
            m.DisconnectedAt     = now;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task ClearDisconnectAsync(int matchId, Guid userId, CancellationToken ct = default)
    {
        var m = await db.ArenaMatch.FirstOrDefaultAsync(x => x.Id == matchId, ct);
        if (m is null || m.DisconnectedUserId != userId) return;
        m.DisconnectedUserId = null;
        m.DisconnectedAt     = null;
        await db.SaveChangesAsync(ct);
    }

    public async Task<ArenaResolveResult> ResolveAbandonmentAsync(int matchId, DateTime now, CancellationToken ct = default)
    {
        var m = await db.ArenaMatch.FirstOrDefaultAsync(x => x.Id == matchId, ct);
        if (m is null || m.Status != ArenaMatchStatus.Active || m.DisconnectedUserId is not Guid gone || m.DisconnectedAt is not DateTime at)
            return new ArenaResolveResult(false, false);
        if ((now - at).TotalSeconds < ReconnectSec) return new ArenaResolveResult(false, false);

        // Vence quem ficou (o que não desconectou).
        var winner = m.Player1Id == gone ? m.Player2Id : m.Player1Id;
        await SettleAsync(m, winner, ct);
        await db.SaveChangesAsync(ct);
        return new ArenaResolveResult(true, true);
    }

    private async Task BumpAsync(Guid userId, string result, CancellationToken ct)
    {
        var r = await db.ArenaRanking.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (r is null) { r = new ArenaRanking { UserId = userId }; db.ArenaRanking.Add(r); }
        switch (result)
        {
            case "win":  r.Wins++;   r.Points += 3; break;
            case "draw": r.Draws++;  r.Points += 1; break;
            default:     r.Losses++;              break;
        }
    }

    private async Task<ArenaMatchDto> ToDtoAsync(ArenaMatch m, int totalRounds, CancellationToken ct)
    {
        var p1Name = await NameAsync(m.Player1Id, ct);
        var p2Name = m.Player2Id is null ? null : await NameAsync(m.Player2Id.Value, ct);
        var p1Cos  = await CosmeticsAsync(m.Player1Id, ct);
        var p2Cos  = m.Player2Id is null ? new List<ArenaCosmeticDto>() : await CosmeticsAsync(m.Player2Id.Value, ct);
        int? dcLeft = m.DisconnectedAt is DateTime at
            ? Math.Max(0, ReconnectSec - (int)(DateTime.UtcNow - at).TotalSeconds)
            : null;
        return new ArenaMatchDto(m.Id, m.Status.ToString(), m.TrailId, m.Player1Id, p1Name,
            m.Player2Id, p2Name, m.Score1, m.Score2, m.WinnerId, m.CurrentRoundIndex, totalRounds, m.SecondsPerQuestion,
            p1Cos, p2Cos, m.Hp1, m.Hp2, m.Crit1, m.Crit2, MaxHp, m.DisconnectedUserId, dcLeft);
    }

    /// <summary>Cosméticos equipados de um jogador, no slot do NAVI (pra render no duelo).</summary>
    private async Task<List<ArenaCosmeticDto>> CosmeticsAsync(Guid userId, CancellationToken ct)
    {
        var rows = await db.UserCosmetic.AsNoTracking()
            .Where(uc => uc.UserId == userId && uc.IsEquipped)
            .Select(uc => new { uc.Cosmetic.Type, uc.Cosmetic.AssetSlug })
            .ToListAsync(ct);

        return rows
            .Select(r => new ArenaCosmeticDto(SlotOf(r.Type), r.AssetSlug))
            .Where(c => c.Slot.Length > 0)
            .ToList();
    }

    private static string SlotOf(CosmeticType type) => type switch
    {
        CosmeticType.Hat        => "hat",
        CosmeticType.Accessory  => "accessory",
        CosmeticType.Fur        => "fur",
        CosmeticType.Expression => "mood",
        _                       => "",
    };

    private Task<string> NameAsync(Guid userId, CancellationToken ct)
        => db.User.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync(ct)!;

    private async Task NotifySafe(Guid userId, NotificationType type, string title, string body, string? link, CancellationToken ct)
    {
        try { await notifications.CreateAsync(userId, type, title, body, link, ct); } catch { /* best-effort */ }
    }

    private static (List<string> options, int correctIndex, string? explanation, string shape) ParseBody(string bodyJson)
    {
        try
        {
            var root = JsonDocument.Parse(bodyJson).RootElement;
            var options = root.TryGetProperty("options", out var o)
                ? o.EnumerateArray().Select(e => e.GetString() ?? "").ToList() : new List<string>();
            var correctIndex = root.TryGetProperty("correctIndex", out var ci) ? ci.GetInt32() : -1;
            var explanation  = root.TryGetProperty("explanation", out var ex) ? ex.GetString() : null;
            var shape        = root.TryGetProperty("shape", out var sh) ? sh.GetString() ?? "MultipleChoice" : "MultipleChoice";
            if (correctIndex < 0 || correctIndex >= options.Count) return (new(), -1, null, shape);
            return (options, correctIndex, explanation, shape);
        }
        catch (JsonException) { return (new(), -1, null, "MultipleChoice"); }
    }

    private static List<string> Deserialize(string optionsJson)
    {
        try { return JsonSerializer.Deserialize<List<string>>(optionsJson) ?? new(); }
        catch (JsonException) { return new(); }
    }
}
