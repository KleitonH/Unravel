import { useState } from "react"
import { useQuery } from "@tanstack/react-query"
import { Bot, Edit, Plus, Sparkles, Star } from "lucide-react"
import { challengePoolApi } from "@/api/challenge-pool"
import { moderatorGoldApi } from "@/api/moderator-gold"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"
import { DeleteGoldButton, GoldFormDialog } from "./gold-form-dialog"
import type { ModeratorGoldItem } from "@/types/gold"

/**
 * PR 56-a — Gerenciador de perguntas de um Content. Duas listas
 * separadas conforme escopo aprovado:
 *
 * 1. **🤖 Geradas pela IA** (read-only neste PR) — listadas a partir
 *    do `challengePoolApi.get(contentId)`. Mostra prompt, shape, opção
 *    correta destacada.
 * 2. **⭐ Curadas por você** — CRUD completo via `moderatorGoldApi`.
 *    Botão "+ Nova pergunta" abre `<GoldFormDialog />`.
 *
 * Ações futuras (PR 56-b):
 * - Promover gerada → gold (1 clique via `sourceGeneratedChallengeId`)
 * - Desativar gerada (PATCH endpoint a criar)
 */
export function ContentQuestionsManager({ contentId }: { contentId: number }) {
  const [formOpen, setFormOpen]   = useState(false)
  const [editingItem, setEditing] = useState<ModeratorGoldItem | null>(null)

  // Geradas — pool query. Key isolada (NÃO ["challenge-pool", contentId]
  // que é a do quiz-page) pra:
  //   1. evitar consumo do cache do quiz que tem count=5
  //   2. evitar marcar challenges como "vistas" no fluxo admin
  //      (servedCount tracking acontece no pool do quiz, não aqui)
  // Refetch quando o usuário volta da aba — perguntas geradas podem
  // ter chegado em background.
  const poolQuery = useQuery({
    queryKey: ["admin", "content-pool", contentId],
    // Backend cap em 20 (validação targetCount 1..20). 20 é suficiente
    // pra moderador inspecionar amostra do pool; pra lista completa
    // futura cria endpoint admin dedicado sem cap.
    queryFn:  () => challengePoolApi.get(contentId, 20),
    refetchOnWindowFocus: true,
  })

  // Gold — query separada
  const goldQuery = useQuery({
    queryKey: ["admin", "gold", contentId],
    queryFn:  () => moderatorGoldApi.list(contentId),
  })

  function openCreate() {
    setEditing(null)
    setFormOpen(true)
  }
  function openEdit(item: ModeratorGoldItem) {
    setEditing(item)
    setFormOpen(true)
  }

  return (
    <div className="space-y-4">
      {/* Lista 1: Geradas pela IA */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base flex items-center justify-between gap-2">
            <span className="flex items-center gap-2">
              <Bot className="h-4 w-4 text-success" />
              Geradas pela IA
            </span>
            <Badge variant="outline" className="text-[10px]">
              {poolQuery.data?.challenges.length ?? 0}
            </Badge>
          </CardTitle>
        </CardHeader>
        <CardContent>
          {poolQuery.isLoading && (
            <div className="space-y-2">
              {[1, 2, 3].map((i) => <Skeleton key={i} className="h-14" />)}
            </div>
          )}
          {poolQuery.isError && (
            <p className="text-xs text-muted-foreground">
              Nenhuma pergunta gerada ainda — use <strong>Gerar perguntas</strong> acima
              pra criar o pool.
            </p>
          )}
          {poolQuery.data && poolQuery.data.challenges.length === 0 && (
            <p className="text-xs text-muted-foreground">
              Pool vazio. Dispare a geração via botão acima.
            </p>
          )}
          {poolQuery.data && poolQuery.data.challenges.length > 0 && (
            <ul className="space-y-1.5">
              {poolQuery.data.challenges.map((c) => (
                <li
                  key={c.id}
                  className="rounded-md border border-border bg-popover/40 px-3 py-2"
                >
                  <div className="flex items-start justify-between gap-2 mb-1">
                    <div className="flex items-center gap-1.5 text-[10px] text-muted-foreground">
                      <Badge variant="outline" className={cn(
                        "text-[10px]",
                        c.shape === "FillInTheBlank" && "border-primary/40 text-primary",
                      )}>
                        {c.shape === "FillInTheBlank" ? "🧩 fill-blank" : "📋 mcq"}
                      </Badge>
                      <span>dif {Math.round(c.estimatedDifficulty * 100)}%</span>
                    </div>
                    <span className="text-[10px] text-muted-foreground font-mono">#{c.id}</span>
                  </div>
                  <p className="text-sm line-clamp-2">{c.prompt}</p>
                  <p className="text-[11px] text-success mt-1">
                    ✓ {c.options[c.correctIndex]}
                  </p>
                </li>
              ))}
            </ul>
          )}
          <p className="text-[11px] text-muted-foreground mt-3">
            Edição direta de perguntas geradas chega no PR 56-b — por
            enquanto promova pra "Curadas" se quiser ajustar.
          </p>
        </CardContent>
      </Card>

      {/* Lista 2: Gold do moderador */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base flex items-center justify-between gap-2">
            <span className="flex items-center gap-2">
              <Star className="h-4 w-4 text-warning fill-warning" />
              Curadas por você
            </span>
            <div className="flex items-center gap-2">
              <Badge variant="outline" className="text-[10px]">
                {goldQuery.data?.length ?? 0}
              </Badge>
              <Button size="sm" onClick={openCreate}>
                <Plus className="h-3.5 w-3.5 mr-1" />
                Nova
              </Button>
            </div>
          </CardTitle>
        </CardHeader>
        <CardContent>
          {goldQuery.isLoading && (
            <div className="space-y-2">
              {[1, 2].map((i) => <Skeleton key={i} className="h-16" />)}
            </div>
          )}
          {goldQuery.data && goldQuery.data.length === 0 && (
            <div className="text-center py-6 text-sm text-muted-foreground space-y-1">
              <Sparkles className="h-6 w-6 mx-auto opacity-40" />
              <p>Nenhuma pergunta curada ainda.</p>
              <p className="text-xs">
                Escreva suas próprias perguntas pra suplementar as geradas.
              </p>
            </div>
          )}
          {goldQuery.data && goldQuery.data.length > 0 && (
            <ul className="space-y-1.5">
              {goldQuery.data.map((g) => <GoldRow key={g.id} item={g} onEdit={() => openEdit(g)} />)}
            </ul>
          )}
        </CardContent>
      </Card>

      <GoldFormDialog
        open={formOpen}
        onClose={() => setFormOpen(false)}
        mode={editingItem ? "edit" : "create"}
        contentId={contentId}
        item={editingItem ?? undefined}
      />
    </div>
  )
}

function GoldRow({ item, onEdit }: { item: ModeratorGoldItem; onEdit: () => void }) {
  let distractors: string[] = []
  try { distractors = JSON.parse(item.distractorsJson) } catch { /* json corrompido */ }

  const fromGenerated = item.sourceGeneratedChallengeId != null

  return (
    <li className="rounded-md border border-warning/30 bg-warning/5 px-3 py-2">
      <div className="flex items-start justify-between gap-2 mb-1">
        <div className="flex items-center gap-1.5 text-[10px] text-muted-foreground">
          <Badge variant="outline" className="text-[10px] border-warning/40 text-warning">
            ⭐ gold
          </Badge>
          {fromGenerated && (
            <Badge variant="outline" className="text-[10px]">
              promovida de #{item.sourceGeneratedChallengeId}
            </Badge>
          )}
        </div>
        <div className="flex items-center gap-1">
          <Button size="icon" variant="ghost" className="h-7 w-7" onClick={onEdit}>
            <Edit className="h-3.5 w-3.5" />
          </Button>
          <DeleteGoldButton goldId={item.id} />
        </div>
      </div>
      <p className="text-sm font-medium">{item.prompt}</p>
      <p className="text-[11px] text-success mt-1">✓ {item.correctAnswer}</p>
      {distractors.length === 3 && (
        <p className="text-[10px] text-muted-foreground mt-0.5 line-clamp-1">
          ✗ {distractors.join(" · ")}
        </p>
      )}
      {item.explanation && (
        <p className="text-[10px] text-muted-foreground italic mt-1 line-clamp-1">
          {item.explanation}
        </p>
      )}
    </li>
  )
}
