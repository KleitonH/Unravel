import { useState } from "react"
import { Link, useParams } from "@tanstack/react-router"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { ChevronLeft, Edit, FileText, Loader2, MoreHorizontal, Plus, Sparkles, Trash2 } from "lucide-react"
import { toast } from "sonner"
import { adminCustomApi } from "@/api/admin-custom"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog"
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from "@/components/ui/dropdown-menu"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Skeleton } from "@/components/ui/skeleton"
import { GenerateQuestionsDialog } from "./generate-questions-dialog"
import type { CreateCustomContentRequest, CustomContentDto } from "@/types/admin-custom"

/**
 * Página de uma trilha custom — lista contents + botão de criar novo
 * + ações por content (editar, gerar perguntas, remover).
 *
 * <para>Lista de trilhas precarregada via cache do TanStack (mesma query
 * key da TrailsListPage); se moderador deep-link direto, faz um fetch
 * leve só pra resolver nome.</para>
 */
export function TrailDetailPage() {
  const { trailId } = useParams({ from: "/authed/admin/trails/$trailId" })
  const id          = Number(trailId)

  const trailsQuery = useQuery({
    queryKey: ["admin", "trails"],
    queryFn:  adminCustomApi.listTrails,
  })
  const trail = trailsQuery.data?.find((t) => t.id === id)

  const contentsQuery = useQuery({
    queryKey: ["admin", "trails", id, "contents"],
    queryFn:  () => adminCustomApi.listContents(id),
    enabled:  !Number.isNaN(id),
  })

  // PR 52 — modal de geração em bulk pra trilha inteira
  const [bulkGenOpen, setBulkGenOpen] = useState(false)

  return (
    <div className="p-6 lg:p-10 max-w-5xl space-y-5">
      <Button asChild variant="ghost" size="sm" className="-ml-2">
        <Link to="/admin/trails">
          <ChevronLeft className="h-4 w-4 mr-1" />
          Voltar
        </Link>
      </Button>

      <header className="flex items-start justify-between gap-4 flex-wrap">
        <div className="min-w-0">
          <h1 className="text-3xl font-display font-extrabold tracking-tight truncate">
            {trail?.icon} {trail?.name ?? <Skeleton className="inline-block h-8 w-48 align-middle" />}
          </h1>
          <p className="text-muted-foreground mt-1">{trail?.description}</p>
          <div className="mt-2 flex flex-wrap gap-2">
            {trail && (
              <>
                <Badge variant={trail.isPublished ? "default" : "outline"} className="text-[10px]">
                  {trail.isPublished ? "Publicada" : "Rascunho"}
                </Badge>
                <Badge variant="outline" className="text-[10px]">
                  {contentsQuery.data?.length ?? 0} conteúdos
                </Badge>
              </>
            )}
          </div>
        </div>
        <div className="flex gap-2 flex-wrap">
          {/* PR 52 — botão de bulk forge da trilha inteira */}
          {(contentsQuery.data?.length ?? 0) > 0 && (
            <Button
              variant="outline"
              onClick={() => setBulkGenOpen(true)}
            >
              <Sparkles className="h-4 w-4 mr-1" />
              Gerar bulk
            </Button>
          )}
          <NewContentDialog trailId={id} />
        </div>
      </header>

      {trail?.slug && (
        <GenerateQuestionsDialog
          open={bulkGenOpen}
          onClose={() => setBulkGenOpen(false)}
          scope="trail"
          scopeId={trail.slug}
          scopeName={trail.name}
          contentsCount={contentsQuery.data?.length ?? 0}
        />
      )}

      {contentsQuery.isLoading && (
        <div className="space-y-2">
          {[1, 2, 3].map((i) => <Skeleton key={i} className="h-20" />)}
        </div>
      )}

      {contentsQuery.data?.length === 0 && (
        <Card>
          <CardContent className="pt-10 pb-10 text-center space-y-2">
            <p className="text-muted-foreground">
              Nenhum conteúdo nessa trilha ainda.
            </p>
            <p className="text-sm text-muted-foreground">
              Crie um conteúdo, escreva o markdown e depois gere perguntas
              automaticamente com o forge.
            </p>
          </CardContent>
        </Card>
      )}

      {contentsQuery.data && contentsQuery.data.length > 0 && (
        <div className="space-y-2">
          {contentsQuery.data.map((c) => <ContentRow key={c.id} content={c} />)}
        </div>
      )}
    </div>
  )
}

