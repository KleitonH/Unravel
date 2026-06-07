using System.Text.RegularExpressions;
using Unravel.Application.Forge.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.Forge;
using Unravel.Domain.Knowledge;
using Unravel.Infrastructure.Knowledge;

namespace Unravel.Infrastructure.Forge.Strategies;

/// <summary>
/// Detecta blocos de código ` ``` ` no body com uma única chamada
/// <c>console.log(...)</c> (JS) ou <c>print(...)</c> (PY) e gera
/// "Qual a saída?". Suporta literais primitivos: string, número, boolean.
///
/// <para><b>Escopo deliberadamente estreito</b>: parser próprio para
/// expressões arbitrárias é um buraco sem fundo. Pegar literais já cobre
/// 80% dos exemplos didáticos em conteúdo introdutório, e qualquer coisa
/// fora disso retorna empty (e o Cloze/Definition fazem a pergunta).</para>
///
/// <para><b>Distratores</b>: para números, vizinhança ±1/±10; para strings,
/// versões com 1 typo controlado ou case-flip; para boolean, simplesmente
/// o complemento + variações textuais.</para>
/// </summary>
[Obsolete("PR 34e — template-based; substituido pelo pipeline LlmGrounded (PR 31+). " +
          "Nao registrado no DI por default; ativa via Forge:UseLegacyStrategies=true se necessario.")]
public sealed class CodeStrategy : IChallengeStrategy
{
    public ForgeStrategy Kind => ForgeStrategy.Code;

    private static readonly Regex CodeFence =
        new(@"```(?<lang>\w+)?\s*\n(?<body>[\s\S]*?)```",
            RegexOptions.Compiled);

    // console.log("foo") | console.log(42) | console.log(true)
    private static readonly Regex JsLog =
        new(@"console\.log\(\s*(?<arg>""[^""]*""|'[^']*'|true|false|-?\d+(?:\.\d+)?)\s*\)",
            RegexOptions.Compiled);

    // print("foo") | print(42) | print(True) — em PY True/False começam maiúsculos
    private static readonly Regex PyPrint =
        new(@"print\(\s*(?<arg>""[^""]*""|'[^']*'|True|False|-?\d+(?:\.\d+)?)\s*\)",
            RegexOptions.Compiled);

    public IReadOnlyList<GeneratedChallengeDraft> Generate(
        Content content, Topic topic, KnowledgeGraph graph, int maxDrafts)
    {
        if (string.IsNullOrWhiteSpace(content.Body)) return Array.Empty<GeneratedChallengeDraft>();

        foreach (Match fence in CodeFence.Matches(content.Body))
        {
            var lang = fence.Groups["lang"].Value.ToLowerInvariant();
            var body = fence.Groups["body"].Value;

            var draft = TryBuildFromJs(body, lang, content, topic)
                     ?? TryBuildFromPython(body, lang, content, topic);
            if (draft is not null) return new[] { draft };
        }

        return Array.Empty<GeneratedChallengeDraft>();
    }

    private static GeneratedChallengeDraft? TryBuildFromJs(
        string body, string lang, Content content, Topic topic)
    {
        if (lang is not ("" or "js" or "javascript" or "ts" or "typescript")) return null;
        var m = JsLog.Match(body);
        if (!m.Success) return null;
        return BuildDraft(content, topic, body.Trim(), m.Groups["arg"].Value, "JavaScript");
    }

    private static GeneratedChallengeDraft? TryBuildFromPython(
        string body, string lang, Content content, Topic topic)
    {
        if (lang is not ("" or "py" or "python")) return null;
        var m = PyPrint.Match(body);
        if (!m.Success) return null;
        return BuildDraft(content, topic, body.Trim(), m.Groups["arg"].Value, "Python");
    }

    private static GeneratedChallengeDraft BuildDraft(
        Content content, Topic topic, string codeBody, string rawArg, string langLabel)
    {
        var correct = NormalizeOutput(rawArg);
        var distractors = BuildDistractors(rawArg);

        var allOptions = distractors.Append(correct)
                                    .Distinct(StringComparer.Ordinal)
                                    .OrderBy(o => TextNormalizer.CanonicalKey(o), StringComparer.Ordinal)
                                    .Take(4)
                                    .ToList();

        return new GeneratedChallengeDraft(
            SourceTopicId:       topic.Id,
            SourceContentId:     content.Id,
            Strategy:            ForgeStrategy.Code,
            Prompt:              $"Qual é a saída do código {langLabel} abaixo?\n\n```\n{codeBody}\n```",
            Options:             allOptions,
            CorrectIndex:        allOptions.IndexOf(correct),
            Explanation:         $"A saída do trecho é {correct}, conforme o exemplo do material da trilha.",
            EstimatedDifficulty: Math.Clamp(topic.DifficultyScore + 0.10, 0.10, 0.95));
    }

    // ── Helpers ──────────────────────────────────────────────────────

    /// <summary>Converte o literal raw na string exibida como saída:
    /// "x" → x · 42 → 42 · true → true. Aspas removidas pra simular o
    /// que o console/stdout imprimiria.</summary>
    private static string NormalizeOutput(string raw)
    {
        if (raw.Length >= 2 && (raw[0] is '"' or '\'') && raw[^1] == raw[0])
            return raw[1..^1];
        return raw;
    }

    private static List<string> BuildDistractors(string raw)
    {
        // Boolean
        if (raw.Equals("true", StringComparison.OrdinalIgnoreCase))
            return new() { "false", "True", "0" };
        if (raw.Equals("false", StringComparison.OrdinalIgnoreCase))
            return new() { "true", "False", "1" };
        if (raw == "True")  return new() { "False", "true", "1" };
        if (raw == "False") return new() { "True",  "false", "0" };

        // Número
        if (double.TryParse(raw, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var n))
        {
            var isInt = !raw.Contains('.');
            string Fmt(double v) => isInt
                ? ((long)v).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            return new() { Fmt(n + 1), Fmt(n - 1), Fmt(n * 10) };
        }

        // String literal — gera variantes (case flip, typo controlado, append)
        var s = NormalizeOutput(raw);
        var distractors = new List<string>();
        if (!string.IsNullOrEmpty(s))
        {
            distractors.Add(s.ToUpperInvariant() == s ? s.ToLowerInvariant() : s.ToUpperInvariant());
            distractors.Add(s + "!");
            if (s.Length >= 2)
                distractors.Add(string.Create(s.Length, s, (span, src) =>
                {
                    src.CopyTo(span);
                    (span[0], span[1]) = (span[1], span[0]);
                }));
            else
                distractors.Add(s + "?");
        }
        return distractors.Distinct(StringComparer.Ordinal).ToList();
    }
}
