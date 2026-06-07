using System.Text.RegularExpressions;
using Unravel.Application.Forge.Llm;
using Unravel.Application.Forge.Ports;
using Unravel.Application.Knowledge.Ports;

namespace Unravel.Infrastructure.Forge.Llm.Grounded;

/// <summary>
/// Heurística determinística que escolhe <see cref="QuestionShape"/>
/// olhando features do <see cref="ClaimCandidate"/>. Implementa
/// <see cref="IClaimShapeRouter"/>.
///
/// <para><b>Decisão (em ordem)</b>:</para>
/// <list type="number">
///   <item>Claim muito curto (≤6 palavras) ou muito longo (&gt;28) →
///     <see cref="QuestionShape.MultipleChoice"/>. Fill-blank precisa de
///     sentença declarativa de tamanho confortável pra ler com lacuna.</item>
///   <item>Sem termo técnico identificável (capitalizado, snake_case,
///     CamelCase, ou entre crases) → <see cref="QuestionShape.MultipleChoice"/>.
///     Sem termo-chave, não tem o que esconder.</item>
///   <item>Caso contrário → <see cref="QuestionShape.FillInTheBlank"/>.</item>
/// </list>
///
/// <para><b>Por que não usar TrueFalseGrounded ainda</b>: reservado pra
/// PR 34a-bis; gerá-lo requer o LLM produzir UMA mutação plausível mas
/// falsa do claim, o que tem yield baixo nos primeiros experimentos
/// (modelo tende a copiar literal e marcar falso, ou inventar mutação
/// óbvia demais). Mantemos o enum estável agora pra não precisar de
/// migration depois.</para>
///
/// <para><b>Calibração</b>: limites (≤6, &gt;28) vieram da distribuição
/// de palavras dos 200 claims do gold set (PR 33d): mediana 14, p10=7,
/// p90=27. Ajustar quando o gold set crescer.</para>
/// </summary>
public sealed class ClaimShapeRouter : IClaimShapeRouter
{
    private const int MinWordsForFillBlank = 7;
    private const int MaxWordsForFillBlank = 28;

    /// <summary>Termos "técnicos" candidatos a esconder: capitalizado
    /// (Component), camelCase/PascalCase (useState, AppModule),
    /// snake_case (max_pool_size), ou entre crases (`const`).
    /// Stop-words capitalizadas (\"O\", \"A\", \"De\") são filtradas
    /// pelo tamanho mínimo de 3 chars.</summary>
    private static readonly Regex TechnicalTermPattern =
        new(@"`[^`]+`|\b[A-Z][a-zA-Z0-9]{2,}\b|\b[a-z]+[A-Z][a-zA-Z0-9]*\b|\b[a-z]+_[a-z_]+\b",
            RegexOptions.Compiled);

    public ShapeDecision Route(ClaimCandidate claim)
    {
        if (claim is null) throw new ArgumentNullException(nameof(claim));

        var text = claim.ClaimText ?? string.Empty;
        var wordCount = CountWords(text);

        if (wordCount < MinWordsForFillBlank)
            return new ShapeDecision(QuestionShape.MultipleChoice, "claim_too_short");

        if (wordCount > MaxWordsForFillBlank)
            return new ShapeDecision(QuestionShape.MultipleChoice, "claim_too_long");

        if (!HasTechnicalTerm(text))
            return new ShapeDecision(QuestionShape.MultipleChoice, "no_technical_term");

        return new ShapeDecision(QuestionShape.FillInTheBlank, "good_shape_match");
    }

    private static int CountWords(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        var count = 0;
        var inWord = false;
        foreach (var c in s)
        {
            if (char.IsWhiteSpace(c)) { inWord = false; continue; }
            if (!inWord) { count++; inWord = true; }
        }
        return count;
    }

    private static bool HasTechnicalTerm(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;

        // Primeira palavra capitalizada é convenção gramatical ("O", "Esta",
        // "Quando"), não termo técnico. Pula o primeiro token alfabético
        // antes de aplicar a regex. Termos entre crases (`x`) ainda contam
        // mesmo no início — são marcação explícita do autor.
        var firstWordEnd = 0;
        while (firstWordEnd < s.Length && !char.IsLetterOrDigit(s[firstWordEnd]) && s[firstWordEnd] != '`')
            firstWordEnd++;
        // Se começa por crase, deixa a regex achar.
        if (firstWordEnd < s.Length && s[firstWordEnd] != '`')
        {
            while (firstWordEnd < s.Length && char.IsLetterOrDigit(s[firstWordEnd]))
                firstWordEnd++;
        }
        var afterFirstWord = firstWordEnd < s.Length ? s[firstWordEnd..] : string.Empty;

        return TechnicalTermPattern.IsMatch(afterFirstWord);
    }
}
