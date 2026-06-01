using Unravel.Application.Forge.Ports;
using Unravel.Application.Knowledge.Ports;

namespace Unravel.Infrastructure.Forge.Llm.Grounded.Validators;

/// <summary>
/// Rejeita perguntas onde o prompt vaza a resposta correta.
/// Vazamento = a resposta literal aparece no prompt, ou um substring
/// significativo dela (≥6 caracteres alfanuméricos consecutivos).
///
/// <para>Comparação case-insensitive e tolerante a pontuação. Pequenas
/// palavras conectivas ("é", "de", "do") são ignoradas pra não dar
/// falso positivo (frase "o componente é a base" não vaza "componente").</para>
///
/// <para>Ordem 1 — barato (string ops), antes do embedding.</para>
/// </summary>
public sealed class AnswerLeakageValidator : IQuestionValidator
{
    public int Order => 1;

    private const int MinSubstringMatch = 6;

    public (GenerationFailureReason Reason, string Detail)? Validate(
        GroundedQuestion question, ClaimCandidate _)
    {
        var answer    = question.Options[question.CorrectIndex];
        var promptLow = question.Prompt.ToLowerInvariant();
        var answerLow = answer.ToLowerInvariant();

        // Match literal exato → vazou.
        if (promptLow.Contains(answerLow))
            return (GenerationFailureReason.AnswerLeakage,
                $"Resposta literal aparece no prompt: \"{answer}\"");

        // Match de substring "significativo": pega tokens da resposta
        // com 6+ chars e checa se algum aparece no prompt. Filtra
        // stopwords/conectivos comuns pra não dar falso positivo.
        var tokens = TokenizeMeaningful(answerLow);
        foreach (var token in tokens)
        {
            if (token.Length < MinSubstringMatch) continue;
            if (promptLow.Contains(token))
                return (GenerationFailureReason.AnswerLeakage,
                    $"Token '{token}' da resposta aparece no prompt");
        }

        return null;
    }

    private static IEnumerable<string> TokenizeMeaningful(string text)
    {
        // Split por não-alfanumérico (incl. acentos), descarta stopwords.
        var pieces = new List<string>();
        var current = new System.Text.StringBuilder();
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c) || c == 'ç' || c == 'Ç')
                current.Append(c);
            else
            {
                if (current.Length > 0) { pieces.Add(current.ToString()); current.Clear(); }
            }
        }
        if (current.Length > 0) pieces.Add(current.ToString());

        return pieces.Where(t => !Stopwords.Contains(t));
    }

    /// <summary>
    /// Tokens genéricos demais no domínio TI/Angular — quando aparecem
    /// em ambos prompt e resposta, é coincidência semântica esperada,
    /// não vazamento real. Inclui variantes PT/EN porque LLMs ocasionalmente
    /// misturam (ex: "@Component" usa "Component" em inglês mesmo no
    /// PT-BR).
    /// </summary>
    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        // PT
        "componente", "componentes",
        "diretiva",   "diretivas",
        "decorator",  "decorators",
        "classe",     "classes",
        "método",     "métodos", "metodo", "metodos",
        "função",     "funções",  "funcao",  "funcoes",
        "template",   "templates",
        "sistema",    "sistemas",
        "objeto",     "objetos",
        "valor",      "valores",
        "campo",      "campos",
        "propriedade","propriedades",
        "elemento",   "elementos",
        "interface",  "interfaces",
        // EN (LLM ocasionalmente vaza)
        "component",  "components",
        "directive",  "directives",
        "service",    "services",
        "module",     "modules",
        "binding",    "bindings",
        "property",   "properties",
        // Angular API names (não são "vazamento" — são identidade)
        "angular",
    };
}
