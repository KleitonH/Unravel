namespace Unravel.Application.Forge.BossFight;

/// <summary>
/// PR 50 — algoritmo puro de seleção combinatorial para o Boss Fight
/// (desafio final da trilha cruzando múltiplos topics).
///
/// <para><b>Greedy heurístico em 3 fases</b>:</para>
/// <list type="number">
///   <item><b>Cobertura</b>: pra cada topic da trilha, escolhe 1 pergunta
///   priorizando: não-vista pelo user > menos servida > id asc.</item>
///   <item><b>Balanceamento de dificuldade</b>: se ainda faltam slots,
///   preenche buckets que ainda não bateram a quota
///   (easy 30% / medium 40% / hard 30%).</item>
///   <item><b>Top-up</b>: completa pelo critério de "menos servida"
///   sem restrição de topic/difficulty.</item>
/// </list>
///
/// <para><b>Filtros aplicados em todas as fases</b>:</para>
/// <list type="bullet">
///   <item><c>IsActive</c> (assumido pelo caller — pool já filtrado).</item>
///   <item><c>CorrectRate ∈ [0.20, 0.85]</c> — exclui triviais e injustas
///   (PR 17 já desativa extremos, mas é defesa em profundidade).</item>
///   <item>Strategy mix: nenhuma strategy &gt; 40% do total selecionado.</item>
/// </list>
///
/// <para><b>Determinismo</b>: ordenação de empate por <c>Id</c> ascendente
/// — chamadas idênticas produzem seleção idêntica.</para>
/// </summary>
public static class BossFightSelector
{
    public const int DefaultQuestionCount = 10;

    /// <summary>Limites de dificuldade pra cada bucket.
    /// Easy &lt; 0.45 / Medium [0.45, 0.65) / Hard ≥ 0.65.</summary>
    public const double EasyMediumBoundary = 0.45;
    public const double MediumHardBoundary = 0.65;

    /// <summary>Quota de cada bucket. Soma = 1.0.</summary>
    public const double EasyShare   = 0.30;
    public const double MediumShare = 0.40;
    public const double HardShare   = 0.30;

    /// <summary>Limite superior pra single-strategy. Acima disso, a
    /// strategy "satura" e novas escolhas dela são bloqueadas
    /// (até o algoritmo ser forçado a relaxar — ver lógica em Select).</summary>
    public const double MaxStrategyShare = 0.40;

    /// <summary>Faixa aceitável de CorrectRate. Fora disso = pergunta
    /// trivial ou injusta — não vai pro Boss.</summary>
    public const double MinCorrectRate = 0.20;
    public const double MaxCorrectRate = 0.85;

