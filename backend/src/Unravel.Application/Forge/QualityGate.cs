using System.Globalization;
using System.Text;
using Unravel.Domain.Forge;

namespace Unravel.Application.Forge;

/// <summary>
/// Filtro determinístico que rejeita drafts ruins antes de servi-los.
/// Regras pensadas para erros típicos de geração baseada em template:
/// alternativas duplicadas, resposta vazia, lacuna sem informação,
/// pergunta absurdamente curta.
///
/// <para>Conservador de propósito: prefere descartar pergunta válida a
/// servir pergunta confusa. Geração é barata; confiança do usuário, não.</para>
/// </summary>
public static class QualityGate
{
    public const int MinPromptLength = 12;
    public const int MinOptions      = 3;
    public const int MaxOptions      = 6;

    /// <summary>true se o draft pode ser servido. Em caso de rejeição,
    /// <paramref name="reason"/> traz código curto pra telemetria.</summary>
    public static bool Approve(GeneratedChallengeDraft draft, out string? reason)
    {
        reason = null;

        if (draft.Prompt is null || draft.Prompt.Trim().Length < MinPromptLength)
        { reason = "prompt_too_short"; return false; }

        if (draft.Options is null || draft.Options.Count < MinOptions || draft.Options.Count > MaxOptions)
        { reason = "options_out_of_range"; return false; }

        if (draft.CorrectIndex < 0 || draft.CorrectIndex >= draft.Options.Count)
        { reason = "correct_index_out_of_range"; return false; }

        if (draft.Options.Any(o => string.IsNullOrWhiteSpace(o)))
        { reason = "empty_option"; return false; }

        // Alternativas devem ser únicas (canonicalizadas: lowercase + sem diacrítico).
        var canonical = draft.Options.Select(Canonical).ToList();
        if (canonical.Distinct().Count() != canonical.Count)
        { reason = "duplicate_options"; return false; }

        // Resposta correta não pode ser confundível com outra alternativa.
        // Distância de Levenshtein ≤ 1 (após canonicalizar) = essencialmente igual.
        var correct = canonical[draft.CorrectIndex];
        for (var i = 0; i < canonical.Count; i++)
        {
            if (i == draft.CorrectIndex) continue;
            if (Levenshtein(correct, canonical[i]) <= 1)
            { reason = "options_too_similar_to_correct"; return false; }
        }

        // Prompt não pode ser literalmente uma das alternativas (sintoma de cloze quebrado).
        var canonicalPrompt = Canonical(draft.Prompt);
        if (canonical.Any(o => canonicalPrompt.Contains(o, StringComparison.Ordinal) && o.Length >= 6
                               && o == canonical[draft.CorrectIndex]))
        { reason = "answer_leaked_in_prompt"; return false; }

        return true;
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static string Canonical(string s)
    {
        var nfd = s.Normalize(NormalizationForm.FormD);
        var sb  = new StringBuilder(nfd.Length);
        foreach (var ch in nfd)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        return sb.ToString().Trim().ToLowerInvariant();
    }

    /// <summary>Distância de edição mínima entre duas strings. O(n*m) —
    /// suficiente para alternativas de poucas dezenas de chars.</summary>
    private static int Levenshtein(string a, string b)
    {
        if (a == b) return 0;
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }
        return prev[b.Length];
    }
}
