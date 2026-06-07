namespace Unravel.Application.Forge.DTOs;

/// <summary>
/// PR 60-a — Content fatiado em capítulos (H2 do markdown). Cada
/// capítulo carrega seu próprio texto + N perguntas filtradas por
/// <c>BodyJson.sourceChunkIndex == chunkIndex</c>.
///
/// <para>Usado pelo novo fluxo "Estudo guiado" (PR 60-b) — substitui
/// o modelo "responder tudo embaralhado" como padrão pra alunos que
/// estão estudando o conteúdo pela primeira vez.</para>
///
/// <para><b>Adaptativo</b>: cada capítulo recebe entre <see cref="MinPerChapter"/>
/// e <see cref="MaxPerChapter"/> perguntas; a contagem real depende do
/// pool disponível + difficulty média do chunk (chunks difíceis pegam
/// mais; fáceis pegam o mínimo).</para>
/// </summary>
public sealed record ChapteredContentResponse(
    int                       ContentId,
    string                    ContentTitle,
    int                       TrailId,
    /// <summary>Caps efetivos (mostrados na UI pra contexto).</summary>
    int                       MinPerChapter,
    int                       MaxPerChapter,
    IReadOnlyList<ChapterDto> Chapters
);

public sealed record ChapterDto(
    int                              ChunkIndex,
    /// <summary>Título extraído do `## H2` (ou caminho `H2 > H3` quando
    /// o chunk veio de subdivisão). Vazio pra chunk de "intro" antes
    /// do primeiro H2 — UI pode usar fallback "Introdução".</summary>
    string                           Title,
    /// <summary>Markdown raw do trecho — UI renderiza com o mesmo MD viewer
    /// usado no editor.</summary>
    string                           BodyMd,
    IReadOnlyList<PoolChallengeDto>  Challenges
);

/// <summary>
/// PR 60-a — Resposta do endpoint que diz se um Content está pronto pra
/// publicação. Bloqueia publish enquanto algum capítulo tiver menos que
/// o mínimo de perguntas válidas; UI usa isso pra mostrar badges
/// "⚠ 2 capítulos sem perguntas".
/// </summary>
public sealed record PublicationReadinessResponse(
    int                                ContentId,
    bool                               Ready,
    int                                MinRequiredPerChapter,
    IReadOnlyList<ChapterReadiness>    Chapters
);

public sealed record ChapterReadiness(
    int    ChunkIndex,
    string Title,
    int    Current,
    int    Required,
    bool   Ready
);
