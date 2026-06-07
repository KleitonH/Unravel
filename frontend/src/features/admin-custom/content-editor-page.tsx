import { useEffect, useState } from "react"
import { Link, useParams } from "@tanstack/react-router"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import MDEditor from "@uiw/react-md-editor"
import { ChevronLeft, Loader2, Save, Sparkles } from "lucide-react"
import { toast } from "sonner"
import { adminCustomApi } from "@/api/admin-custom"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Skeleton } from "@/components/ui/skeleton"
import { GenerateQuestionsDialog } from "./generate-questions-dialog"

/**
 * Editor de markdown pra um Content custom. Mostra preview lado-a-lado
 * (via <see cref="MDEditor"/> com preview="live"). Detecta dirty-state
 * comparando com último salvo; salva via PATCH.
 *
 * <para><b>Atenção</b>: editar o body invalida todas as perguntas
 * existentes desse content (backend marca <c>IsActive=false</c>). Aviso
 * visível antes do save. Pra repopular o pool, botão "Gerar perguntas"
 * dispara o forge.</para>
 *
 * <para>Fluxo típico: salva → gera perguntas → confere o status no painel
 * admin. Geração leva alguns minutos por content (1 job ≈ 4-8s no OpenAI).</para>
 */
export function ContentEditorPage() {
  const { contentId } = useParams({ from: "/authed/admin/contents/$contentId" })
  const id            = Number(contentId)

  // Não existe ainda endpoint GET /api/admin/contents/{id} — então buscamos
  // todas as trilhas custom do moderador e iteramos pra achar o content.
  // Volume é baixo (moderador típico tem dezenas de contents, não milhares),
  // e os dados ficam em cache compartilhado com TrailsListPage / TrailDetailPage.
  // Otimização futura quando catálogo crescer: endpoint dedicado no backend.
  const contentQuery = useQuery({
    queryKey: ["admin", "content", id],
    queryFn:  async () => {
      const trails = await adminCustomApi.listTrails()
      for (const t of trails) {
        const contents = await adminCustomApi.listContents(t.id)
        const found    = contents.find((c) => c.id === id)
        if (found) return { content: found, trailId: t.id, trailName: t.name }
      }
      throw new Error("Content not found")
    },
    enabled: !Number.isNaN(id),
  })

  return (
    <div className="p-6 lg:p-10 max-w-6xl space-y-5">
      <Button asChild variant="ghost" size="sm" className="-ml-2">
        <Link
          to={contentQuery.data?.trailId ? "/admin/trails/$trailId" : "/admin/trails"}
          params={contentQuery.data?.trailId ? { trailId: String(contentQuery.data.trailId) } : undefined}
        >
          <ChevronLeft className="h-4 w-4 mr-1" />
          {contentQuery.data?.trailName ?? "Voltar"}
        </Link>
      </Button>

      {contentQuery.isLoading && (
        <div className="space-y-4">
          <Skeleton className="h-10 w-2/3" />
          <Skeleton className="h-96" />
        </div>
      )}

      {contentQuery.error && (
        <Card className="border-destructive/40 bg-destructive/5">
          <CardContent className="pt-6 text-sm">Conteúdo não encontrado ou sem permissão.</CardContent>
        </Card>
      )}

      {contentQuery.data && (
        <EditorForm
          key={contentQuery.data.content.id}
          contentId={id}
          initialTitle={contentQuery.data.content.title}
          initialBody={contentQuery.data.content.body}
          editedAt={contentQuery.data.content.editedAt}
          createdAt={contentQuery.data.content.createdAt}
        />
      )}
    </div>
  )
}

