import { useState } from "react"
import { useQuery } from "@tanstack/react-query"
import { Edit, Plus, Sparkles, Star, TriangleAlert } from "lucide-react"
import { chaptersApi } from "@/api/chapters"
import { moderatorGoldApi } from "@/api/moderator-gold"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { DeleteGoldButton, GoldFormDialog } from "./gold-form-dialog"
import type { ModeratorGoldItem } from "@/types/gold"

/**
 * PR 56-a / PR 60-f — Gabarito de referência (gold) do conteúdo.
 *
 * **Atenção (PR 60-f):** as perguntas que o ALUNO responde (geradas pela IA
 * + autorais do moderador) agora são gerenciadas no
 * `<ChapterQuestionsPanel />`, organizadas por capítulo. Este card cobre só
 * o **gold**: perguntas curadas que servem de *gabarito de qualidade* pra
 * medir a IA (`forge:eval`) — elas **não vão pro quiz do aluno** nem contam
 * no readiness. Mantido pra quem quer calibrar o pipeline.
 */
export function ContentQuestionsManager({ contentId }: { contentId: number }) {
  const [formOpen, setFormOpen]   = useState(false)
  const [editingItem, setEditing] = useState<ModeratorGoldItem | null>(null)

  const goldQuery = useQuery({
    queryKey: ["admin", "gold", contentId],
    queryFn:  () => moderatorGoldApi.list(contentId),
  })

  // PR 62b — saúde da geração: recomenda gold quando o sucesso da IA cai.
  const healthQuery = useQuery({
    queryKey: ["admin", "generation-health", contentId],
    queryFn:  () => chaptersApi.generationHealth(contentId),
    refetchOnWindowFocus: true,
  })
  const health = healthQuery.data

  function openCreate() {
    setEditing(null)
    setFormOpen(true)
  }
  function openEdit(item: ModeratorGoldItem) {
    setEditing(item)
    setFormOpen(true)
  }

  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="text-base flex items-center justify-between gap-2">
          <span className="flex items-center gap-2">
            <Star className="h-4 w-4 text-warning fill-warning" />
            Gabarito de referência (mede a IA)
          </span>
          <div className="flex items-center gap-2">
            <Badge variant="outline" className="text-[10px]">
              {goldQuery.data?.length ?? 0}
            </Badge>
            <Button size="sm" variant="outline" onClick={openCreate}>
              <Plus className="h-3.5 w-3.5 mr-1" />
              Novo
            </Button>
          </div>
        </CardTitle>
      </CardHeader>
      <CardContent>
        {health?.recommendGold && (
          <div className="mb-3 rounded-md border border-warning/40 bg-warning/5 p-3 text-xs flex gap-2 items-start">
            <TriangleAlert className="h-4 w-4 text-warning shrink-0 mt-0.5" />
            <div>
              <p className="font-semibold text-warning">Recomendado: crie gold questions</p>
              <p className="text-foreground/80">
                A IA está acertando só <strong>{Math.round(health.successRate * 100)}%</strong> das
                gerações deste conteúdo ({health.done}/{health.total}), abaixo de{" "}
                {Math.round(health.threshold * 100)}%. Exemplos curados aqui ajudam a
                calibrar e melhorar a geração futura.
              </p>
            </div>
          </div>
        )}
        <p className="text-[11px] text-muted-foreground mb-3">
          Estas perguntas <strong>não aparecem pro aluno</strong> — servem só pra
          avaliar a qualidade do gerador (<code>forge:eval</code>). Pra adicionar
          perguntas que o aluno responde, use <strong>Perguntas por capítulo</strong> acima.
        </p>
        {goldQuery.isLoading && (
          <div className="space-y-2">
            {[1, 2].map((i) => <Skeleton key={i} className="h-16" />)}
          </div>
        )}
        {goldQuery.data && goldQuery.data.length === 0 && (
          <div className="text-center py-6 text-sm text-muted-foreground space-y-1">
            <Sparkles className="h-6 w-6 mx-auto opacity-40" />
            <p>Nenhum item de gabarito ainda.</p>
            <p className="text-xs">Opcional — só pra calibrar o pipeline de IA.</p>
          </div>
        )}
        {goldQuery.data && goldQuery.data.length > 0 && (
          <ul className="space-y-1.5">
            {goldQuery.data.map((g) => <GoldRow key={g.id} item={g} onEdit={() => openEdit(g)} />)}
          </ul>
        )}
      </CardContent>

      <GoldFormDialog
        open={formOpen}
        onClose={() => setFormOpen(false)}
        mode={editingItem ? "edit" : "create"}
        contentId={contentId}
        item={editingItem ?? undefined}
      />
    </Card>
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
            ⭐ gabarito
          </Badge>
          {fromGenerated && (
            <Badge variant="outline" className="text-[10px]">
              de #{item.sourceGeneratedChallengeId}
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
