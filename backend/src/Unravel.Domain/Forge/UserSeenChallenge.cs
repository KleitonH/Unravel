namespace Unravel.Domain.Forge;

/// <summary>
/// PR 37 — registra que um usuário <i>respondeu</i> uma pergunta gerada.
/// Não conta servir-sem-responder (abandono não vira "visto") — gravado
/// apenas no fluxo de submit, garantindo que reinforcement quiz só exclui
/// perguntas em que o aluno teve oportunidade real de engajar.
///
/// <para><b>Por que existir</b>: o reinforcement quiz precisa anti-join
/// contra "perguntas já vistas pelo usuário". <c>GeneratedChallenge.ServedCount</c>
/// é agregado (não por usuário) e <c>Mastery</c> é por tópico (não por
/// pergunta). Esta tabela preenche a lacuna com índice único pra UPSERT
/// barato (defesa contra double-submit).</para>
///
/// <para><b>WasCorrect</b> redundante? Sim, em parte — pode ser derivado
/// de gamificação. Manter aqui torna queries de "quais fraquezas o user
/// já tentou e errou" diretas, sem precisar cruzar com mastery histórico.</para>
/// </summary>
public sealed class UserSeenChallenge
{
    public Guid     UserId                { get; set; }
    public int      GeneratedChallengeId  { get; set; }

    /// <summary>Quando o usuário respondeu (não quando foi servido).
    /// Permite query "mostre perguntas vistas há mais de 30 dias" pra
    /// reciclar pool quando esgotar.</summary>
    public DateTime SeenAt                { get; set; }

    /// <summary>Resultado da tentativa. Null seria "abandono" — mas hoje
    /// só gravamos no submit, então sempre tem valor. Mantido nullable
    /// pra futuro modo "registra ao servir" sem migration.</summary>
    public bool?    WasCorrect            { get; set; }

    public GeneratedChallenge? GeneratedChallenge { get; set; }
}
