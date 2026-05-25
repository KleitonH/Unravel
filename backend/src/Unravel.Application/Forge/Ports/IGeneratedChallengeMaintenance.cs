namespace Unravel.Application.Forge.Ports;

/// <summary>
/// Operações de manutenção sobre <c>GeneratedChallenge</c> agendadas, não
/// no caminho quente do usuário. Hoje cobre apenas auto-desativação de
/// perguntas com taxa de acerto extrema; pode crescer (purga de inativos
/// antigos, recalibração de difficulty, etc).
/// </summary>
public interface IGeneratedChallengeMaintenance
{
    /// <summary>Desativa (IsActive=false) perguntas com:
    /// <list type="bullet">
    ///   <item><c>ServedCount &gt;= minServed</c> — só age quando há sinal estatístico.</item>
    ///   <item><c>CorrectRate &lt; lowerBound</c> (todo mundo erra → ambígua/quebrada) <b>ou</b></item>
    ///   <item><c>CorrectRate &gt; upperBound</c> (todo mundo acerta → trivial)</item>
    /// </list>
    /// Retorna quantas foram desativadas, separadas por categoria, para
    /// telemetria/log.</summary>
    Task<AutoDisableReport> AutoDisableExtremesAsync(
        int minServed = 20,
        double lowerBound = 0.10,
        double upperBound = 0.95,
        CancellationToken ct = default);
}

public sealed record AutoDisableReport(int DisabledTooHard, int DisabledTooEasy);
