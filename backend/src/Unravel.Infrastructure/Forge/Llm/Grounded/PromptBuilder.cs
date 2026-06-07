using Unravel.Application.Forge.Llm;
using Unravel.Application.Knowledge.Ports;
using Unravel.Infrastructure.Forge.Llm.Grounded.Prompts;

namespace Unravel.Infrastructure.Forge.Llm.Grounded;

/// <summary>
/// Dispatcher de prompts shape-aware (PR 34a). Encaminha a montagem do
/// prompt pra <see cref="MultipleChoicePrompt"/> ou
/// <see cref="FillBlankPrompt"/> conforme o <see cref="QuestionShape"/>
/// escolhido pelo <see cref="Application.Forge.Ports.IClaimShapeRouter"/>.
///
/// <para>Antes do PR 34a esse arquivo continha o prompt MCQ inline. A
/// refatoração extraiu cada prompt pra arquivo dedicado em
/// <c>Prompts/</c> e transformou esse builder em dispatcher fino —
/// adicionar shape novo é só estender o switch.</para>
///
/// <para><b>Determinismo</b>: o builder não introduz randomicidade.
/// Mesmo <c>(shape, contentTitle, claim)</c> → mesmo prompt.</para>
/// </summary>
internal static class PromptBuilder
{
    public static string Build(QuestionShape shape, string contentTitle, ClaimCandidate claim) =>
        shape switch
        {
            QuestionShape.MultipleChoice    => MultipleChoicePrompt.Build(contentTitle, claim),
            QuestionShape.FillInTheBlank    => FillBlankPrompt.Build(contentTitle, claim),
            QuestionShape.TrueFalseGrounded =>
                // Reservado pra PR 34a-bis. Caso o router decida emitir
                // antes do prompt estar pronto, devolvemos o MCQ canônico
                // como fallback seguro — yield conhecido, sem regressão.
                MultipleChoicePrompt.Build(contentTitle, claim),
            _ => MultipleChoicePrompt.Build(contentTitle, claim),
        };
}