function EditorForm({
  contentId, initialTitle, initialBody, editedAt, createdAt,
}: {
  contentId:    number
  initialTitle: string
  initialBody:  string
  editedAt:     string | null
  createdAt:    string
}) {
  const qc = useQueryClient()
  const [title, setTitle] = useState(initialTitle)
  const [body,  setBody]  = useState<string>(initialBody)

  // Reset on prop change (defensive: parent re-mounta via key={}, mas guarda)
  useEffect(() => { setTitle(initialTitle); setBody(initialBody) }, [initialTitle, initialBody])

  const titleDirty = title.trim() !== initialTitle
  const bodyDirty  = body !== initialBody
  const isDirty    = titleDirty || bodyDirty

  const saveMutation = useMutation({
    mutationFn: () => adminCustomApi.updateContent(contentId, {
      title: titleDirty ? title.trim() : undefined,
      body:  bodyDirty  ? body         : undefined,
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin", "content", contentId] })
      qc.invalidateQueries({ queryKey: ["admin", "trails"] })
      toast.success(
        bodyDirty
          ? "Conteúdo salvo. Pool de perguntas invalidado — clique em \"Gerar perguntas\" pra repopular."
          : "Título atualizado.",
      )
    },
    onError: () => toast.error("Falha ao salvar."),
  })

  // PR 52 — modal de geração com saldo de lã + slider de quantidade
  const [genOpen, setGenOpen] = useState(false)

  // Aviso ao sair de página com mudanças não salvas
  useEffect(() => {
    if (!isDirty) return
    const handler = (e: BeforeUnloadEvent) => {
      e.preventDefault()
      e.returnValue = ""
    }
    window.addEventListener("beforeunload", handler)
    return () => window.removeEventListener("beforeunload", handler)
  }, [isDirty])

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="pb-3">
          <div className="flex items-center justify-between gap-3 flex-wrap">
            <CardTitle className="text-lg flex items-center gap-2">
              Editar conteúdo
              {isDirty && <Badge variant="outline" className="text-[10px]">Não salvo</Badge>}
              {editedAt && !isDirty && (
                <Badge variant="outline" className="text-[10px]">
                  Editado em {new Date(editedAt).toLocaleString("pt-BR")}
                </Badge>
              )}
            </CardTitle>
            <div className="flex gap-2">
              <Button
                variant="outline"
                onClick={() => setGenOpen(true)}
                disabled={isDirty}
                title={isDirty ? "Salve antes de gerar perguntas." : ""}
              >
                <Sparkles className="h-4 w-4 mr-1" />
                Gerar perguntas
              </Button>
              <Button
                onClick={() => saveMutation.mutate()}
                disabled={!isDirty || saveMutation.isPending}
              >
                {saveMutation.isPending
                  ? <><Loader2 className="h-4 w-4 mr-1 animate-spin" />Salvando…</>
                  : <><Save className="h-4 w-4 mr-1" />Salvar</>}
              </Button>
            </div>
          </div>
        </CardHeader>
        <CardContent className="space-y-3 pt-0">
          <div className="space-y-1">
            <Label htmlFor="title" className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
              Título
            </Label>
            <Input
              id="title"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              className="text-lg font-display font-bold"
            />
          </div>

          {bodyDirty && (
            <div className="rounded-md border border-warning/40 bg-warning/5 p-3 text-xs text-foreground/80">
              <strong className="text-warning">⚠ Atenção:</strong> ao salvar com body
              alterado, todas as perguntas geradas anteriormente são marcadas como
              inativas (proteção contra perguntas desalinhadas com o conteúdo novo).
              Use <em>Gerar perguntas</em> depois pra repopular o pool.
            </div>
          )}

          <div className="space-y-1">
            <Label className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
              Markdown
            </Label>
            <div data-color-mode="dark" className="rounded-md overflow-hidden border border-border">
              <MDEditor
                value={body}
                onChange={(v) => setBody(v ?? "")}
                height={620}
                preview="live"
              />
            </div>
            <p className="text-[11px] text-muted-foreground">
              Use H2 (<code>##</code>) pra demarcar seções — o gerador de perguntas
              usa H2 como ponto de quebra natural pra extrair chunks.
              {" "}{body.length.toLocaleString("pt-BR")} chars · criado em{" "}
              {new Date(createdAt).toLocaleString("pt-BR")}.
            </p>
          </div>
        </CardContent>
      </Card>

      <GenerateQuestionsDialog
        open={genOpen}
        onClose={() => setGenOpen(false)}
        scope="content"
        scopeId={contentId}
        scopeName={title}
      />
    </div>
  )
}
