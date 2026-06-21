using Microsoft.EntityFrameworkCore;
using Unravel.Domain.Entities;
using Unravel.Domain.ValueObjects;
using Unravel.Infrastructure.Notifications;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Tests.Notifications;

/// <summary>
/// PR 70 — lembrete de hábito: notifica ofensiva-em-risco (estudante com
/// streak ativo que não estudou hoje), idempotente por dia, ignora quem
/// estudou hoje / sem streak / não-aluno. EF InMemory.
/// </summary>
public class HabitReminderServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly HabitReminderService _sut;
    private readonly DateTime _now = new(2026, 6, 12, 21, 0, 0, DateTimeKind.Utc);

    public HabitReminderServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _sut = new HabitReminderService(_db);
    }

    private User AddStudent(string name, int streak, DateTime? lastActivity)
    {
        var u = User.Create(name, Email.Create($"{name}@u.test".ToLower()), "h");
        u.StreakDays = streak;
        u.LastActivityDate = lastActivity;
        _db.User.Add(u);
        _db.SaveChanges();
        return u;
    }

    [Fact]
    public async Task NotifiesActiveStreak_NotStudiedToday()
    {
        var u = AddStudent("Ana", 5, _now.AddDays(-1)); // estudou ontem
        var n = await _sut.RunAsync(_now);

        Assert.Equal(1, n);
        var notif = await _db.Notification.SingleAsync(x => x.UserId == u.Id);
        Assert.Equal(NotificationType.StreakRisk, notif.Type);
        Assert.Contains("5 dia", notif.Body);
    }

    [Fact]
    public async Task SkipsWhoStudiedToday()
    {
        AddStudent("Bia", 3, _now); // já estudou hoje
        Assert.Equal(0, await _sut.RunAsync(_now));
    }

    [Fact]
    public async Task SkipsZeroStreak()
    {
        AddStudent("Caio", 0, _now.AddDays(-1));
        Assert.Equal(0, await _sut.RunAsync(_now));
    }

    [Fact]
    public async Task IdempotentPerDay()
    {
        AddStudent("Duda", 2, _now.AddDays(-1));
        Assert.Equal(1, await _sut.RunAsync(_now));            // 1ª vez cria
        Assert.Equal(0, await _sut.RunAsync(_now.AddHours(1))); // mesmo dia → nada
        Assert.Equal(1, await _db.Notification.CountAsync());
    }

    [Fact]
    public async Task RemindsAgainNextDay()
    {
        var u = AddStudent("Edu", 4, _now.AddDays(-1));
        await _sut.RunAsync(_now);
        // continua sem estudar; no dia seguinte lembra de novo
        Assert.Equal(1, await _sut.RunAsync(_now.AddDays(1)));
        Assert.Equal(2, await _db.Notification.CountAsync(x => x.UserId == u.Id));
    }

    [Fact]
    public void NextDelay_TargetsNext21hUtc()
    {
        var morning = new DateTime(2026, 6, 12, 9, 0, 0, DateTimeKind.Utc);
        Assert.Equal(TimeSpan.FromHours(12), HabitReminderHostedService.NextDelay(morning));

        var evening = new DateTime(2026, 6, 12, 22, 0, 0, DateTimeKind.Utc);
        Assert.Equal(TimeSpan.FromHours(23), HabitReminderHostedService.NextDelay(evening)); // amanhã 21h
    }

    public void Dispose() => _db.Dispose();
}
