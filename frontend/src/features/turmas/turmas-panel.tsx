import { useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { Loader2, Plus, Search, Trash2, UserPlus, Users } from "lucide-react"
import { toast } from "sonner"
import { turmasApi } from "@/api/turmas"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader } from "@/components/ui/card"
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger,
} from "@/components/ui/dialog"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"
import type { Turma } from "@/types/api"

/**
 * Painel de Turmas do professor — vive numa aba da Curadoria. Lista as
 * turmas do moderador, permite criar e gerenciar membros (buscar alunos da
 * plataforma + convidar + remover). O roster vai alimentar o Quiz ao Vivo.
 */
export function TurmasPanel() {
  const { data, isLoading, error } = useQuery({
    queryKey: ["turmas", "owned"],
    queryFn:  turmasApi.owned,
  })

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-3">
        <p className="text-sm text-muted-foreground">
          Crie turmas e convide seus alunos. Só alunos das suas turmas poderão
          entrar no <strong>Quiz ao Vivo</strong>.
        </p>
        <CreateTurmaDialog />
      </div>

      {error && (
        <Card className="border-destructive/40 bg-destructive/5">
          <CardContent className="pt-6 text-sm text-destructive">Falha ao carregar turmas.</CardContent>
        </Card>
      )}

      {isLoading && (
        <div className="grid gap-3 md:grid-cols-2">
          {[1, 2].map((i) => <Skeleton key={i} className="h-28" />)}
        </div>
      )}

      {!isLoading && data?.length === 0 && (
        <Card>
          <CardContent className="pt-10 pb-10 text-center space-y-1">
            <Users className="h-8 w-8 text-muted-foreground mx-auto" />
            <p className="text-muted-foreground">Nenhuma turma ainda.</p>
            <p className="text-sm text-muted-foreground">Clique em <strong>Nova turma</strong> pra começar.</p>
          </CardContent>
        </Card>
      )}

      {!isLoading && data && data.length > 0 && (
        <div className="grid gap-3 md:grid-cols-2">
          {data.map((t) => <TurmaCard key={t.id} turma={t} />)}
        </div>
      )}
    </div>
  )
}

function CreateTurmaDialog() {
  const qc = useQueryClient()
  const [open, setOpen] = useState(false)
  const [name, setName] = useState("")
  const [emblem, setEmblem] = useState("")
  const [description, setDescription] = useState("")

  const create = useMutation({
    mutationFn: () => turmasApi.create({ name: name.trim(), description: description.trim() || null, emblem: emblem.trim() || null }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["turmas", "owned"] })
      toast.success("Turma criada!")
      setOpen(false); setName(""); setEmblem(""); setDescription("")
    },
    onError: () => toast.error("Falha ao criar turma."),
  })

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm"><Plus className="h-4 w-4 mr-1" />Nova turma</Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Nova turma</DialogTitle>
          <DialogDescription>Depois você convida os alunos da plataforma.</DialogDescription>
        </DialogHeader>
        <div className="space-y-3">
          <div className="flex gap-2">
            <Input className="w-16 text-center" placeholder="🎓" maxLength={4} value={emblem} onChange={(e) => setEmblem(e.target.value)} />
            <Input className="flex-1" placeholder="Nome da turma (ex.: 3º A — Manhã)" value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <Textarea placeholder="Descrição (opcional)" value={description} onChange={(e) => setDescription(e.target.value)} />
        </div>
        <DialogFooter>
          <Button onClick={() => create.mutate()} disabled={create.isPending || name.trim().length < 2}>
            {create.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : "Criar"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function TurmaCard({ turma }: { turma: Turma }) {
  const qc = useQueryClient()
  const archive = useMutation({
    mutationFn: () => turmasApi.archive(turma.id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["turmas", "owned"] })
      toast.success("Turma arquivada.")
    },
    onError: () => toast.error("Falha ao arquivar."),
  })

  return (
    <Card className="overflow-hidden">
      <CardHeader className="flex flex-row items-start gap-3 space-y-0">
        <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-md bg-primary/10 text-2xl">
          {turma.emblem || "🎓"}
        </div>
        <div className="min-w-0 flex-1">
          <h3 className="font-display font-bold truncate">{turma.name}</h3>
          {turma.description && <p className="text-sm text-muted-foreground truncate">{turma.description}</p>}
          <div className="flex gap-2 mt-1.5">
            <Badge variant="outline" className="text-[10px]">{turma.memberCount} aluno(s)</Badge>
            {turma.pendingCount > 0 && (
              <Badge variant="outline" className="text-[10px] border-warning/40 text-warning">
                {turma.pendingCount} pendente(s)
              </Badge>
            )}
          </div>
        </div>
      </CardHeader>
      <CardContent className="flex gap-2 pt-0">
        <ManageMembersDialog turma={turma} />
        <Button
          size="sm" variant="ghost"
          className="ml-auto text-destructive hover:bg-destructive/10 hover:text-destructive"
          onClick={() => { if (confirm(`Arquivar a turma "${turma.name}"?`)) archive.mutate() }}
          disabled={archive.isPending}
          title="Arquivar turma"
        >
          <Trash2 className="h-4 w-4" />
        </Button>
      </CardContent>
    </Card>
  )
}

