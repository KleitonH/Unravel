using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Unravel.Application.Forge.Ports;

namespace Unravel.Infrastructure.Forge.Llm;

/// <summary>
/// Hosted service não-bloqueante que faz um ping no LLM no startup.
/// Loga sucesso ou warning — não impede a API de subir se o LLM
/// estiver offline (intencional: backend continua funcionando com
/// estratégias template-based; LLM é augmentação opcional).
///
/// <para>Rodado uma vez no <c>StartAsync</c>. Pra Ollama: bate em
/// <c>POST /api/generate</c> com prompt curto e timeout 30s. Pra
/// LLamaSharp: tenta gerar 1 token (cold start do modelo já confirma
/// que arquivo é válido).</para>
///
/// <para>Por que não interromper o startup quando falha: você pode
/// querer rodar a API sem GPU local (ex: VPS) mesmo com Llm:Enabled=true
/// pra ter telemetria do que <i>seria</i> gerado, ou pra testar outros
/// fluxos. O log warning é suficiente.</para>
/// </summary>
public sealed class LlmHealthCheck(
    ILlmInference llm,
    ILogger<LlmHealthCheck> log) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Não bloqueia startup — health check roda em background
        _ = Task.Run(async () =>
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelTimeout(TimeSpan.FromSeconds(30));

            try
            {
                log.LogInformation("LLM health check: pinging…");
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var response = await llm.CompleteAsync(
                    // PR 33h: prompt deve mencionar "json" pra OpenAI
                    // aceitar com response_format=json_object — exigência
                    // da API. Ollama ignora a palavra mas roda igual.
                    "Responda em JSON com a chave \"status\" e valor \"ok\".",
                    cts.Token);
                sw.Stop();

                if (string.IsNullOrWhiteSpace(response))
                {
                    log.LogWarning(
                        "LLM health check: provider retornou vazio/null após {Elapsed}ms. " +
                        "Geração de perguntas via LLM ficará indisponível até resolver.",
                        sw.ElapsedMilliseconds);
                }
                else
                {
                    log.LogInformation(
                        "LLM health check OK ({Elapsed}ms). Provider: {Type}.",
                        sw.ElapsedMilliseconds, llm.GetType().Name);
                }
            }
            catch (OperationCanceledException)
            {
                log.LogWarning(
                    "LLM health check: timeout após 30s. Daemon offline ou modelo carregando?");
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "LLM health check falhou. Inferência ficará indisponível.");
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Helper pra setar timeout em CancellationTokenSource sem
/// quebrar com instância já-cancelada (CTS.CancelAfter throws se já
/// foi disposed).</summary>
internal static class CtsExtensions
{
    public static void CancelTimeout(this CancellationTokenSource cts, TimeSpan timeout)
    {
        try { cts.CancelAfter(timeout); } catch (ObjectDisposedException) { /* ignore */ }
    }
}
