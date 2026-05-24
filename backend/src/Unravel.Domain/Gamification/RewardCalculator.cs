namespace Unravel.Domain.Gamification;

/// <summary>
/// Calcula recompensas e penalidades de uma submissão de quiz, sem efeito
/// colateral. Inputs: dificuldade estimada da pergunta (0..1) e se a
/// resposta foi correta. Outputs: XP/Coins/Stars ganhos + delta de vidas.
///
/// <para><b>Fórmulas</b> calibradas a partir de <c>Sistema_Recompensas.docx</c>
/// e do <see cref="Entities.Challenge"/> existente (que usa baseXp=150,
/// baseCoins=10 para perguntas curadas). GeneratedChallenge é menor em
/// peso porque é gerado on-demand — manter proporção ~1/3:</para>
/// <list type="bullet">
///   <item><b>XP</b> = 50 + 100·difficulty  (50..150, só se acertou)</item>
///   <item><b>Coins</b> = round(5·(1 + difficulty))  (5..10, só se acertou)</item>
///   <item><b>Stars</b> = 1 se acertou pergunta com difficulty ≥ 0.5 (estrela
///   só pra perguntas com substância — evita farm com cloze trivial)</item>
///   <item><b>LifeDelta</b> = -1 se errou, 0 se acertou  (regra do doc Ofensiva:
///   1 erro = 1 vida)</item>
/// </list>
///
/// <para>Funções puras → testáveis sem mocks, replayables com mesmo input.</para>
/// </summary>
public static class RewardCalculator
{
    public const int BaseXp     = 50;
    public const int MaxXpBonus = 100;   // 50 + 100·1.0 = 150
    public const int BaseCoins  = 5;
    public const double StarsDifficultyThreshold = 0.5;

    public static SubmissionRewards Compute(double difficulty, bool correct)
    {
        if (difficulty is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "difficulty must be in [0,1]");

        if (!correct)
            return new SubmissionRewards(Xp: 0, Coins: 0, Stars: 0, LifeDelta: -1);

        var xp    = (int)Math.Round(BaseXp + MaxXpBonus * difficulty);
        var coins = (int)Math.Round(BaseCoins * (1 + difficulty));
        var stars = difficulty >= StarsDifficultyThreshold ? 1 : 0;

        return new SubmissionRewards(xp, coins, stars, LifeDelta: 0);
    }
}

public sealed record SubmissionRewards(int Xp, int Coins, int Stars, int LifeDelta);
