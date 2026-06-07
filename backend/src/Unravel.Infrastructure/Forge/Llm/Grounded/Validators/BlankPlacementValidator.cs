using Unravel.Application.Forge.Llm;
using Unravel.Application.Forge.Ports;
using Unravel.Application.Knowledge.Ports;

namespace Unravel.Infrastructure.Forge.Llm.Grounded.Validators;

/// <summary>
/// PR 34b — valida que a sentença fill-in-the-blank tem exatamente
/// UMA lacuna <c>_____</c>, posicionada com contexto antes E depois.
///
/// <para><b>Regras</b>:</para>
/// <list type="bullet">
///   <item>Marcador <c>_____</c> (5+ underscores) presente.</item>
///   <item>Exatamente UMA ocorrência — múltiplas lacunas viram pergunta
///     ambígua: o aluno não sabe qual termo escolhido vai onde.</item>
///   <item>≥3 palavras antes do marcador (contexto à esquerda).</item>
///   <item>≥3 palavras depois do marcador (contexto à direita).</item>
/// </list>
///
/// <para><b>Short-circuit</b>: validator é no-op pra
/// <see cref="QuestionShape.MultipleChoice"/> e
/// <see cref="QuestionShape.TrueFalseGrounded"/>. Roda só pra
/// <see cref="QuestionShape.FillInTheBlank"/>.</para>
///
/// <para><b>Ordem 5</b>: depois dos validators MCQ-universais (Schema,
/// Leakage, Grounding, Diversity) pra não bloquear short-circuit barato.</para>
/// </summary>
public sealed class BlankPlacementValidator : IQuestionValidator
{
    /// <summary>Padrão da lacuna — 5 ou mais underscores. Sincronizado
    /// com o prompt em <c>FillBlankPrompt</c>.</summary>
    private const string BlankMarker = "_____";

    /// <summary>Mínimo de palavras de contexto de cada lado da lacuna.
    /// Calibrado em 2 (não 3) porque PT-BR usa muito artigo+substantivo
    /// ("O decorator _____ marca..."); exigir 3 rejeitaria perguntas
    /// gramaticalmente saudáveis. Lacuna como primeira ou última palavra
    /// ainda fica bloqueada (0 ou 1 palavra de contexto).</summary>
    private const int MinContextWordsEachSide = 2;

    public int Order => 5;

    public (GenerationFailureReason Reason, string Detail)? Validate(
        GroundedQuestion question, ClaimCandidate _)
    {
        if (question.Shape != QuestionShape.FillInTheBlank)
            return null;   // no-op pros outros shapes

        var prompt = question.Prompt ?? string.Empty;

        // Conta ocorrências (≥5 underscores consecutivos)
        var occurrences = CountBlankOccurrences(prompt);
        if (occurrences == 0)
            return (GenerationFailureReason.SchemaInvalid,
                "FillBlank sem marcador '_____' no prompt");
        if (occurrences > 1)
            return (GenerationFailureReason.SchemaInvalid,
                $"FillBlank com {occurrences} lacunas; esperado exatamente 1");

        // Posição: ≥3 palavras antes E depois
        var blankIdx = prompt.IndexOf(BlankMarker, StringComparison.Ordinal);
        var before   = prompt[..blankIdx];
        var afterIdx = blankIdx + BlankMarker.Length;
        // Avança enquanto for underscore (caso modelo tenha gerado mais de 5)
        while (afterIdx < prompt.Length && prompt[afterIdx] == '_') afterIdx++;
        var after    = afterIdx < prompt.Length ? prompt[afterIdx..] : string.Empty;

        var wordsBefore = CountWords(before);
        var wordsAfter  = CountWords(after);

        if (wordsBefore < MinContextWordsEachSide)
            return (GenerationFailureReason.SchemaInvalid,
                $"Lacuna sem contexto suficiente à esquerda ({wordsBefore} palavras, mínimo {MinContextWordsEachSide})");

        if (wordsAfter < MinContextWordsEachSide)
            return (GenerationFailureReason.SchemaInvalid,
                $"Lacuna sem contexto suficiente à direita ({wordsAfter} palavras, mínimo {MinContextWordsEachSide})");

        return null;
    }

    private static int CountBlankOccurrences(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        var count = 0;
        var i = 0;
        while ((i = s.IndexOf(BlankMarker, i, StringComparison.Ordinal)) >= 0)
        {
            count++;
            // Pula consecutivos pra contar UM bloco de underscores como
            // uma única lacuna (modelo às vezes gera "______" com 6).
            i += BlankMarker.Length;
            while (i < s.Length && s[i] == '_') i++;
        }
        return count;
    }

    private static int CountWords(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        var count = 0;
        var inWord = false;
        foreach (var c in s)
        {
            if (char.IsLetterOrDigit(c) || c == 'ç' || c == 'Ç')
            {
                if (!inWord) { count++; inWord = true; }
            }
            else inWord = false;
        }
        return count;
    }
}
