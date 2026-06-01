using Unravel.Application.Forge.Ports;
using Unravel.Application.Knowledge.Ports;

namespace Unravel.Infrastructure.Forge.Llm.Grounded.Validators;

/// <summary>
/// Garante invariantes estruturais da MCQ: prompt e options não-vazios,
/// 4 options únicas (sem duplicatas case-insensitive), correctIndex
/// dentro de [0,3], explanation presente.
///
/// <para>Ordem 0 — roda primeiro, é o mais barato.</para>
/// </summary>
public sealed class SchemaValidator : IQuestionValidator
{
    private const int RequiredOptions = 4;
    private const int MinPromptChars  = 15;

    public int Order => 0;

    public (GenerationFailureReason Reason, string Detail)? Validate(
        GroundedQuestion question, ClaimCandidate _)
    {
        if (string.IsNullOrWhiteSpace(question.Prompt) || question.Prompt.Length < MinPromptChars)
            return (GenerationFailureReason.SchemaInvalid,
                $"Prompt vazio ou curto demais ({question.Prompt?.Length ?? 0} chars)");

        if (question.Options is null || question.Options.Length != RequiredOptions)
            return (GenerationFailureReason.SchemaInvalid,
                $"Esperado {RequiredOptions} options, recebeu {question.Options?.Length ?? 0}");

        if (question.Options.Any(string.IsNullOrWhiteSpace))
            return (GenerationFailureReason.SchemaInvalid, "Opção vazia presente");

        // Duplicatas case-insensitive: distratores idênticos à resposta
        // ou entre si destroem a MCQ.
        var distinctCount = question.Options
            .Select(o => o.Trim().ToLowerInvariant())
            .Distinct()
            .Count();
        if (distinctCount != RequiredOptions)
            return (GenerationFailureReason.SchemaInvalid,
                $"Options têm duplicatas (distintas={distinctCount})");

        if (question.CorrectIndex < 0 || question.CorrectIndex >= RequiredOptions)
            return (GenerationFailureReason.SchemaInvalid,
                $"correctIndex fora do range: {question.CorrectIndex}");

        if (string.IsNullOrWhiteSpace(question.Explanation))
            return (GenerationFailureReason.SchemaInvalid, "Explanation vazia");

        return null;
    }
}
