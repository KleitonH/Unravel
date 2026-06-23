using Microsoft.EntityFrameworkCore;
using Unravel.Application.LiveQuiz.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.Forge;
using Unravel.Infrastructure.LiveQuiz;
using Unravel.Infrastructure.Notifications;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Tests.LiveQuiz;

/// <summary>
/// UC30 — relatório pedagógico da turma. Conduz uma sessão real (criar →
/// entrar → responder) via LiveQuizService e valida a agregação do relatório:
/// por questão, por participante, por tópico, resumo, e a autorização (só o
/// host lê). EF InMemory.
/// </summary>
public class LiveQuizReportServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly LiveQuizService _quiz;
    private readonly LiveQuizReportService _sut;
    private readonly Guid _host = Guid.NewGuid();
    private readonly DateTime _t0 = new(2026, 6, 22, 10, 0, 0, DateTimeKind.Utc);

    public LiveQuizReportServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _quiz = new LiveQuizService(_db, new NotificationService(_db));
        _sut = new LiveQuizReportService(_db);
    }

    private int AddContent(string title)
    {
        var c = new Content { Title = title, Body = "x" };
        _db.Content.Add(c);
        _db.SaveChanges();
        return c.Id;
    }

    /// <summary>Pergunta no pool com correctIndex 0 ("A"), ligada a um conteúdo.</summary>
    private int AddChallenge(string prompt, int contentId)
    {
        var body = """{"options":["A","B","C","D"],"correctIndex":0,"explanation":"pq","shape":"MultipleChoice"}""";
        var g = new GeneratedChallenge
        {
            ContentId = contentId, TopicId = 1, TrailId = 1,
            Strategy = ForgeStrategy.ModeratorAuthored,
            Prompt = prompt, BodyJson = body, EstimatedDifficulty = 0.5, IsActive = true,
        };
        _db.GeneratedChallenge.Add(g);
        _db.SaveChanges();
        return g.Id;
    }

    private CreateLiveQuizRequest Req(IReadOnlyList<int> qids) =>
        new(LiveQuizMode.Livre, SecondsPerQuestion: 20, ShowRankBetween: true,
            ShuffleQuestions: false, ShuffleOptions: false,
            QuestionChallengeIds: qids, AllowedUserIds: Array.Empty<Guid>());

    /// <summary>
    /// Sessão: 2 perguntas (conteúdos "PHP" e "SQL"), Ana acerta as duas, Bia
    /// erra a segunda. Conduz tudo via o serviço real.
    /// </summary>
    private async Task<int> SeedFinishedSessionAsync(Guid ana, Guid bia)
    {
        var php = AddContent("PHP");
        var sql = AddContent("SQL");
        var ids = new[] { AddChallenge("Q1", php), AddChallenge("Q2", sql) };
        var dto = await _quiz.CreateAsync(_host, Req(ids));

        await _quiz.JoinAsync(dto.JoinCode, ana, "Ana");
        await _quiz.JoinAsync(dto.JoinCode, bia, "Bia");

        await _quiz.StartAsync(dto.Id, _host);                       // pergunta 0
        await _quiz.SubmitAnswerAsync(dto.Id, ana, 0, 0, _t0.AddSeconds(2));  // certo (rápido)
        await _quiz.SubmitAnswerAsync(dto.Id, bia, 0, 0, _t0.AddSeconds(8));  // certo (lento)

        await _quiz.AdvanceAsync(dto.Id, _host);                     // pergunta 1
        await _quiz.SubmitAnswerAsync(dto.Id, ana, 1, 0, _t0.AddSeconds(3));  // certo
        await _quiz.SubmitAnswerAsync(dto.Id, bia, 1, 2, _t0.AddSeconds(4));  // errado

        await _quiz.FinishAsync(dto.Id, _host);
        return dto.Id;
    }

    [Fact]
    public async Task Report_aggregates_per_question_participant_topic()
    {
        var ana = Guid.NewGuid(); var bia = Guid.NewGuid();
        var sessionId = await SeedFinishedSessionAsync(ana, bia);

        var report = await _sut.GetReportAsync(_host, sessionId);
        Assert.NotNull(report);

        // Resumo: 4 respostas, 3 corretas → 75%.
        Assert.Equal(2, report!.Summary.ParticipantCount);
        Assert.Equal(2, report.Summary.QuestionCount);
        Assert.Equal(4, report.Summary.AnswersCount);
        Assert.Equal(0.75, report.Summary.OverallAccuracy, 3);

        // Por questão: Q1 100% (2/2), Q2 50% (1/2).
        var q0 = report.Questions.Single(q => q.OrderIndex == 0);
        Assert.Equal(2, q0.CorrectCount);
        Assert.Equal(1.0, q0.Accuracy, 3);
        Assert.Equal(2, q0.OptionDistribution[0]); // ambos marcaram "A"

        var q1 = report.Questions.Single(q => q.OrderIndex == 1);
        Assert.Equal(1, q1.CorrectCount);
        Assert.Equal(0.5, q1.Accuracy, 3);
        Assert.Equal(1, q1.OptionDistribution[0]); // Ana em "A"
        Assert.Equal(1, q1.OptionDistribution[2]); // Bia em "C"

        // Por participante: Ana acertou 2/2; Bia 1/2. Ana lidera (mais rápida + sem erro).
        var anaRow = report.Participants.Single(p => p.UserId == ana);
        var biaRow = report.Participants.Single(p => p.UserId == bia);
        Assert.Equal(2, anaRow.CorrectCount);
        Assert.Equal(1.0, anaRow.Accuracy, 3);
        Assert.Equal(1, biaRow.CorrectCount);
        Assert.Equal(0.5, biaRow.Accuracy, 3);
        Assert.Equal(1, anaRow.Rank);

        // Por tópico: PHP 100%, SQL 50%; lacuna (SQL) vem primeiro.
        Assert.Equal(2, report.Topics.Count);
        Assert.Equal("SQL", report.Topics[0].Topic);
        Assert.Equal(0.5, report.Topics[0].Accuracy, 3);
        var php = report.Topics.Single(t => t.Topic == "PHP");
        Assert.Equal(1.0, php.Accuracy, 3);
    }

    [Fact]
    public async Task Report_is_host_only()
    {
        var sessionId = await SeedFinishedSessionAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.Null(await _sut.GetReportAsync(Guid.NewGuid(), sessionId)); // outro professor
    }

    [Fact]
    public async Task ListHostSessions_returns_only_my_sessions()
    {
        await SeedFinishedSessionAsync(Guid.NewGuid(), Guid.NewGuid());

        var mine = await _sut.ListHostSessionsAsync(_host);
        Assert.Single(mine);
        Assert.Equal(2, mine[0].QuestionCount);
        Assert.Equal(2, mine[0].ParticipantCount);

        Assert.Empty(await _sut.ListHostSessionsAsync(Guid.NewGuid()));
    }

    public void Dispose() => _db.Dispose();
}
