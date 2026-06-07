namespace Unravel.Application.Forge.Llm;

/// <summary>
/// Formato visual/cognitivo de uma pergunta LLM-grounded. Decisão tomada
/// pelo <see cref="Ports.IClaimShapeRouter"/> com base em features do
/// <see cref="Knowledge.Ports.ClaimCandidate"/> — não pelo aluno nem pelo
/// moderador.
///
/// <para><b>Por que separar do <see cref="Domain.Forge.ForgeStrategy"/></b>:
/// strategy descreve <i>como</i> a pergunta foi gerada (template Cloze,
/// template Definition, LLM grounded). Shape descreve <i>como ela é
/// apresentada</i> ao aluno. Hoje toda pergunta servida vem de
/// <c>ForgeStrategy.LlmGrounded</c> (PR 51 matou o fallback template);
/// dentro desse universo, shape varia.</para>
///
/// <para><b>Persistência</b>: gravado como string no <c>BodyJson</c> da
/// <c>GeneratedChallenge</c> (campo <c>shape</c>). Rows antigas sem o
/// campo são tratadas como <see cref="MultipleChoice"/> — retrocompat
/// zero-migration.</para>
/// </summary>
public enum QuestionShape
{
    /// <summary>Pergunta + 4 opções textuais (padrão histórico do quiz).</summary>
    MultipleChoice    = 1,

    /// <summary>Sentença afirmativa do trecho com termo-chave substituído
    /// por <c>_____</c>; 4 opções, 1 correta. UI renderiza com
    /// <c>&lt;InlineBlank /&gt;</c> (PR 34c).</summary>
    FillInTheBlank    = 2,

    /// <summary>Afirmação grounded no trecho vs mutação plausível mas
    /// falsa; o aluno marca "verdadeiro" ou "falso". Útil pra claims com
    /// quantificadores absolutos ("sempre", "nunca"). Reservado pro PR
    /// 34a-bis — não emitido ainda pelo router.</summary>
    TrueFalseGrounded = 3,
}
