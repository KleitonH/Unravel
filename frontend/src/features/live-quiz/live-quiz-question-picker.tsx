import { useMemo, useState } from "react"
import { useQuery } from "@tanstack/react-query"
import { Bot, Check, ChevronLeft, PenLine, X } from "lucide-react"
import { adminCustomApi } from "@/api/admin-custom"
import { trailsApi } from "@/api/trails"
import { contentsApi } from "@/api/contents"
import { chaptersApi } from "@/api/chapters"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"
import type { ContentQuestion } from "@/types/chapters"

type Tab = "meus" | "plataforma"

export type PickedQuestion = {
  id:           number
  prompt:       string
  authored:     boolean
  contentTitle: string
}

/**
 * Quiz ao Vivo — ETAPA 2: seleção manual das perguntas.
 *
 * Abas "Meus conteúdos" / "Plataforma" → escolhe trilha → conteúdo → vê as
 * perguntas autorais + as da IA e marca. As escolhidas ficam no painel da
 * direita (cesta), respeitando a quantidade configurada.
 */
export function LiveQuizQuestionPicker({
  target, onBack, onConfirm, initial,
}: {
  target: number
  onBack: () => void
  onConfirm: (picked: PickedQuestion[]) => void
  initial: PickedQuestion[]
}) {
  const [tab, setTab] = useState<Tab>("meus")
  const [trailId, setTrailId] = useState<number | null>(null)
  const [contentId, setContentId] = useState<number | null>(null)
  const [contentTitle, setContentTitle] = useState("")
  const [picked, setPicked] = useState<Map<number, PickedQuestion>>(
    () => new Map(initial.map((p) => [p.id, p])),
  )

  // Trilhas: minhas (custom) vs plataforma (todas, menos as minhas).
  const ownedQuery = useQuery({ queryKey: ["admin", "trails"], queryFn: adminCustomApi.listTrails })
  const allQuery   = useQuery({ queryKey: ["trails"], queryFn: trailsApi.list, enabled: tab === "plataforma" })

  const ownedIds = useMemo(() => new Set((ownedQuery.data ?? []).map((t) => t.id)), [ownedQuery.data])
  const trails = useMemo(() => {
    if (tab === "meus") return (ownedQuery.data ?? []).map((t) => ({ id: t.id, name: t.name, icon: t.icon }))
    return (allQuery.data ?? []).filter((t) => !ownedIds.has(t.id)).map((t) => ({ id: t.id, name: t.name, icon: t.icon }))
  }, [tab, ownedQuery.data, allQuery.data, ownedIds])

  const contentsQuery = useQuery({
    queryKey: ["contents", "by-trail", trailId],
    queryFn:  () => contentsApi.byTrail(trailId!),
    enabled:  trailId != null,
  })

  const questionsQuery = useQuery({
    queryKey: ["admin", "content-questions", contentId],
    queryFn:  () => chaptersApi.listQuestions(contentId!),
    enabled:  contentId != null,
  })

  function switchTab(t: Tab) {
    setTab(t); setTrailId(null); setContentId(null); setContentTitle("")
  }

  function toggle(q: ContentQuestion) {
    setPicked((prev) => {
      const next = new Map(prev)
      if (next.has(q.id)) next.delete(q.id)
      else if (next.size < target) next.set(q.id, { id: q.id, prompt: q.prompt, authored: q.authored, contentTitle })
      return next
    })
  }

  const pickedList = [...picked.values()]
  const full = picked.size >= target

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-3">
        <Button variant="ghost" size="sm" className="-ml-2" onClick={onBack}>
          <ChevronLeft className="h-4 w-4 mr-1" />Configuração
        </Button>
        <Badge variant="outline" className={cn("text-sm", full ? "border-success/50 text-success" : "")}>
          {picked.size} / {target} selecionadas
        </Badge>
      </div>

      <div className="grid gap-4 lg:grid-cols-[1fr_320px]">
        {/* Coluna de navegação + perguntas */}
        <div className="space-y-3 min-w-0">
          {/* Abas */}
          <div className="flex gap-1 border-b border-border">
            <TabBtn active={tab === "meus"} onClick={() => switchTab("meus")} label="Meus conteúdos" />
            <TabBtn active={tab === "plataforma"} onClick={() => switchTab("plataforma")} label="Plataforma" />
          </div>

          {/* Trilha + conteúdo (selects) */}
          <div className="grid gap-2 sm:grid-cols-2">
            <LabeledSelect
              label="Trilha"
              value={trailId ?? ""}
              onChange={(v) => { setTrailId(v ? Number(v) : null); setContentId(null); setContentTitle("") }}
              loading={tab === "meus" ? ownedQuery.isLoading : allQuery.isLoading}
              options={trails.map((t) => ({ value: t.id, label: `${t.icon ?? "📘"} ${t.name}` }))}
              placeholder="Escolha uma trilha…"
            />
            <LabeledSelect
              label="Conteúdo"
              value={contentId ?? ""}
              onChange={(v) => {
                const c = (contentsQuery.data ?? []).find((x) => x.id === Number(v))
                setContentId(v ? Number(v) : null); setContentTitle(c?.title ?? "")
              }}
              loading={contentsQuery.isLoading}
              disabled={trailId == null}
              options={(contentsQuery.data ?? []).map((c) => ({ value: c.id, label: c.title }))}
              placeholder={trailId == null ? "Escolha a trilha primeiro" : "Escolha um conteúdo…"}
            />
          </div>

          {/* Perguntas do conteúdo */}
          <div className="space-y-2">
            {contentId == null && (
              <Card><CardContent className="py-8 text-center text-sm text-muted-foreground">
                Selecione uma trilha e um conteúdo pra ver as perguntas.
              </CardContent></Card>
            )}
            {contentId != null && questionsQuery.isLoading && <Skeleton className="h-40 w-full" />}
            {contentId != null && !questionsQuery.isLoading && (questionsQuery.data?.length ?? 0) === 0 && (
              <Card><CardContent className="py-8 text-center text-sm text-muted-foreground">
                Esse conteúdo ainda não tem perguntas. Gere/registre na aba Trilhas.
              </CardContent></Card>
            )}
            {questionsQuery.data?.map((q) => {
              const on = picked.has(q.id)
              const blocked = !on && full
              return (
                <button key={q.id} type="button" onClick={() => toggle(q)} disabled={blocked}
                  className={cn(
                    "w-full text-left rounded-md border p-3 transition-colors",
                    on ? "border-primary bg-primary/10" : "border-border hover:border-primary/50",
                    blocked && "opacity-50 cursor-not-allowed",
                  )}>
                  <div className="flex items-start gap-2">
                    <span className={cn("mt-0.5 flex h-4 w-4 shrink-0 items-center justify-center rounded border",
                      on ? "bg-primary border-primary text-primary-foreground" : "border-muted-foreground/40")}>
                      {on && <Check className="h-3 w-3" />}
                    </span>
                    <div className="min-w-0 flex-1">
                      <p className="text-sm">{q.prompt}</p>
                      <div className="flex gap-1.5 mt-1.5">
                        <Badge variant="outline" className={cn("text-[10px] gap-1", q.authored ? "border-primary/40 text-primary" : "border-accent/40 text-accent")}>
                          {q.authored ? <><PenLine className="h-3 w-3" />Autoral</> : <><Bot className="h-3 w-3" />IA</>}
                        </Badge>
                        <Badge variant="outline" className="text-[10px]">Dif. {Math.round(q.estimatedDifficulty * 100)}%</Badge>
                      </div>
                    </div>
                  </div>
                </button>
              )
            })}
          </div>
        </div>

        {/* Cesta de selecionadas */}
        <aside className="space-y-2 lg:sticky lg:top-16 self-start">
          <div className="flex items-center justify-between">
            <p className="text-xs font-display font-bold uppercase tracking-wider text-muted-foreground">
              Selecionadas
            </p>
            {pickedList.length > 0 && (
              <button className="text-xs text-muted-foreground hover:text-destructive" onClick={() => setPicked(new Map())}>
                limpar
              </button>
            )}
          </div>
          <div className="rounded-md border border-border divide-y divide-border/60 max-h-[420px] overflow-y-auto">
            {pickedList.length === 0 && (
              <p className="text-sm text-muted-foreground text-center py-8 px-3">
                Nenhuma pergunta ainda. Marque perguntas à esquerda.
              </p>
            )}
            {pickedList.map((p, i) => (
              <div key={p.id} className="flex items-start gap-2 px-2.5 py-2">
                <span className="text-xs text-muted-foreground tabular-nums mt-0.5 w-5 shrink-0">{i + 1}.</span>
                <div className="min-w-0 flex-1">
                  <p className="text-xs line-clamp-2">{p.prompt}</p>
                  <p className="text-[10px] text-muted-foreground truncate">{p.contentTitle}</p>
                </div>
                <button onClick={() => setPicked((prev) => { const n = new Map(prev); n.delete(p.id); return n })}
                  className="text-muted-foreground hover:text-destructive shrink-0" title="Remover">
                  <X className="h-3.5 w-3.5" />
                </button>
              </div>
            ))}
          </div>
          <Button className="w-full" disabled={picked.size === 0} onClick={() => onConfirm(pickedList)}>
            {picked.size < target ? `Concluir com ${picked.size} (de ${target})` : "Concluir seleção"}
          </Button>
          {picked.size > 0 && picked.size < target && (
            <p className="text-[11px] text-muted-foreground text-center">
              Você pode iniciar com menos que {target} perguntas.
            </p>
          )}
        </aside>
      </div>
    </div>
  )
}

function TabBtn({ active, onClick, label }: { active: boolean; onClick: () => void; label: string }) {
  return (
    <button type="button" onClick={onClick}
      className={cn(
        "px-4 py-2 text-sm font-medium border-b-2 -mb-px transition-colors",
        active ? "border-primary text-primary" : "border-transparent text-muted-foreground hover:text-foreground",
      )}>
      {label}
    </button>
  )
}

function LabeledSelect({
  label, value, onChange, options, placeholder, loading, disabled,
}: {
  label: string
  value: number | ""
  onChange: (v: string) => void
  options: { value: number; label: string }[]
  placeholder: string
  loading?: boolean
  disabled?: boolean
}) {
  return (
    <label className="block">
      <span className="text-xs font-medium text-muted-foreground">{label}</span>
      <select
        value={value}
        onChange={(e) => onChange(e.target.value)}
        disabled={disabled || loading}
        className={cn(
          "mt-1 w-full rounded-md border border-border bg-background px-2.5 py-2 text-sm",
          "focus:outline-none focus:ring-2 focus:ring-primary/40 disabled:opacity-50",
        )}>
        <option value="">{loading ? "Carregando…" : placeholder}</option>
        {options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
      </select>
    </label>
  )
}