    /// <summary>
    /// Seleciona <paramref name="count"/> challenges balanceados.
    /// Retorna lista vazia se não há candidatos viáveis.
    /// </summary>
    /// <param name="topicIds">Todos os topics da trilha. Usado pra cobertura.</param>
    /// <param name="candidates">Pool inteiro disponível (ativo, da trilha).</param>
    /// <param name="seenIds">Perguntas que o user já viu. Não exclui — só
    /// despriorit­iza no critério de cobertura. Boss aceita repetir se
    /// necessário pra fechar a quota.</param>
    public static IReadOnlyList<BossFightChoice> Select(
        IReadOnlyCollection<int>           topicIds,
        IReadOnlyCollection<BossCandidate> candidates,
        IReadOnlySet<int>                  seenIds,
        int                                count = DefaultQuestionCount)
    {
        if (count <= 0) return Array.Empty<BossFightChoice>();
        if (candidates.Count == 0) return Array.Empty<BossFightChoice>();

        // Filtro de qualidade baseado em CorrectRate (PR 17 já desativa
        // extremos, mas é defesa em profundidade pro Boss).
        // ServedCount=0 → sem dados ainda → aceita (não pune perguntas frescas).
        var viable = candidates
            .Where(c => c.ServedCount == 0
                     || (c.CorrectRate >= MinCorrectRate && c.CorrectRate <= MaxCorrectRate))
            .ToList();
        if (viable.Count == 0) return Array.Empty<BossFightChoice>();

        var selected      = new List<BossFightChoice>(count);
        var selectedIds   = new HashSet<int>();
        var strategyCount = new Dictionary<string, int>();
        var bucketCount   = new Dictionary<DifficultyBucket, int>
        {
            [DifficultyBucket.Easy]   = 0,
            [DifficultyBucket.Medium] = 0,
            [DifficultyBucket.Hard]   = 0,
        };

        // Quotas por bucket — round-up no Hard pra cobrir resto.
        var quotaEasy   = (int)Math.Round(count * EasyShare,   MidpointRounding.AwayFromZero);
        var quotaMedium = (int)Math.Round(count * MediumShare, MidpointRounding.AwayFromZero);
        var quotaHard   = count - quotaEasy - quotaMedium;

        var quotas = new Dictionary<DifficultyBucket, int>
        {
            [DifficultyBucket.Easy]   = quotaEasy,
            [DifficultyBucket.Medium] = quotaMedium,
            [DifficultyBucket.Hard]   = quotaHard,
        };

        var maxPerStrategy = (int)Math.Floor(count * MaxStrategyShare);

        // ── Fase 1: Cobertura (1 pergunta por topic, na ordem dos topics).
        // Cobertura tem prioridade absoluta sobre balanceamento (difficulty
        // ou strategy mix) — pra trilha onde todos topics caem no mesmo
        // bucket OU compartilham uma única strategy, a Fase 1 precisa
        // garantir representatividade. Balanceamento entra nas fases seguintes.
        foreach (var topicId in topicIds)
        {
            if (selected.Count >= count) break;
            var pick = PickBest(
                viable.Where(c => c.TopicId == topicId && !selectedIds.Contains(c.Id)),
                seenIds, strategyCount, maxPerStrategy: int.MaxValue,
                allowOverQuota: true, bucketCount, quotas);
            if (pick is not null) Add(pick);
        }

        // ── Fase 2: Balanceamento de dificuldade.
        while (selected.Count < count)
        {
            var bucketBelow = quotas
                .Where(kv => bucketCount[kv.Key] < kv.Value)
                .Select(kv => kv.Key)
                .ToList();
            if (bucketBelow.Count == 0) break;

            var pickedInRound = false;
            foreach (var bucket in bucketBelow)
            {
                var pick = PickBest(
                    viable.Where(c => Bucket(c.EstimatedDifficulty) == bucket && !selectedIds.Contains(c.Id)),
                    seenIds, strategyCount, maxPerStrategy, allowOverQuota: false, bucketCount, quotas);
                if (pick is not null)
                {
                    Add(pick);
                    pickedInRound = true;
                    if (selected.Count >= count) break;
                }
            }
            if (!pickedInRound) break;
        }

        // ── Fase 3: Top-up (relaxa quotas mas mantém strategy mix).
        while (selected.Count < count)
        {
            var pick = PickBest(
                viable.Where(c => !selectedIds.Contains(c.Id)),
                seenIds, strategyCount, maxPerStrategy, allowOverQuota: true, bucketCount, quotas);
            if (pick is null) break;
            Add(pick);
        }

        // ── Fase 4: Último recurso — relaxa strategy mix se ainda faltar.
        while (selected.Count < count)
        {
            var pick = PickBest(
                viable.Where(c => !selectedIds.Contains(c.Id)),
                seenIds, strategyCount, maxPerStrategy: int.MaxValue,
                allowOverQuota: true, bucketCount, quotas);
            if (pick is null) break;
            Add(pick);
        }

        return selected;

        // ── Helpers locais ──
        void Add(BossCandidate c)
        {
            selected.Add(new BossFightChoice(c.Id, c.TopicId, c.Strategy, c.EstimatedDifficulty));
            selectedIds.Add(c.Id);
            strategyCount[c.Strategy] = strategyCount.GetValueOrDefault(c.Strategy, 0) + 1;
            bucketCount[Bucket(c.EstimatedDifficulty)]++;
        }
    }

    /// <summary>Escolhe o melhor candidato dado os filtros e prioridades.
    /// Critério (em ordem):
    /// <list type="number">
    ///   <item>Strategy ainda &lt; <paramref name="maxPerStrategy"/>
    ///   (a menos que <paramref name="allowOverQuota"/>).</item>
    ///   <item>Bucket ainda abaixo da quota (a menos que <paramref name="allowOverQuota"/>).</item>
    ///   <item>Não-visto pelo user (despriorit­iza vistos).</item>
    ///   <item>ServedCount asc (preferir perguntas menos servidas).</item>
    ///   <item>Id asc (tie-break determinístico).</item>
    /// </list></summary>
    private static BossCandidate? PickBest(
        IEnumerable<BossCandidate>            pool,
        IReadOnlySet<int>                     seenIds,
        IReadOnlyDictionary<string, int>      strategyCount,
        int                                   maxPerStrategy,
        bool                                  allowOverQuota,
        IReadOnlyDictionary<DifficultyBucket, int> bucketCount,
        IReadOnlyDictionary<DifficultyBucket, int> quotas)
    {
        return pool
            .Where(c => strategyCount.GetValueOrDefault(c.Strategy, 0) < maxPerStrategy)
            .Where(c => allowOverQuota
                        || bucketCount[Bucket(c.EstimatedDifficulty)] < quotas[Bucket(c.EstimatedDifficulty)])
            .OrderBy(c => seenIds.Contains(c.Id) ? 1 : 0)   // não-vistos primeiro
            .ThenBy(c => c.ServedCount)
            .ThenBy(c => c.Id)
            .FirstOrDefault();
    }

    public static DifficultyBucket Bucket(double difficulty)
    {
        if (difficulty < EasyMediumBoundary) return DifficultyBucket.Easy;
        if (difficulty < MediumHardBoundary) return DifficultyBucket.Medium;
        return DifficultyBucket.Hard;
    }
}

/// <summary>Visão mínima de uma <c>GeneratedChallenge</c> que o seletor precisa.</summary>
public sealed record BossCandidate(
    int    Id,
    int    TopicId,
    string Strategy,
    double EstimatedDifficulty,
    double CorrectRate,
    int    ServedCount);

public sealed record BossFightChoice(
    int    Id,
    int    TopicId,
    string Strategy,
    double EstimatedDifficulty);

public enum DifficultyBucket
{
    Easy   = 1,
    Medium = 2,
    Hard   = 3,
}
