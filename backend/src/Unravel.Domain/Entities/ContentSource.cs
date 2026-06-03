namespace Unravel.Domain.Entities;

/// <summary>
/// PR 35 — origem da Trail ou Content. Determina pipeline de upsert,
/// permissões de edição via API e regras de visibilidade.
///
/// <para><b>Git</b> (default): conteúdo seedado via filesystem em
/// <c>backend/knowledge/</c>. Source-of-truth no repositório; edição
/// via PR. Ignorado pelo <see cref="ContentSource.ModeratorCustom"/>
/// re-import (KnowledgeImporter pula contents com Source ≠ Git).</para>
///
/// <para><b>ModeratorCustom</b>: criado via API pela UI admin. Body é
/// gravado direto no DB. Editável via PATCH endpoints. Pode ter
/// <c>Trail.OwnerUserId</c> preenchido (autor).</para>
///
/// <para><b>Imported</b>: reservado pra integrações futuras (Coursera,
/// LMS externo, etc.). Hoje sem uso.</para>
///
/// <para><b>AiGenerated</b>: reservado pra trilhas auto-geradas a partir
/// de outras fontes. Hoje sem uso.</para>
/// </summary>
public enum ContentSource
{
    Git             = 0,
    ModeratorCustom = 1,
    Imported        = 2,
    AiGenerated     = 3,
}
