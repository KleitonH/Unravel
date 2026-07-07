using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unravel.Application.Gamification.Ports;
using Unravel.Application.Social.Ports;
using Unravel.Domain.Gamification;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Gamification;

/// <summary>
/// Motor de missões diárias. Implementa a leitura (<see cref="IDailyQuestService"/>)
/// e o write-path (<see cref="IActivitySink"/>).
///
/// <para><b>Fluxo:</b> cada atividade (responder/acertar/boss) avança as missões
/// do dia que casam com o tipo. Quando uma missão <b>fecha</b>, credita +1 no
/// novelo das parcerias e pontos na meta da caixinha — a missão é a unidade de
/// progresso social. O crédito acontece <b>uma vez</b> por missão (guardado pelo
/// <c>CompletedAt</c>).</para>
///
/// <para><b>Atribuição</b> do conjunto do dia é preguiçosa e idempotente: na
/// primeira atividade/leitura do dia, cria as linhas a partir do catálogo
/// rotativo (<see cref="DailyQuestCatalog.ForDate"/>). O índice único
/// (UserId, QuestDate, QuestKey) protege contra corrida.</para>
/// </summary>
public sealed class DailyQuestEngine : IActivitySink, IDailyQuestService
{
    private readonly ApplicationDbContext         _db;
    private readonly ICaixinhaContributionService _caixinha;
    private readonly IPartnershipService          _partnerships;
    private readonly ILogger<DailyQuestEngine>    _log;

    public DailyQuestEngine(
        ApplicationDbContext         db,
        ICaixinhaContributionService caixinha,
        IPartnershipService          partnerships,
        ILogger<DailyQuestEngine>    log)
    {
        _db           = db;
        _caixinha     = caixinha;
        _partnerships = partnerships;
        _log          = log;
    }

    public async Task RecordAsync(Guid userId, ActivityKind kind, int count, DateTime asOfUtc, CancellationToken ct = default)
    {
        if (count <= 0) return;

        try
        {
            var today   = asOfUtc.Date;
            var quests  = await EnsureAssignedAsync(userId, today, ct);

            var completedKeys = new List<string>();
            foreach (var q in quests.Where(q => !q.IsComplete))
            {
                var def = DailyQuestCatalog.Find(q.QuestKey);
                if (def is null || def.Activity != kind) continue;

                q.Progress = Math.Min(q.Target, q.Progress + count);
                if (q.Progress >= q.Target)
                {
                    q.CompletedAt = asOfUtc;
                    completedKeys.Add(q.QuestKey);
                }
            }

            if (completedKeys.Count == 0)
            {
                await _db.SaveChangesAsync(ct);   // persiste progresso parcial
                return;
            }

            await _db.SaveChangesAsync(ct);

            // Fan-out social: cada missão concluída = +1 novelo e +pontos clã.
            // Best-effort por missão — o social não pode derrubar o estudo.
            foreach (var _ in completedKeys)
            {
                try { await _partnerships.AddProgressAsync(userId, 1, ct); }
                catch (Exception ex) { _log.LogWarning(ex, "Missão: falha ao avançar novelo (user {UserId}).", userId); }

                try { await _caixinha.ContributeAsync(userId, DailyQuestCatalog.CaixinhaPointsPerQuest, asOfUtc, ct); }
                catch (Exception ex) { _log.LogWarning(ex, "Missão: falha ao creditar caixinha (user {UserId}).", userId); }
            }
        }
        catch (Exception ex)
        {
            // Contrato: nunca lançar. Perder o sinal de missão é tolerável.
            _log.LogWarning(ex, "Falha ao registrar atividade {Kind} (user {UserId}).", kind, userId);
        }
    }

    public async Task<IReadOnlyList<DailyQuestView>> GetTodayAsync(Guid userId, DateTime asOfUtc, CancellationToken ct = default)
    {
        var today  = asOfUtc.Date;
        var quests = await EnsureAssignedAsync(userId, today, ct);

        // Ordena pela ordem do conjunto do dia (estável).
        var order = DailyQuestCatalog.ForDate(today).Select((d, i) => (d.Key, i))
                                     .ToDictionary(x => x.Key, x => x.i);

        return quests
            .OrderBy(q => order.TryGetValue(q.QuestKey, out var i) ? i : int.MaxValue)
            .Select(q =>
            {
                var def = DailyQuestCatalog.Find(q.QuestKey);
                return new DailyQuestView(
                    Key:         q.QuestKey,
                    Title:       def?.Title       ?? q.QuestKey,
                    Description: def?.Description  ?? "",
                    Icon:        def?.Icon         ?? "🎯",
                    Target:      q.Target,
                    Progress:    q.Progress,
                    Completed:   q.IsComplete);
            })
            .ToList();
    }

    /// <summary>Garante que as missões do dia existem pro usuário e as retorna
    /// (rastreadas). Idempotente: se já existem, só carrega.</summary>
    private async Task<List<UserDailyQuest>> EnsureAssignedAsync(Guid userId, DateTime today, CancellationToken ct)
    {
        var existing = await _db.UserDailyQuest
            .Where(q => q.UserId == userId && q.QuestDate == today)
            .ToListAsync(ct);
        if (existing.Count > 0) return existing;

        foreach (var def in DailyQuestCatalog.ForDate(today))
            _db.UserDailyQuest.Add(new UserDailyQuest
            {
                UserId    = userId,
                QuestDate = today,
                QuestKey  = def.Key,
                Target    = def.Target,
                Progress  = 0,
            });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Corrida: outra requisição atribuiu primeiro. Recarrega o vencedor.
            foreach (var e in _db.ChangeTracker.Entries<UserDailyQuest>().ToList())
                e.State = EntityState.Detached;
        }

        return await _db.UserDailyQuest
            .Where(q => q.UserId == userId && q.QuestDate == today)
            .ToListAsync(ct);
    }
}
