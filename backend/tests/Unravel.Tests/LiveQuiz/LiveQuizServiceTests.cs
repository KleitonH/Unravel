using Microsoft.EntityFrameworkCore;
using Unravel.Application.LiveQuiz.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.Forge;
using Unravel.Infrastructure.LiveQuiz;
using Unravel.Infrastructure.Notifications;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Tests.LiveQuiz;

/// <summary>
/// Quiz ao Vivo — snapshot/estado/scoring/autorização. EF InMemory.
/// </summary>
public class LiveQuizServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly LiveQuizService _sut;
    private readonly Guid _host = Guid.NewGuid();

    public LiveQuizServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _sut = new LiveQuizService(_db, new NotificationService(_db));
    }

    /// <summary>Cria uma pergunta no pool com correctIndex 0 (opção "A").</summary>
    private int AddChallenge(string prompt)
    {
        var body = """{"options":["A","B","C","D"],"correctIndex":0,"explanation":"pq","shape":"MultipleChoice"}""";
        var g = new GeneratedChallenge
        {
            ContentId = 1, TopicId = 1, TrailId = 1,
            Strategy = ForgeStrategy.ModeratorAuthored,
            Prompt = prompt, BodyJson = body, EstimatedDifficulty = 0.5, IsActive = true,
        };
        _db.GeneratedChallenge.Add(g);
        _db.SaveChanges();
        return g.Id;
    }

    private CreateLiveQuizRequest Req(LiveQuizMode mode, IReadOnlyList<int> qids, IReadOnlyList<Guid>? allowed = null) =>
        new(mode, SecondsPerQuestion: 20, ShowRankBetween: true, ShuffleQuestions: false, ShuffleOptions: false,
            QuestionChallengeIds: qids, AllowedUserIds: allowed ?? Array.Empty<Guid>());

    [Fact]
    public async Task Create_snapshots_questions_in_order()
    {
        var ids = new[] { AddChallenge("Q1"), AddChallenge("Q2"), AddChallenge("Q3") };
        var dto = await _sut.CreateAsync(_host, Req(LiveQuizMode.Livre, ids));

        Assert.Equal(3, dto.QuestionCount);
        Assert.True(dto.JoinCode.Length >= 6);
        Assert.Equal("Lobby", dto.Status);
        Assert.Equal(-1, dto.CurrentQuestionIndex);

        var qs = await _db.LiveQuizQuestion.Where(q => q.SessionId == dto.Id).OrderBy(q => q.OrderIndex).ToListAsync();
        Assert.Equal(new[] { "Q1", "Q2", "Q3" }, qs.Select(q => q.Prompt));
        Assert.All(qs, q => Assert.Equal(0, q.CorrectIndex));
    }

    [Fact]
    public async Task Join_livre_is_open()
    {
        var dto = await _sut.CreateAsync(_host, Req(LiveQuizMode.Livre, new[] { AddChallenge("Q1") }));
        var r = await _sut.JoinAsync(dto.JoinCode, Guid.NewGuid(), "Ana");
        Assert.Equal(JoinOutcome.Ok, r.Outcome);
    }

    [Fact]
    public async Task Join_turma_enforces_whitelist()
    {
        var ana = Guid.NewGuid();
        var bia = Guid.NewGuid();
        var dto = await _sut.CreateAsync(_host, Req(LiveQuizMode.Turma, new[] { AddChallenge("Q1") }, new[] { ana }));

        Assert.Equal(JoinOutcome.NotAllowed, (await _sut.JoinAsync(dto.JoinCode, bia, "Bia")).Outcome);
        Assert.Equal(JoinOutcome.Ok,         (await _sut.JoinAsync(dto.JoinCode, ana, "Ana")).Outcome);
    }

    [Fact]
    public async Task Join_unknown_code_notFound()
        => Assert.Equal(JoinOutcome.NotFound, (await _sut.JoinAsync("ZZZZZZ", Guid.NewGuid(), "X")).Outcome);

    [Fact]
    public async Task Start_only_by_host()
    {
        var dto = await _sut.CreateAsync(_host, Req(LiveQuizMode.Livre, new[] { AddChallenge("Q1") }));
        Assert.False(await _sut.StartAsync(dto.Id, Guid.NewGuid()));   // não-host
        Assert.True(await _sut.StartAsync(dto.Id, _host));             // host
        var q = await _sut.CurrentQuestionAsync(dto.Id);
        Assert.NotNull(q);
        Assert.Equal(0, q!.OrderIndex);
    }

    [Fact]
    public async Task Submit_scores_by_speed_and_ranks()
    {
        var ids = new[] { AddChallenge("Q1") };
        var dto = await _sut.CreateAsync(_host, Req(LiveQuizMode.Livre, ids));
        var ana = Guid.NewGuid(); var bia = Guid.NewGuid();
        await _sut.JoinAsync(dto.JoinCode, ana, "Ana");
        await _sut.JoinAsync(dto.JoinCode, bia, "Bia");
        await _sut.StartAsync(dto.Id, _host);

        var startedAt = (await _db.LiveQuizSession.AsNoTracking().FirstAsync(s => s.Id == dto.Id)).CurrentQuestionStartedAt!.Value;

        // Ana acerta rápido (opção 0), Bia acerta devagar.
        var ar = await _sut.SubmitAnswerAsync(dto.Id, ana, 0, 0, startedAt.AddSeconds(1));
        var br = await _sut.SubmitAnswerAsync(dto.Id, bia, 0, 0, startedAt.AddSeconds(15));
        Assert.True(ar.Accepted && ar.IsCorrect);
        Assert.True(br.Accepted && br.IsCorrect);
        Assert.True(ar.Points > br.Points);

        var board = await _sut.LeaderboardAsync(dto.Id);
        Assert.Equal(2, board.Count);
        Assert.Equal(ana, board[0].UserId);
        Assert.Equal(1, board[0].Rank);
    }

    [Fact]
    public async Task Wrong_answer_scores_zero()
    {
        var dto = await _sut.CreateAsync(_host, Req(LiveQuizMode.Livre, new[] { AddChallenge("Q1") }));
        var ana = Guid.NewGuid();
        await _sut.JoinAsync(dto.JoinCode, ana, "Ana");
        await _sut.StartAsync(dto.Id, _host);
        var startedAt = (await _db.LiveQuizSession.AsNoTracking().FirstAsync(s => s.Id == dto.Id)).CurrentQuestionStartedAt!.Value;

        var r = await _sut.SubmitAnswerAsync(dto.Id, ana, 0, 2, startedAt.AddSeconds(1)); // opção errada
        Assert.True(r.Accepted);
        Assert.False(r.IsCorrect);
        Assert.Equal(0, r.Points);
    }

    [Fact]
    public async Task Submit_is_idempotent()
    {
        var dto = await _sut.CreateAsync(_host, Req(LiveQuizMode.Livre, new[] { AddChallenge("Q1") }));
        var ana = Guid.NewGuid();
        await _sut.JoinAsync(dto.JoinCode, ana, "Ana");
        await _sut.StartAsync(dto.Id, _host);
        var startedAt = (await _db.LiveQuizSession.AsNoTracking().FirstAsync(s => s.Id == dto.Id)).CurrentQuestionStartedAt!.Value;

        var first  = await _sut.SubmitAnswerAsync(dto.Id, ana, 0, 0, startedAt.AddSeconds(1));
        var second = await _sut.SubmitAnswerAsync(dto.Id, ana, 0, 0, startedAt.AddSeconds(2));
        Assert.True(first.Accepted);
        Assert.False(second.Accepted);
        var board = await _sut.LeaderboardAsync(dto.Id);
        Assert.Equal(first.Points, board[0].Score); // não dobrou
    }

    [Fact]
    public async Task Advance_finishes_after_last()
    {
        var dto = await _sut.CreateAsync(_host, Req(LiveQuizMode.Livre, new[] { AddChallenge("Q1"), AddChallenge("Q2") }));
        await _sut.StartAsync(dto.Id, _host);                  // index 0
        Assert.Equal(1, await _sut.AdvanceAsync(dto.Id, _host)); // index 1
        Assert.Equal(-1, await _sut.AdvanceAsync(dto.Id, _host)); // encerra

        var s = await _sut.GetAsync(dto.Id);
        Assert.Equal("Finished", s!.Status);
    }

    [Fact]
    public async Task Join_after_finished_rejected()
    {
        var dto = await _sut.CreateAsync(_host, Req(LiveQuizMode.Livre, new[] { AddChallenge("Q1") }));
        await _sut.StartAsync(dto.Id, _host);
        await _sut.FinishAsync(dto.Id, _host);
        Assert.Equal(JoinOutcome.Finished, (await _sut.JoinAsync(dto.JoinCode, Guid.NewGuid(), "Z")).Outcome);
    }

    public void Dispose() => _db.Dispose();
}
