using System.Text.RegularExpressions;
using Unravel.Domain.Entities;

namespace Unravel.Infrastructure.Knowledge;

/// <summary>
/// Calcula um <c>DifficultyScore</c> ∈ [0,1] para cada Content combinando
/// sinais objetivos do texto com o nível declarado pelo moderador. Não
/// confiamos cegamente em <see cref="DifficultyLevel"/> porque cadastros
/// reais são inconsistentes; usamos como sinal de prior (peso 0.3).
///
/// <para>Sinais usados:</para>
/// <list type="bullet">
///   <item><b>Densidade lexical</b>: palavras únicas / palavras totais.
///   Textos densos = mais conceitos por linha = mais difíceis.</item>
///   <item><b>Comprimento médio de sentença</b> (Flesch-Kincaid simplificado):
///   sentenças longas correlacionam com complexidade sintática.</item>
///   <item><b>Tokens técnicos</b>: identificadores CamelCase, snake_case,
///   blocos de código (` `` `), HTML tags. Densidade alta = avançado.</item>
///   <item><b>Nível declarado</b> (Beginner/Intermediate/Advanced).</item>
/// </list>
///
/// Determinismo: nenhuma randomização; mesma entrada → mesma saída.
/// </summary>
public sealed class DifficultyScorer
{
    private static readonly Regex Sentence    = new(@"[.!?]+", RegexOptions.Compiled);
    private static readonly Regex Word        = new(@"\p{L}+", RegexOptions.Compiled);
    private static readonly Regex CodeFence   = new(@"```[\s\S]*?```", RegexOptions.Compiled);
    private static readonly Regex TechToken   = new(@"\b([A-Z][a-z]+){2,}\b|\b[a-z]+_[a-z_]+\b|<[\w/].*?>", RegexOptions.Compiled);

    public double Score(string title, string body, DifficultyLevel declaredLevel)
    {
        var text = $"{title}\n{body}";

        // 1) prior do nível declarado
        var declared = declaredLevel switch
        {
            DifficultyLevel.Beginner     => 0.20,
            DifficultyLevel.Intermediate => 0.50,
            DifficultyLevel.Advanced     => 0.80,
            _                            => 0.40,
        };

        if (string.IsNullOrWhiteSpace(body))
            return declared; // sem corpo, só o prior

        // 2) densidade lexical
        var words = Word.Matches(text).Select(m => m.Value.ToLowerInvariant()).ToList();
        var lexicalDensity = words.Count == 0 ? 0 : (double)words.Distinct().Count() / words.Count;

        // 3) comprimento médio de sentença
        var sentences   = Sentence.Split(text).Count(s => Word.IsMatch(s));
        var meanSentLen = sentences == 0 ? 0 : (double)words.Count / sentences;
        // 8 palavras = bem fácil, 25+ = bem difícil. Clampa em [0,1].
        var lengthSignal = Math.Clamp((meanSentLen - 8) / 17.0, 0, 1);

        // 4) presença de tokens técnicos
        var codeBlocks = CodeFence.Matches(body).Count;
        var techTokens = TechToken.Matches(text).Count;
        var techSignal = Math.Clamp((techTokens + codeBlocks * 5) / 20.0, 0, 1);

        // combinação ponderada
        var score = 0.30 * declared
                  + 0.20 * lexicalDensity
                  + 0.25 * lengthSignal
                  + 0.25 * techSignal;

        return Math.Clamp(score, 0, 1);
    }
}
