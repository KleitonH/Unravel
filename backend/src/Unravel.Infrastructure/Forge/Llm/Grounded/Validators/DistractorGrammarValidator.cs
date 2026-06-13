using Unravel.Application.Forge.Llm;
using Unravel.Application.Forge.Ports;
using Unravel.Application.Knowledge.Ports;

namespace Unravel.Infrastructure.Forge.Llm.Grounded.Validators;

/// <summary>
/// PR 34b — valida que distratores do FillBlank são "do mesmo tipo
/// gramatical" da resposta correta. Heurística (não-NLP completo):
///
/// <list type="bullet">
///   <item><b>Comprimento</b>: cada distrator dentro de [40%, 250%] do
///     número de caracteres da resposta. Distrator 10x maior é
///     pista visual (aluno detecta sem ler).</item>
///   <item><b>Forma léxica</b>: classifica cada opção em um shape lexical
///     (PascalCase, camelCase, snake_case, lower, kebab-case, símbolo) e
///     exige que <i>ao menos metade</i> dos distratores compartilhe a
///     forma da resposta. Não é regra dura — gold-set tem casos legítimos
///     onde 1 distrator é "outro estilo" pra confundir aluno mais avançado.</item>
///   <item><b>Word count</b>: distratores e resposta na mesma faixa
///     (1 palavra vs 1 palavra; 2-4 vs 2-4). Resposta de 1 palavra com
///     distrator de 4 é pista óbvia.</item>
/// </list>
///
/// <para><b>Short-circuit</b>: validator é no-op pra
/// <see cref="QuestionShape.MultipleChoice"/> e
/// <see cref="QuestionShape.TrueFalseGrounded"/>.</para>
///
/// <para><b>Ordem 6</b>: depois de <see cref="BlankPlacementValidator"/>
/// (precisa que o prompt esteja estrutural OK pra que options façam
/// sentido analisar).</para>
/// </summary>
public sealed class DistractorGrammarValidator : IQuestionValidator
{
    private const double MinLengthRatio = 0.40;
    private const double MaxLengthRatio = 2.50;

    public int Order => 6;

    public (GenerationFailureReason Reason, string Detail)? Validate(
        GroundedQuestion question, ClaimCandidate _)
    {
        if (question.Shape != QuestionShape.FillInTheBlank) return null;
        if (question.Options is null || question.Options.Length < 2) return null; // schema cuida

        var correct = question.Options[question.CorrectIndex] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(correct)) return null; // schema cuida

        var correctLen        = correct.Length;
        var correctWords      = CountWords(correct);
        var correctLexShape   = ClassifyLexShape(correct);

        var distractors       = question.Options
                                         .Where((_, i) => i != question.CorrectIndex)
                                         .ToList();

        // PR 34b-bis — Acronym e Other ficam isentos de TODOS os checks:
        // siglas técnicas (JIT, PHP, PSR) naturalmente têm distratores em
        // formato narrativo ("compila funções inteiras") com tamanho e
        // word count muito diferentes. Forçar simetria gera false-positives
        // em massa em conteúdo técnico avançado (visto no diagnóstico PHP JIT).
        var skipShapeChecks = correctLexShape == LexShape.Acronym
                           || correctLexShape == LexShape.Other;
        if (skipShapeChecks) return null;

        // 1) Comprimento por distrator
        foreach (var d in distractors)
        {
            if (string.IsNullOrWhiteSpace(d)) continue;
            var ratio = (double)d.Length / Math.Max(1, correctLen);
            if (ratio < MinLengthRatio || ratio > MaxLengthRatio)
                return (GenerationFailureReason.DistractorsPoor,
                    $"Distrator '{Truncate(d, 40)}' fora da faixa de tamanho da resposta " +
                    $"({d.Length} chars vs {correctLen} chars; ratio {ratio:F2})");
        }

        // 2) Word count: distratores devem ficar perto da resposta
        // Faixa permissiva: ±2 palavras OU dobro
        foreach (var d in distractors)
        {
            var dWords = CountWords(d);
            var diff   = Math.Abs(dWords - correctWords);
            var max    = Math.Max(2, correctWords);
            if (diff > max)
                return (GenerationFailureReason.DistractorsPoor,
                    $"Distrator '{Truncate(d, 40)}' com {dWords} palavras vs {correctWords} da resposta " +
                    $"(diferença {diff} > {max})");
        }

        // 3) Forma léxica: ≥ metade dos distratores compartilha shape.
        // (Skip pra Acronym/Other já feito acima.)
        var matchingShape = distractors.Count(d => ClassifyLexShape(d) == correctLexShape);
        if (matchingShape * 2 < distractors.Count)
            return (GenerationFailureReason.DistractorsPoor,
                $"Apenas {matchingShape}/{distractors.Count} distratores compartilham forma léxica " +
                $"'{correctLexShape}' da resposta '{Truncate(correct, 30)}'");

        return null;
    }

    /// <summary>Classifica string num shape lexical estável. Usado pra
    /// medir se distratores têm "tipo" parecido com a resposta —
    /// puramente sintático, não-semântico.</summary>
    internal static LexShape ClassifyLexShape(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return LexShape.Other;
        var t = s.Trim();

        // Símbolo dominante (@Component, #header, .class, etc.)
        if (!char.IsLetterOrDigit(t[0]) && t[0] != '_' && t[0] != '`')
            return LexShape.SymbolPrefixed;

        // Backtick (`const`) — código inline
        if (t.StartsWith('`') && t.EndsWith('`'))
            return LexShape.Backticked;

        // Acronym — 2-6 chars TODOS MAIÚSCULOS (JIT, PHP, PSR, API, HTTPS, JSON).
        // PR 34b-bis (diagnostico PHP JIT): conteudo tecnico avancado tem
        // muitas siglas como resposta correta; sem essa categoria caem em
        // "Other" e DistractorsPoor rejeitava tudo em massa.
        if (t.Length >= 2 && t.Length <= 6
            && t.All(c => char.IsUpper(c) || char.IsDigit(c)))
            return LexShape.Acronym;

        // snake_case (tem _ no meio, todo minúsculo)
        if (t.Contains('_') && t.All(c => char.IsLower(c) || char.IsDigit(c) || c == '_'))
            return LexShape.SnakeCase;

        // kebab-case (tem - no meio)
        if (t.Contains('-') && !t.Contains(' '))
            return LexShape.KebabCase;

        // PascalCase: começa maiúsculo, sem espaço, mistura case
        if (char.IsUpper(t[0]) && !t.Contains(' ') && t.Any(char.IsLower))
            return LexShape.PascalCase;

        // camelCase: começa minúsculo, sem espaço, tem pelo menos 1 maiúsculo
        if (char.IsLower(t[0]) && !t.Contains(' ') && t.Any(char.IsUpper))
            return LexShape.CamelCase;

        // Multi-word (tem espaço): frase
        if (t.Contains(' '))
            return LexShape.Phrase;

        // 1 palavra toda minúscula
        if (t.All(c => char.IsLower(c) || char.IsDigit(c)))
            return LexShape.LowerWord;

        return LexShape.Other;
    }

    internal enum LexShape
    {
        Other,
        SymbolPrefixed,   // @Component, #id, .class
        Backticked,       // `const`
        Acronym,          // JIT, PHP, PSR, API (PR 34b-bis)
        PascalCase,       // AppModule, MyClass
        CamelCase,        // useState, ngOnInit
        SnakeCase,        // max_pool_size
        KebabCase,        // app-root, ngFor-template
        LowerWord,        // selector, component (1 word lower)
        Phrase,           // "componente Angular" (2+ words)
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

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
