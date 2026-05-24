using System.Text.Json;
using Unravel.Application.Forge.DTOs;
using Unravel.Application.Forge.Ports;
using Unravel.Application.Journey.Ports;
using Unravel.Domain.Knowledge;

namespace Unravel.Application.Forge.UseCases;

/// <summary>
/// Submete uma resposta a uma <c>GeneratedChallenge</c> e propaga o sinal:
/// <list type="number">
///   <item>Carrega o gabarito do <c>BodyJson</c> persistido (servidor é a
///   fonte da verdade; nunca confia no cliente).</item>
///   <item>Computa <c>correct = selectedIndex == correctIndex</c>.</item>
///   <item>Atualiza <see cref="Mastery"/> do tópico via
///   <see cref="MasteryScoring.Apply"/> com outcome 1.0 ou 0.0.</item>
///   <item>Registra outcome no GeneratedChallenge (atualiza
///   <c>CorrectRate</c> + incrementa <c>ServedCount</c>).</item>
///   <item>Devolve o gabarito e a explicação para a UI mostrar.</item>
/// </list>
///
/// <para>Diferente do <see cref="GetChallengePoolUseCase"/>, o submit NÃO
/// regenera nada — só lê e atualiza. Idempotência: chamar duas vezes com
/// a mesma resposta credita o sinal duas vezes (intencional? não — mas o
/// produto pode prevenir no cliente já que o quiz desabilita ao escolher).</para>
/// </summary>
public sealed class SubmitPoolChallengeUseCase
{
    private readonly IGeneratedChallengeRepository _generated;
    private readonly IMasteryRepository            _mastery;

    public SubmitPoolChallengeUseCase(
        IGeneratedChallengeRepository generated,
        IMasteryRepository            mastery)
    {
        _generated = generated;
        _mastery   = mastery;
    }

    public async Task<SubmitPoolChallengeResponse?> ExecuteAsync(
        Guid userId, int contentId, SubmitPoolChallengeRequest request, CancellationToken ct = default)
    {
        var gc = await _generated.GetByIdAsync(request.GeneratedChallengeId, ct);
        if (gc is null) return null;
        if (gc.ContentId != contentId) return null;          // 404 — defesa contra IDs trocados

        var (correctIndex, explanation) = ParseBody(gc.BodyJson);
        if (correctIndex < 0) return null;                   // BodyJson corrompido

        var isCorrect = request.SelectedOptionIndex == correctIndex;
        var outcome   = isCorrect ? 1.0 : 0.0;
        var now       = DateTime.UtcNow;

        // Atualiza Mastery do topic dessa pergunta.
        var current = await _mastery.GetAsync(userId, gc.TopicId, ct)
                      ?? Mastery.Initial(userId, gc.TopicId, gc.TrailId, now);
        var updated = MasteryScoring.Apply(current, outcome, now);
        await _mastery.UpsertAsync(updated, ct);

        // Registra outcome no challenge gerado (média móvel + served).
        await _generated.RecordOutcomeAsync(gc.Id, isCorrect, ct);

        return new SubmitPoolChallengeResponse(
            IsCorrect:           isCorrect,
            CorrectOptionIndex:  correctIndex,
            Explanation:         explanation,
            NewMasteryScore:     Math.Round(updated.Score, 4),
            NewMasteryConfidence: updated.Confidence);
    }

    private static (int correctIndex, string? explanation) ParseBody(string bodyJson)
    {
        try
        {
            var doc = JsonDocument.Parse(bodyJson);
            var correctIndex = doc.RootElement.GetProperty("correctIndex").GetInt32();
            var explanation  = doc.RootElement.TryGetProperty("explanation", out var ex)
                               ? ex.GetString()
                               : null;
            return (correctIndex, explanation);
        }
        catch (JsonException)
        {
            return (-1, null);
        }
    }
}
