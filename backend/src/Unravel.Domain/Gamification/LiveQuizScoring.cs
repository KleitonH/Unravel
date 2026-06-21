namespace Unravel.Domain.Gamification;

/// <summary>
/// Pontuação do Quiz ao Vivo (estilo Kahoot): errou = 0; acertou = base +
/// bônus de velocidade. Função pura → testável e replayável.
///
/// <para>Acerto vale entre <see cref="Base"/> (resposta lenta, no limite) e
/// <see cref="Base"/>+<see cref="SpeedBonus"/> (resposta instantânea). O bônus
/// decai linearmente com o tempo gasto sobre o limite da pergunta.</para>
/// </summary>
public static class LiveQuizScoring
{
    public const int Base       = 500;  // piso de um acerto
    public const int SpeedBonus = 500;  // bônus máximo por velocidade

    /// <param name="correct">Se a resposta foi correta.</param>
    /// <param name="msToAnswer">Tempo de resposta em ms (≥ 0).</param>
    /// <param name="limitSeconds">Tempo-limite da pergunta em segundos (&gt; 0).</param>
    public static int Points(bool correct, int msToAnswer, int limitSeconds)
    {
        if (!correct) return 0;
        if (limitSeconds <= 0) return Base + SpeedBonus;

        var limitMs = limitSeconds * 1000.0;
        var frac    = Math.Clamp(1.0 - Math.Max(0, msToAnswer) / limitMs, 0.0, 1.0);
        return Base + (int)Math.Round(SpeedBonus * frac);
    }
}
