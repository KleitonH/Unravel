using System.Text;
using System.Web;
using Unravel.Application.Forge.Eval;
using Unravel.Application.Forge.Ports;

namespace Unravel.Infrastructure.Forge.Eval;

/// <summary>
/// Renderiza <see cref="ForgeEvalReport"/> em HTML auto-contido (sem
/// CSS externo, sem JS) pra abrir no browser. Layout:
///
/// <list type="bullet">
///   <item>Header: trail, model, timestamp, métricas overall</item>
///   <item>Tabela por tópico (yield + cosine + jaccard)</item>
///   <item>Pares gold↔gen lado-a-lado com code-color por bucket de falha</item>
/// </list>
///
/// <para>Identidade visual segue Unravel (dark purple + accent verde),
/// embora seja ferramenta dev-internal — só pra você não ter choque
/// trocando entre o app e o report.</para>
/// </summary>
public static class HtmlReportRenderer
{
    public static string Render(ForgeEvalReport report)
    {
        var sb = new StringBuilder(50_000);
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"pt-BR\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.AppendLine($"<title>Forge Eval — {Esc(report.Trail)} — {report.RunAt:yyyy-MM-dd HH:mm}</title>");
        AppendStyles(sb);
        sb.AppendLine("</head><body>");

        AppendHeader(sb, report);
        AppendOverallMetrics(sb, report.Overall);
        AppendByTopic(sb, report.ByTopic);
        AppendPairs(sb, report.Pairs);

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static void AppendStyles(StringBuilder sb)
    {
        sb.AppendLine("""
        <style>
          :root {
            --bg:       #0e0a1e;
            --card:     #181230;
            --popover:  #1f1839;
            --border:   #2a2444;
            --text:     #f6f4ff;
            --muted:    #a59fc8;
            --primary:  #a78bfa;
            --accent:   #38db8c;
            --warning:  #facc15;
            --danger:   #f97373;
          }
          * { box-sizing: border-box }
          body {
            background: var(--bg); color: var(--text);
            font: 14px/1.5 -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
            margin: 0; padding: 32px;
          }
          h1, h2, h3 { color: var(--text); margin-top: 0 }
          h1 { font-size: 28px; margin-bottom: 8px }
          h2 { font-size: 20px; margin-top: 40px; padding-top: 16px;
               border-top: 1px solid var(--border) }
          a { color: var(--primary) }
          .muted { color: var(--muted); font-size: 13px }
          .grid { display: grid; gap: 12px; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)) }
          .card {
            background: var(--card); border: 1px solid var(--border);
            border-radius: 8px; padding: 14px 16px;
          }
          .stat { display: flex; flex-direction: column }
          .stat .v { font-size: 24px; font-weight: 700; color: var(--primary) }
          .stat .l { font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; color: var(--muted) }
          .stat.good .v { color: var(--accent) }
          .stat.warn .v { color: var(--warning) }
          .stat.bad .v { color: var(--danger) }
          table { width: 100%; border-collapse: collapse; margin: 12px 0 }
          th, td { padding: 8px 10px; border-bottom: 1px solid var(--border); text-align: left }
          th { color: var(--muted); font-weight: 600; font-size: 12px; text-transform: uppercase; letter-spacing: 0.5px }
          tr.bad td { background: rgba(249, 115, 115, 0.05) }
          .pair {
            display: grid; gap: 16px; grid-template-columns: 1fr 1fr;
            background: var(--card); border: 1px solid var(--border);
            border-radius: 8px; padding: 16px; margin: 12px 0;
          }
          .pair.fail { border-color: var(--danger); }
          .pair > div h4 {
            margin: 0 0 8px; font-size: 13px; text-transform: uppercase;
            letter-spacing: 0.5px; color: var(--muted);
          }
          .prompt { font-weight: 600; margin-bottom: 8px }
          .opt { padding: 4px 10px; margin: 4px 0; border-radius: 6px;
                 background: var(--popover); font-size: 13px; }
          .opt.correct { background: rgba(56, 219, 140, 0.15); border-left: 3px solid var(--accent) }
          .opt.distractor { opacity: 0.75 }
          .badge {
            display: inline-block; padding: 2px 8px; border-radius: 999px;
            font-size: 11px; font-weight: 600; text-transform: uppercase;
            letter-spacing: 0.5px;
          }
          .b-ok { background: rgba(56, 219, 140, 0.2); color: var(--accent) }
          .b-fail { background: rgba(249, 115, 115, 0.2); color: var(--danger) }
          .b-warn { background: rgba(250, 204, 21, 0.15); color: var(--warning) }
          .meta { color: var(--muted); font-size: 12px; margin-top: 8px }
          .fail-reason { color: var(--danger); font-family: monospace; font-size: 12px }
          .explanation { color: var(--muted); font-style: italic; font-size: 13px; margin-top: 8px }
        </style>
        """);
    }

    private static void AppendHeader(StringBuilder sb, ForgeEvalReport r)
    {
        sb.AppendLine($"""
        <h1>Forge Eval — {Esc(r.Trail)}</h1>
        <div class="muted">Modelo: <strong>{Esc(r.ModelName)}</strong> · Rodado em {r.RunAt:yyyy-MM-dd HH:mm:ss} UTC</div>
        """);
    }

    private static void AppendOverallMetrics(StringBuilder sb, EvalMetrics m)
    {
        sb.AppendLine("<h2>Métricas Overall</h2>");
        sb.AppendLine("<div class=\"grid\">");
        Stat(sb, $"{m.YieldPercent:F0}%",
            $"yield ({m.TotalGeneratedSuccessfully}/{m.TotalGold})",
            m.YieldPercent >= 70 ? "good" : m.YieldPercent >= 50 ? "warn" : "bad");
        Stat(sb, Pct(m.AvgPromptCosine), "cosine prompt", m.AvgPromptCosine >= 0.6 ? "good" : "warn");
        Stat(sb, Pct(m.AvgAnswerCosine), "cosine answer", m.AvgAnswerCosine >= 0.6 ? "good" : "warn");
        Stat(sb, $"{m.AnswerMatchCount}", $"answer match (≥0.75)", null);
        Stat(sb, Pct(1 - m.AvgDistractorJaccard), "distractor diversity",
            m.AvgDistractorJaccard <= 0.4 ? "good" : "warn");
        sb.AppendLine("</div>");

        if (m.FailureBreakdown.Count > 0)
        {
            sb.AppendLine("<h3 style=\"margin-top:24px;font-size:15px;color:var(--muted)\">Falhas por bucket</h3>");
            sb.AppendLine("<table>");
            sb.AppendLine("<tr><th>Bucket</th><th>Contagem</th></tr>");
            foreach (var (reason, count) in m.FailureBreakdown.OrderByDescending(kv => kv.Value))
                sb.AppendLine($"<tr><td><code>{reason}</code></td><td>{count}</td></tr>");
            sb.AppendLine("</table>");
        }
    }

    private static void AppendByTopic(StringBuilder sb, IReadOnlyList<TopicAggregation> topics)
    {
        sb.AppendLine("<h2>Por tópico</h2>");
        sb.AppendLine("<table>");
        sb.AppendLine("<tr><th>Tópico</th><th>Yield</th><th>Cos prompt</th><th>Cos answer</th><th>Match</th><th>Dist Jacc</th></tr>");
        foreach (var t in topics)
        {
            var m = t.Metrics;
            var cls = m.YieldPercent >= 70 ? "" : "class=\"bad\"";
            sb.AppendLine($"""
            <tr {cls}>
              <td>{Esc(t.TopicSlug)}</td>
              <td>{m.YieldPercent:F0}% ({m.TotalGeneratedSuccessfully}/{m.TotalGold})</td>
              <td>{Pct(m.AvgPromptCosine)}</td>
              <td>{Pct(m.AvgAnswerCosine)}</td>
              <td>{m.AnswerMatchCount}/{m.TotalGeneratedSuccessfully}</td>
              <td>{Pct(m.AvgDistractorJaccard)}</td>
            </tr>
            """);
        }
        sb.AppendLine("</table>");
    }

    private static void AppendPairs(StringBuilder sb, IReadOnlyList<EvalPair> pairs)
    {
        sb.AppendLine("<h2>Pares gold ↔ generated</h2>");
        var i = 0;
        foreach (var p in pairs)
        {
            i++;
            var failClass = p.Generated is null ? "fail" : "";
            sb.AppendLine($"<div class=\"pair {failClass}\">");

            // Gold
            sb.AppendLine("<div>");
            sb.AppendLine($"<h4>#{i} · {Esc(p.TopicSlug)} · gold</h4>");
            AppendQuestionBlock(sb,
                p.Gold.Prompt, p.Gold.CorrectAnswer, p.Gold.Distractors, p.Gold.Explanation);
            sb.AppendLine($"<div class=\"meta\">claim: <em>{Esc(p.Gold.SourceClaim)}</em></div>");
            sb.AppendLine("</div>");

            // Generated
            sb.AppendLine("<div>");
            if (p.Generated is not null)
            {
                sb.AppendLine("<h4>generated <span class=\"badge b-ok\">OK</span></h4>");
                var distractors = p.Generated.Options
                    .Where((_, idx) => idx != p.Generated.CorrectIndex).ToList();
                AppendQuestionBlock(sb,
                    p.Generated.Prompt,
                    p.Generated.Options[p.Generated.CorrectIndex],
                    distractors,
                    p.Generated.Explanation);
                var matchBadge = p.AnswerMatches ? "b-ok" : "b-warn";
                var matchTxt   = p.AnswerMatches ? "MATCH" : "NO MATCH";
                sb.AppendLine($"""
                <div class="meta">
                  cosine prompt: <strong>{Pct(p.PromptCosine)}</strong> ·
                  cosine answer: <strong>{Pct(p.AnswerCosine)}</strong>
                  <span class="badge {matchBadge}">{matchTxt}</span>
                </div>
                """);
            }
            else
            {
                sb.AppendLine($"<h4>generated <span class=\"badge b-fail\">{p.GeneratedFailure}</span></h4>");
                sb.AppendLine($"<div class=\"fail-reason\">{Esc(p.GeneratedFailureDetail ?? "(sem detalhe)")}</div>");
            }
            sb.AppendLine("</div>");

            sb.AppendLine("</div>");
        }
    }

    private static void AppendQuestionBlock(StringBuilder sb, string prompt,
        string correct, IReadOnlyList<string> distractors, string? explanation)
    {
        sb.AppendLine($"<div class=\"prompt\">{Esc(prompt)}</div>");
        sb.AppendLine($"<div class=\"opt correct\">✓ {Esc(correct)}</div>");
        foreach (var d in distractors)
            sb.AppendLine($"<div class=\"opt distractor\">{Esc(d)}</div>");
        if (!string.IsNullOrWhiteSpace(explanation))
            sb.AppendLine($"<div class=\"explanation\">{Esc(explanation)}</div>");
    }

    private static void Stat(StringBuilder sb, string value, string label, string? color)
    {
        var cls = color is null ? "stat" : $"stat {color}";
        sb.AppendLine($"""
        <div class="card {cls}">
          <span class="v">{Esc(value)}</span>
          <span class="l">{Esc(label)}</span>
        </div>
        """);
    }

    private static string Pct(double v) =>
        double.IsNaN(v) ? "—" : $"{v:F2}";

    private static string Esc(string? s) =>
        s is null ? "" : HttpUtility.HtmlEncode(s);
}