function ManageMembersDialog({ turma }: { turma: Turma }) {
  const qc = useQueryClient()
  const [open, setOpen] = useState(false)
  const [q, setQ] = useState("")

  const detail = useQuery({
    queryKey: ["turmas", "detail", turma.id],
    queryFn:  () => turmasApi.detail(turma.id),
    enabled:  open,
  })

  const search = useQuery({
    queryKey: ["turmas", "search", turma.id, q],
    queryFn:  () => turmasApi.searchStudents(turma.id, q),
    enabled:  open && q.trim().length >= 2,
  })

  const refresh = () => {
    qc.invalidateQueries({ queryKey: ["turmas", "detail", turma.id] })
    qc.invalidateQueries({ queryKey: ["turmas", "owned"] })
    qc.invalidateQueries({ queryKey: ["turmas", "search", turma.id] })
  }

  const invite = useMutation({
    mutationFn: (studentId: string) => turmasApi.invite(turma.id, studentId),
    onSuccess: () => { refresh(); toast.success("Convite enviado!") },
    onError: () => toast.error("Falha ao convidar (talvez já esteja na turma)."),
  })

  const remove = useMutation({
    mutationFn: (studentId: string) => turmasApi.removeMember(turma.id, studentId),
    onSuccess: () => { refresh(); toast.success("Removido da turma.") },
    onError: () => toast.error("Falha ao remover."),
  })

  const members = detail.data?.members ?? []

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm"><Users className="h-4 w-4 mr-1" />Gerenciar</Button>
      </DialogTrigger>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>{turma.emblem || "🎓"} {turma.name}</DialogTitle>
          <DialogDescription>Convide alunos da plataforma e gerencie quem está na turma.</DialogDescription>
        </DialogHeader>

        {/* Buscar + convidar */}
        <div className="space-y-2">
          <div className="relative">
            <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
            <Input className="pl-8" placeholder="Buscar aluno por nome…" value={q} onChange={(e) => setQ(e.target.value)} />
          </div>
          {q.trim().length >= 2 && (
            <div className="max-h-40 overflow-y-auto rounded-md border border-border divide-y divide-border/60">
              {search.isLoading && <p className="text-xs text-muted-foreground text-center py-3">Buscando…</p>}
              {!search.isLoading && (search.data?.length ?? 0) === 0 && (
                <p className="text-xs text-muted-foreground text-center py-3">Nenhum aluno encontrado.</p>
              )}
              {search.data?.map((u) => (
                <div key={u.userId} className="flex items-center gap-2 px-2.5 py-1.5">
                  <span className="flex-1 min-w-0 text-sm truncate">{u.name}</span>
                  <span className="text-[10px] text-muted-foreground tabular-nums">{u.xp} XP</span>
                  {u.relation === "none" ? (
                    <Button size="sm" variant="outline" className="h-7 px-2"
                      onClick={() => invite.mutate(u.userId)} disabled={invite.isPending}>
                      <UserPlus className="h-3.5 w-3.5 mr-1" />Convidar
                    </Button>
                  ) : (
                    <Badge variant="outline" className={cn("text-[10px]", u.relation === "member" ? "border-success/40 text-success" : "border-warning/40 text-warning")}>
                      {u.relation === "member" ? "Na turma" : "Convidado"}
                    </Badge>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Membros atuais */}
        <div>
          <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground mb-1.5">
            Membros ({members.filter((m) => m.status === "active").length})
          </p>
          <div className="max-h-56 overflow-y-auto space-y-1">
            {detail.isLoading && <Skeleton className="h-10 w-full" />}
            {!detail.isLoading && members.length === 0 && (
              <p className="text-sm text-muted-foreground text-center py-4">Ninguém ainda. Convide acima 👆</p>
            )}
            {members.map((m) => (
              <div key={m.memberId} className="flex items-center gap-2 rounded-md border border-border bg-popover/40 px-2.5 py-1.5">
                <span className="flex-1 min-w-0 text-sm truncate">{m.name}</span>
                <Badge variant="outline" className={cn("text-[10px]", m.status === "active" ? "border-success/40 text-success" : "border-warning/40 text-warning")}>
                  {m.status === "active" ? "ativo" : "convidado"}
                </Badge>
                <Button size="sm" variant="ghost" className="h-7 w-7 p-0 text-destructive hover:bg-destructive/10 hover:text-destructive"
                  onClick={() => remove.mutate(m.userId)} disabled={remove.isPending} title="Remover">
                  <Trash2 className="h-3.5 w-3.5" />
                </Button>
              </div>
            ))}
          </div>
        </div>
      </DialogContent>
    </Dialog>
  )
}
