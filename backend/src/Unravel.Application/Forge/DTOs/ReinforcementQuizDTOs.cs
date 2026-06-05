namespace Unravel.Application.Forge.DTOs;

/// <summary>
/// PR 37 — payload do "Treinar fraquezas". Reusa <see cref="PoolChallengeDto"/>
/// (mesmo formato que o quiz normal serve) e adiciona contexto sobre <i>por que</i>
/// estas perguntas foram escolhidas — pra UI mostrar "essas perguntas são focadas
/// em DI e Lifecycle, seus tópicos mais fracos".
///
/// <para><b>moreComing</b>: pool insuficiente disparou jobs urgent no
/// <see cref="Unravel.Application.Forge.Ports.IQuestionForgeQueue"/>. UI pode
/// mostrar "geramos mais X perguntas em background — refresh em ~1min".</para>
///
/// <para>Pode retornar <see cref="Challenges"/> vazio com <b>reason</b> populado:</para>
/// <list type="bullet">
///   <item><c>"no_weaknesses"</c> — user não tem masteries abaixo do threshold.
///   UI exibe "parabéns, sem fraquezas detectadas hoje".</item>
///   <item><c>"pool_exhausted"</c> — fraquezas existem mas user já viu todas
///   as perguntas disponíveis. Jobs foram enfileirados; volte em alguns minutos.</item>
/// </list>
/// </summary>
public sealed record ReinforcementQuizResponse(
    int                              TrailId,
    IReadOnlyList<WeakTopicDto>      WeakTopics,
    IReadOnlyList<PoolChallengeDto>  Challenges,
    bool                             MoreComing,
    int                              JobsEnqueued,
    string?                          Reason           // null em sucesso normal
);

/// <summary>Um tópico onde o aluno está fraco. Mastery é a versão "fresca"
/// (com decaimento) no momento da request. <c>QuestionsAvailable</c> = pool
/// fresco real disponível pro user, já descontando "vistos".</summary>
public sealed record WeakTopicDto(
    int     TopicId,
    string  TopicSlug,
    double  EffectiveMastery,
    int     QuestionsAvailable
);
