using Unravel.Infrastructure.Forge;

namespace Unravel.Tests.Forge;

/// <summary>Cobre apenas as partes puras/testáveis sem DB: NextDelay do
/// hosted service. A lógica de UPDATE em massa é cobertura de integração
/// (banco real) — fora do escopo da suíte unitária atual.</summary>
public class GeneratedChallengeMaintenanceTests
{
    [Fact]
    public void NextDelay_FromMondayBefore1am_TargetsSameDay()
    {
        // Segunda 00:30 UTC → próximo disparo: segunda 01:00 = 30 min.
        var nowUtc = new DateTime(2026, 3, 2, 0, 30, 0, DateTimeKind.Utc); // segunda
        Assert.Equal(DayOfWeek.Monday, nowUtc.DayOfWeek);

        var delay = GeneratedChallengeMaintenanceHostedService.NextDelay(nowUtc);

        Assert.Equal(TimeSpan.FromMinutes(30), delay);
    }

    [Fact]
    public void NextDelay_FromMondayAfter1am_TargetsNextWeek()
    {
        // Segunda 02:00 UTC → próximo disparo: próxima segunda 01:00.
        var nowUtc = new DateTime(2026, 3, 2, 2, 0, 0, DateTimeKind.Utc);
        var delay  = GeneratedChallengeMaintenanceHostedService.NextDelay(nowUtc);
        Assert.Equal(TimeSpan.FromDays(6) + TimeSpan.FromHours(23), delay);
    }

    [Fact]
    public void NextDelay_FromWednesday_TargetsUpcomingMonday()
    {
        // Quarta 12:00 UTC → próximo disparo: segunda 01:00 (5 dias + 13h).
        var nowUtc = new DateTime(2026, 3, 4, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(DayOfWeek.Wednesday, nowUtc.DayOfWeek);

        var delay = GeneratedChallengeMaintenanceHostedService.NextDelay(nowUtc);
        var next  = nowUtc + delay;

        Assert.Equal(DayOfWeek.Monday, next.DayOfWeek);
        Assert.Equal(1, next.Hour);
        Assert.Equal(0, next.Minute);
    }
}
