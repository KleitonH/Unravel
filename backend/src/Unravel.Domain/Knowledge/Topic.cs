namespace Unravel.Domain.Knowledge;

/// <summary>
/// Representação semântica derivada de um <see cref="Entities.Content"/>.
/// Não persistida diretamente: é reconstruída pelo <c>GraphBuilder</c> sempre
/// que o cache do <c>KnowledgeGraph</c> da trilha é invalidado. Identifica o que
/// o conteúdo cobre (keywords), quão denso/avançado é (difficultyScore) e
/// permite ao planner inferir pré-requisitos por sobreposição lexical.
/// </summary>
public sealed class Topic
{
    public int    Id              { get; }
    public int    ContentId       { get; }
    public int    TrailId         { get; }
    public string Slug            { get; }
    public IReadOnlyList<Keyword> Keywords { get; }

    /// <summary>0..1 — combinação de densidade lexical, novidade, presença
    /// de tokens técnicos e nível declarado do Content. Determinístico.</summary>
    public double DifficultyScore { get; }

    /// <summary>Posição original do Content na trilha; serve de tie-breaker
    /// no ranking de pré-requisitos quando a similaridade é simétrica.</summary>
    public int OriginalOrder { get; }

    public Topic(int id, int contentId, int trailId, string slug,
                 IReadOnlyList<Keyword> keywords, double difficultyScore, int originalOrder)
    {
        Id              = id;
        ContentId       = contentId;
        TrailId         = trailId;
        Slug            = slug;
        Keywords        = keywords;
        DifficultyScore = difficultyScore;
        OriginalOrder   = originalOrder;
    }
}

/// <summary>Par (termo, peso) extraído por algum <c>IKeywordExtractor</c>.
/// O termo já vem normalizado (lowercase + stem, conforme o extractor).</summary>
public readonly record struct Keyword(string Term, double Score);
