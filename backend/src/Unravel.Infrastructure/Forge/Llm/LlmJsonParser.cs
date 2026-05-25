using System.Text.Json;
using System.Text.RegularExpressions;
using Unravel.Domain.Forge;

namespace Unravel.Infrastructure.Forge.Llm;

/// <summary>
/// Parser defensivo da saída JSON do LLM. Modelos pequenos costumam
/// "decorar" o JSON com markdown, texto antes/depois, ou usar aspas
/// curvas — extraímos o primeiro <c>{...}</c> bem-formado.
///
/// <para>Saídas inválidas (estrutura errada, opções faltando, etc) viram
/// <c>null</c>; o orquestrador descarta sem propagar erro. <c>QualityGate</c>
/// faz a validação semântica depois.</para>
/// </summary>
internal static class LlmJsonParser
{
    private static readonly Regex JsonObjectFence =
        new(@"\{[\s\S]*?\}", RegexOptions.Compiled);

    public static GeneratedChallengeDraft? TryParse(
        string raw, int sourceTopicId, int sourceContentId, double estimatedDifficulty)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // 1) Localiza o primeiro objeto JSON-like.
        var match = JsonObjectFence.Match(raw);
        if (!match.Success) return null;

        QuestionShape? shape;
        try
        {
            shape = JsonSerializer.Deserialize<QuestionShape>(
                match.Value, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });
        }
        catch (JsonException)
        {
            return null;
        }

        // 2) Validação estrutural mínima — o QualityGate aplica o resto.
        if (shape is null) return null;
        if (string.IsNullOrWhiteSpace(shape.Prompt)) return null;
        if (shape.Options is null || shape.Options.Length < 3 || shape.Options.Length > 6) return null;
        if (shape.CorrectIndex is < 0 || shape.CorrectIndex >= shape.Options.Length) return null;
        if (shape.Options.Any(string.IsNullOrWhiteSpace)) return null;

        return new GeneratedChallengeDraft(
            SourceTopicId:       sourceTopicId,
            SourceContentId:     sourceContentId,
            Strategy:            ForgeStrategy.Cloze,   // reusa enum existente; rotular como "Llm" exigiria migration
            Prompt:              shape.Prompt.Trim(),
            Options:             shape.Options.Select(o => o.Trim()).ToList(),
            CorrectIndex:        shape.CorrectIndex,
            Explanation:         shape.Explanation?.Trim(),
            EstimatedDifficulty: estimatedDifficulty);
    }

    private sealed record QuestionShape(
        string Prompt,
        string[] Options,
        int CorrectIndex,
        string? Explanation);
}
