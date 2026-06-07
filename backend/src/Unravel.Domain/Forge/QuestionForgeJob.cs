namespace Unravel.Domain.Forge;

/// <summary>
/// Job persistido na fila de geração de perguntas LLM-grounded (PR 32).
/// Cada job representa "gerar 1 pergunta a partir do par (chunk, claim)
/// desse Content". Worker pega 1 por vez (GPU-bound) e popula
/// <see cref="GeneratedChallenge"/> em caso de sucesso.
///
/// <para><b>Persistência</b>: sobrevive a restart da API. Se o worker
/// morrer no meio de um job, o próximo startup re-claim jobs com
/// status=Running há mais de N minutos (resume).</para>
///
/// <para><b>Idempotência</b>: o enfileirador pode chamar várias vezes
/// pro mesmo Content; deduplica por (ContentId, ChunkIndex, ClaimHash).
/// Pra re-gerar deliberadamente, marca jobs antigos como Done e
/// enfileira novos.</para>
/// </summary>
public class QuestionForgeJob
{
    public long Id           { get; set; }
    public int  ContentId    { get; set; }
    public int  ChunkIndex   { get; set; }

    /// <summary>Texto bruto do claim — gravado pra reprodução exata
    /// mesmo se o conteúdo-fonte mudar entre enqueue e processing.</summary>
    public string ClaimText  { get; set; } = string.Empty;

    /// <summary>Hash do claim (SHA-256 truncado em hex 16 chars).
    /// Usado pra dedup junto com (ContentId, ChunkIndex). Permite
    /// re-importar o conteúdo sem duplicar jobs se as claims continuam
    /// iguais.</summary>
    public string ClaimHash  { get; set; } = string.Empty;

    public ForgeJobStatus   Status   { get; set; } = ForgeJobStatus.Pending;
    public ForgeJobPriority Priority { get; set; } = ForgeJobPriority.Normal;

    public int      AttemptCount { get; set; }
    public string?  LastError    { get; set; }

    public DateTime EnqueuedAt   { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt   { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>ID do GeneratedChallenge criado quando sucesso.
    /// Permite rastrear job → pergunta resultante.</summary>
    public int? GeneratedChallengeId { get; set; }

    /// <summary>
    /// PR 52a — agrupa jobs disparados na mesma chamada de enqueue
    /// (POST forge/{id} ou POST forge/bulk). Permite ao moderador ver
    /// "os jobs DELE" sem misturar com fila global de outros admins ou
    /// do cron noturno legado.
    ///
    /// <para>Opcional pra retrocompat: jobs antigos (pré-52a) ficam null
    /// e aparecem agrupados em "Sem batch" no admin panel. Não há
    /// migration de backfill — histórico fica como está.</para>
    /// </summary>
    public Guid? BatchId { get; set; }

    /// <summary>
    /// PR 52a — userId do moderador que disparou o batch. Usado pra
    /// filtrar batches "meus" no painel admin sem expor batches de
    /// outros admins. Null pra cron noturno / sistema.
    /// </summary>
    public Guid? EnqueuedByUserId { get; set; }
}

public enum ForgeJobStatus
{
    Pending = 0,
    Running = 1,
    Done    = 2,
    Failed  = 3,
}

public enum ForgeJobPriority
{
    Normal = 0,
    /// <summary>Pool baixo demais — corta fila normal.</summary>
    Urgent = 1,
}
