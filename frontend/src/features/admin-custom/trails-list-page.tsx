import { Link } from "@tanstack/react-router"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { Eye, EyeOff, FileText, Loader2, Trash2 } from "lucide-react"
import { toast } from "sonner"
import { adminCustomApi } from "@/api/admin-custom"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { NewTrailDialog } from "./trail-form-dialog"
import type { CustomTrailDto } from "@/types/admin-custom"

/**
 * Lista trilhas custom do moderador autenticado. Backend filtra por
 * <c>Source=ModeratorCustom AND OwnerUserId=current</c>; trilhas Git
 * não aparecem (gerenciadas via filesystem).
 *
 * <para>Cada card mostra status (publicada vs rascunho), nº de conteúdos
 * e atalhos pra editar/publicar/deletar.</para>
 */
export function TrailsListPage() {
  const { data, isLoading, error } = useQuery({
    queryKey: ["admin", "trails"],
    queryFn:  adminCustomApi.listTrails,
  })

  return (
    <div className="p-6 lg:p-10 max-w-5xl space-y-5">
      <header className="flex items-start justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-3xl font-display font-extrabold tracking-tight">
            🛠️ Minhas trilhas
          </h1>
          <p className="text-muted-foreground mt-1">
            Trilhas customizadas que você criou. Trilhas oficiais (Angular,
            etc.) são gerenciadas via repositório e não aparecem aqui.
          </p>
        </div>
        <NewTrailDialog />
      </header>

      {error && (
        <Card className="border-destructive/40 bg-destructive/5">
          <CardContent className="pt-6 text-sm text-destructive">
            Falha ao carregar trilhas. Recarregue a página.
          </CardContent>
        </Card>
      )}

      {isLoading && (
        <div className="grid gap-3 md:grid-cols-2">
          {[1, 2, 3, 4].map((i) => <Skeleton key={i} className="h-32" />)}
        </div>
      )}

      {!isLoading && data?.length === 0 && (
        <Card>
          <CardContent className="pt-10 pb-10 text-center space-y-2">
            <p className="text-muted-foreground">
              Nenhuma trilha custom ainda.
            </p>
            <p className="text-sm text-muted-foreground">
              Clique em <strong>Nova trilha</strong> pra começar.
            </p>
          </CardContent>
        </Card>
      )}

      {!isLoading && data && data.length > 0 && (
        <div className="grid gap-3 md:grid-cols-2">
          {data.map((trail) => <TrailCard key={trail.id} trail={trail} />)}
        </div>
      )}
    </div>
  )
}

function TrailCard({ trail }: { trail: CustomTrailDto }) {
  const qc = useQueryClient()

  const publishMutation = useMutation({
    mutationFn: () =>
      adminCustomApi.updateTrail(trail.id, { isPublished: !trail.isPublished }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin", "trails"] })
      toast.success(trail.isPublished ? "Trilha despublicada." : "Trilha publicada.")
    },
    onError: () => toast.error("Falha ao alterar publicação."),
  })

  const deleteMutation = useMutation({
    mutationFn: () => adminCustomApi.deleteTrail(trail.id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin", "trails"] })
      toast.success("Trilha removida (soft-delete).")
    },
    onError: () => toast.error("Falha ao remover."),
  })

  return (
    <Card className="overflow-hidden">
      <CardHeader className="flex flex-row items-start gap-3 space-y-0">
        <div
          className="flex h-12 w-12 shrink-0 items-center justify-center rounded-md text-2xl"
          style={{ backgroundColor: `${trail.accentColor}20`, color: trail.accentColor }}
        >
          {trail.icon}
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2 flex-wrap">
            <h3 className="font-display font-bold truncate">{trail.name}</h3>
            <Badge variant={trail.isPublished ? "default" : "outline"} className="text-[10px]">
              {trail.isPublished ? "Publicada" : "Rascunho"}
            </Badge>
            {!trail.isActive && (
              <Badge variant="destructive" className="text-[10px]">Removida</Badge>
            )}
          </div>
          <p className="text-sm text-muted-foreground truncate">
            {trail.description || <em>sem descrição</em>}
          </p>
          <p className="text-xs text-muted-foreground mt-1">
            {trail.contentsCount} {trail.contentsCount === 1 ? "conteúdo" : "conteúdos"}
            {" · "}slug: <code>{trail.slug}</code>
          </p>
        </div>
      </CardHeader>
      <CardContent className="flex flex-wrap gap-2 pt-0">
        <Button asChild size="sm">
          <Link to="/admin/trails/$trailId" params={{ trailId: String(trail.id) }}>
            <FileText className="h-4 w-4 mr-1" />
            Conteúdos
          </Link>
        </Button>
        <Button
          size="sm"
          variant="outline"
          onClick={() => publishMutation.mutate()}
          disabled={publishMutation.isPending || !trail.isActive}
        >
          {publishMutation.isPending
            ? <Loader2 className="h-4 w-4 animate-spin" />
            : trail.isPublished
              ? <><EyeOff className="h-4 w-4 mr-1" />Despublicar</>
              : <><Eye    className="h-4 w-4 mr-1" />Publicar</>}
        </Button>
        <Button
          size="sm"
          variant="ghost"
          onClick={() => {
            if (confirm(`Remover trilha "${trail.name}"? (soft-delete, pode ser recuperada via DB)`))
              deleteMutation.mutate()
          }}
          disabled={deleteMutation.isPending || !trail.isActive}
          className="ml-auto text-destructive hover:bg-destructive/10 hover:text-destructive"
        >
          <Trash2 className="h-4 w-4" />
        </Button>
      </CardContent>
    </Card>
  )
}
