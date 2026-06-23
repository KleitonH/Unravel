using Microsoft.EntityFrameworkCore;
using Unravel.Application.Arena.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.Forge;
using Unravel.Infrastructure.Arena;
using Unravel.Infrastructure.Notifications;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Tests.Arena;

/// <summary>
/// Arena (PvP) — matchmaking/desafio, ciclo da partida, pontuação e ranking.
/// EF InMemory.
/// </summary>
public class ArenaServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly ArenaService _sut;
    private readonly Guid _ana;
    private readonly Guid _bia;

    public ArenaServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _sut = new ArenaService(_db, new NotificationService(_db));
        _ana = AddUser("Ana");
        _bia = AddUser("Bia");
    }

    private Guid AddUser(string name)
    {
        var u = User.Create(name, Unravel.Domain.ValueObjects.Email.Create($"{name}{Guid.NewGuid():N}@u.test"), "h");
        _db.User.Add(u);
        _db.SaveChanges();
        return u.Id;
    }

    /// <summary>Cria N questões na trilha 1 com correctIndex 0.</summary>
    private void AddChallenges(int n)
    {
        var body = """{"options":["A","B","C","D"],"correctIndex":0,"explanation":"x","shape":"MultipleChoice"}""";
        for (var i = 0; i < n; i++)
            _db.GeneratedChallenge.Add(new GeneratedChallenge
            {
                ContentId = 1, TopicId = 1, TrailId = 1, Strategy = ForgeStrategy.ModeratorAuthored,
                Prompt = $"Q{i}", BodyJson = body, EstimatedDifficulty = 0.5, IsActive = true,
            });
        _db.SaveChanges();
    }

    [Fact]
    public async Task Enqueue_first_waits_then_second_pairs()
    {
        AddChallenges(3);
        var r1 = await _sut.EnqueueAsync(_ana, 1);
        Assert.False(r1.Matched);

        var r2 = await _sut.EnqueueAsync(_bia, 1);
        Assert.True(r2.Matched);
        Assert.NotNull(r2.MatchId);

        var m = await _sut.GetMatchAsync(r2.MatchId!.Value);
        Assert.Equal("Active", m!.Status);
        Assert.Equal(0, m.CurrentRoundIndex);
        Assert.Equal(_ana, m.Player1Id);
        Assert.Equal(_bia, m.Player2Id);
    }

    [Fact]
    public async Task Enqueue_pairs_with_closest_ranking()
    {
        AddChallenges(5);
        var low  = AddUser("Low");   // pontos 10
        var high = AddUser("High");  // pontos 90
        _db.ArenaRanking.Add(new ArenaRanking { UserId = low,  Points = 10 });
        _db.ArenaRanking.Add(new ArenaRanking { UserId = high, Points = 90 });
        _db.ArenaRanking.Add(new ArenaRanking { UserId = _ana, Points = 80 });
        // Dois já esperando ao mesmo tempo (semeado direto — low entrou antes,
        // FIFO escolheria ele).
        _db.ArenaQueueEntry.Add(new ArenaQueueEntry { UserId = low,  TrailId = 1, CreatedAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc) });
        _db.ArenaQueueEntry.Add(new ArenaQueueEntry { UserId = high, TrailId = 1, CreatedAt = new DateTime(2026, 6, 1, 0, 1, 0, DateTimeKind.Utc) });
        _db.SaveChanges();

        // Quem chega com 80 pontos pareia com o mais próximo (high=90), não FIFO (low).
        var r = await _sut.EnqueueAsync(_ana, 1);
        Assert.True(r.Matched);
        var pm = await _sut.GetMatchAsync(r.MatchId!.Value);
        Assert.Equal(high, pm!.Player1Id);
        Assert.Equal(_ana, pm.Player2Id);
    }

    [Fact]
    public async Task Challenge_creates_pending_notifies_and_accept_starts()
    {
        AddChallenges(2);
        var r = await _sut.ChallengeAsync(_ana, _bia, 1);
        Assert.Equal(ArenaActionOutcome.Ok, r.Outcome);

        var notif = await _db.Notification.SingleAsync(n => n.UserId == _bia);
        Assert.Equal(NotificationType.ArenaChallenge, notif.Type);

        // só o oponente responde
        Assert.Equal(ArenaActionOutcome.NotAuthorized, (await _sut.RespondChallengeAsync(r.MatchId!.Value, _ana, true)).Outcome);

        var acc = await _sut.RespondChallengeAsync(r.MatchId!.Value, _bia, accept: true);
        Assert.Equal(ArenaActionOutcome.Ok, acc.Outcome);
        var m = await _sut.GetMatchAsync(r.MatchId!.Value);
        Assert.Equal("Active", m!.Status);
    }

    [Fact]
    public async Task Challenge_self_rejected()
        => Assert.Equal(ArenaActionOutcome.CannotSelf, (await _sut.ChallengeAsync(_ana, _ana, 1)).Outcome);

    [Fact]
    public async Task Full_match_resolves_winner_and_ranking()
    {
        AddChallenges(1); // 1 rodada → termina rápido
        await _sut.EnqueueAsync(_ana, 1);
        var r = await _sut.EnqueueAsync(_bia, 1);
        var mid = r.MatchId!.Value;
        var started = (await _db.ArenaMatch.AsNoTracking().FirstAsync(x => x.Id == mid)).CurrentRoundStartedAt!.Value;

        // Ana acerta rápido (opção 0); Bia erra.
        var a = await _sut.SubmitAnswerAsync(mid, _ana, 0, 0, started.AddSeconds(1));
        Assert.True(a.Accepted && a.IsCorrect && !a.RoundResolved); // só Ana respondeu
        var b = await _sut.SubmitAnswerAsync(mid, _bia, 0, 2, started.AddSeconds(2));
        Assert.True(b.Accepted && !b.IsCorrect);
        Assert.True(b.RoundResolved && b.MatchFinished);

        var m = await _sut.GetMatchAsync(mid);
        Assert.Equal("Finished", m!.Status);
        Assert.Equal(_ana, m.WinnerId);
        Assert.True(m.Score1 > m.Score2);

        var ranking = await _sut.RankingAsync(10);
        var anaRow = ranking.First(x => x.UserId == _ana);
        Assert.Equal(1, anaRow.Wins);
        Assert.Equal(3, anaRow.Points);
        Assert.Equal(1, ranking.First(x => x.UserId == _bia).Losses);
    }

    [Fact]
    public async Task Submit_is_idempotent_per_player()
    {
        AddChallenges(2);
        await _sut.EnqueueAsync(_ana, 1);
        var r = await _sut.EnqueueAsync(_bia, 1);
        var mid = r.MatchId!.Value;
        var started = (await _db.ArenaMatch.AsNoTracking().FirstAsync(x => x.Id == mid)).CurrentRoundStartedAt!.Value;

        var first  = await _sut.SubmitAnswerAsync(mid, _ana, 0, 0, started.AddSeconds(1));
        var second = await _sut.SubmitAnswerAsync(mid, _ana, 0, 1, started.AddSeconds(2));
        Assert.True(first.Accepted);
        Assert.False(second.Accepted);
    }

    [Fact]
    public async Task Enqueue_no_questions_returns_unmatched()
    {
        // sem questões na trilha 1
        await _sut.EnqueueAsync(_ana, 1);
        var r = await _sut.EnqueueAsync(_bia, 1);
        Assert.False(r.Matched); // não pareia sem questões
    }

    public void Dispose() => _db.Dispose();
}