function ContentRow({ content }: { content: CustomContentDto }) {
  const qc = useQueryClient()

  const forgeMutation = useMutation({
    mutationFn: () => adminCustomApi.enqueueForge(content.id, 20),
    onSuccess: (res) => {
      toast.success(
        `${res.enqueued} jobs enfileirados pra "${res.contentTitle}". ` +
        `Confira o status do forge no painel admin.`,
      )
    },
    onError: () => toast.error("Falha ao enfileirar forge."),
  })

  const deleteMutation = useMutation({
    mutationFn: () => adminCustomApi.deleteContent(content.id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin", "trails"] })  // refresh contentsCount
      qc.invalidateQueries({ queryKey: ["admin"] })
      toast.success("Conteúdo removido.")
    },
    onError: () => toast.error("Falha ao remover."),
  })

  return (
    <Card className="overflow-hidden">
      <CardHeader className="flex flex-row items-center gap-3 space-y-0 py-3">
        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-md bg-primary/10 text-primary">
          <FileText className="h-5 w-5" />
        </div>
        <div className="min-w-0 flex-1">
          <CardTitle className="text-base truncate">
            #{content.order} · {content.title}
            {!content.isActive && (
              <Badge variant="destructive" className="ml-2 text-[10px]">Removido</Badge>
            )}
          </CardTitle>
          <p className="text-xs text-muted-foreground">
            {content.body.length} chars
            {" · "}slug: <code>{content.slug}</code>
            {content.editedAt && <span> · editado {fmtAgo(content.editedAt)}</span>}
          </p>
        </div>
        <Button asChild size="sm" variant="outline">
          <Link to="/admin/contents/$contentId" params={{ contentId: String(content.id) }}>
            <Edit className="h-4 w-4 mr-1" />
            Editar
          </Link>
        </Button>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button size="icon" variant="ghost" disabled={!content.isActive}>
              <MoreHorizontal className="h-4 w-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem
              onClick={() => forgeMutation.mutate()}
              disabled={forgeMutation.isPending}
            >
              {forgeMutation.isPending
                ? <Loader2 className="h-4 w-4 animate-spin" />
                : <Sparkles className="h-4 w-4" />}
              Gerar perguntas
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem
              destructive
              onClick={() => {
                if (confirm(`Remover "${content.title}"?`))
                  deleteMutation.mutate()
              }}
              disabled={deleteMutation.isPending}
            >
              <Trash2 className="h-4 w-4" />
              Remover
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </CardHeader>
    </Card>
  )
}

// ── Dialog: novo content ───────────────────────────────────────────

function NewContentDialog({ trailId }: { trailId: number }) {
  const [open, setOpen] = useState(false)
  const [form, setForm] = useState<CreateCustomContentRequest>({
    title: "",
    body:  "",
    level: "Beginner",
  })
  const qc = useQueryClient()

  const mutation = useMutation({
    mutationFn: () => adminCustomApi.createContent(trailId, {
      title: form.title.trim(),
      body:  form.body || `# ${form.title.trim()}\n\nEscreva o conteúdo aqui.`,
      level: form.level,
    }),
    onSuccess: (created) => {
      qc.invalidateQueries({ queryKey: ["admin", "trails"] })
      qc.invalidateQueries({ queryKey: ["admin", "trails", trailId, "contents"] })
      toast.success(`Conteúdo "${created.title}" criado. Abra pra escrever o markdown.`)
      setOpen(false)
      setForm({ title: "", body: "", level: "Beginner" })
    },
    onError: (err: unknown) => {
      const status = (err as { response?: { status?: number; data?: { message?: string } } })?.response
      if (status?.status === 409) toast.error(status.data?.message ?? "Slug em uso.")
      else                        toast.error("Falha ao criar conteúdo.")
    },
  })

  const canSubmit = form.title.trim().length >= 3 && !mutation.isPending

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button>
          <Plus className="h-4 w-4 mr-1" />
          Novo conteúdo
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Criar conteúdo</DialogTitle>
          <DialogDescription>
            Você poderá escrever o markdown completo no editor após criar.
            Aqui só nome e nível.
          </DialogDescription>
        </DialogHeader>

        <form
          onSubmit={(e) => { e.preventDefault(); if (canSubmit) mutation.mutate() }}
          className="space-y-3"
        >
          <div className="space-y-1">
            <Label htmlFor="content-title" className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
              Título *
            </Label>
            <Input
              id="content-title"
              value={form.title}
              onChange={(e) => setForm({ ...form, title: e.target.value })}
              placeholder="Ex: Modelagem Relacional"
              autoFocus
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="content-level" className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
              Nível
            </Label>
            <select
              id="content-level"
              value={form.level}
              onChange={(e) =>
                setForm({ ...form, level: e.target.value as CreateCustomContentRequest["level"] })
              }
              className="flex h-10 w-full rounded-md border border-input bg-popover px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              <option value="Beginner">Iniciante</option>
              <option value="Intermediate">Intermediário</option>
              <option value="Advanced">Avançado</option>
            </select>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setOpen(false)}>
              Cancelar
            </Button>
            <Button type="submit" disabled={!canSubmit}>
              {mutation.isPending
                ? <><Loader2 className="h-4 w-4 mr-1 animate-spin" />Criando…</>
                : "Criar"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function fmtAgo(iso: string): string {
  const d   = new Date(iso)
  const ms  = Date.now() - d.getTime()
  const min = Math.floor(ms / 60_000)
  if (min < 1)        return "agora"
  if (min < 60)       return `há ${min}min`
  const hr = Math.floor(min / 60)
  if (hr < 24)        return `há ${hr}h`
  const day = Math.floor(hr / 24)
  return `há ${day}d`
}
