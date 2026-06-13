namespace Unravel.Application.Forge;

/// <summary>
/// PR 60-f — lógica pura de montagem/validação de uma pergunta escrita à
/// mão pelo moderador, antes de virar <c>GeneratedChallenge</c>
/// (Strategy=ModeratorAuthored). Separada do controller pra ser testável
/// sem WebApplicationFactory.
///
/// <para>Regras (espelham a validação de gold manual, PR 56-a):</para>
/// <list type="bullet">
///   <item>prompt e correctAnswer obrigatórios (não-vazios).</item>
///   <item>exatamente 3 distratores não-vazios.</item>
///   <item>as 4 opções (correct + distratores) distintas case-insensitive.</item>
/// </list>
///
/// <para>A posição da resposta correta é <b>determinística</b> (rotação por
/// <c>positionSeed</c>) — não aleatória — pra ser testável e pra não exigir
/// RNG. O caller passa um seed estável (ex.: tamanho do prompt) e a correta
/// não fica sempre no índice 0, evitando padrão explorável pelo aluno.</para>
/// </summary>
public static class AuthoredQuestion
{
    public sealed record Result(bool Ok, string? Error, string[] Options, int CorrectIndex)
    {
        public static Result Fail(string error) => new(false, error, Array.Empty<string>(), 0);
    }

    public static Result Build(
        string? prompt,
        string? correctAnswer,
        IReadOnlyList<string>? distractors,
        int positionSeed)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return Result.Fail("Enunciado (prompt) é obrigatório.");
        if (string.IsNullOrWhiteSpace(correctAnswer))
            return Result.Fail("Resposta correta é obrigatória.");
        if (distractors is null || distractors.Count != 3 || distractors.Any(string.IsNullOrWhiteSpace))
            return Result.Fail("Informe exatamente 3 distratores não-vazios.");

        var correct  = correctAnswer.Trim();
        var distinct = distractors.Select(d => d.Trim()).ToList();

        var all = distinct.Append(correct)
            .Select(s => s.ToLowerInvariant())
            .ToList();
        if (all.Distinct().Count() != 4)
            return Result.Fail("Resposta correta + distratores precisam ser 4 opções distintas.");

        // Monta [correct, d0, d1, d2] e rotaciona deterministicamente pra
        // a correta não cair sempre no índice 0.
        var options = new List<string> { correct, distinct[0], distinct[1], distinct[2] };
        var shift   = ((positionSeed % 4) + 4) % 4; // normaliza pra [0,3] mesmo com seed negativo
        Rotate(options, shift);
        var correctIndex = shift; // correct estava em 0; rotação à direita por `shift` o leva a `shift`

        return new Result(true, null, options.ToArray(), correctIndex);
    }

    /// <summary>Rotação à direita in-place por <paramref name="n"/> posições.</summary>
    private static void Rotate(List<string> list, int n)
    {
        if (list.Count == 0) return;
        n %= list.Count;
        if (n == 0) return;
        var tail = list.GetRange(list.Count - n, n);
        list.RemoveRange(list.Count - n, n);
        list.InsertRange(0, tail);
    }
}
