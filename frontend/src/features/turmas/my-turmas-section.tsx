import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { Check, GraduationCap, LogOut, X } from "lucide-react"
import { toast } from "sonner"
import { turmasApi } from "@/api/turmas"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"

/**
 * Seção "Minhas turmas" do aluno — vive no Perfil. Lista as turmas que o
 * aluno participa e os convites pendentes (aceitar/recusar). Os convites
 * também chegam pelo sino de notificações (que aponta pra cá).
 */
export function MyTurmasSection() {
  const qc = useQueryClient()

  const mine    = useQuery({ queryKey: ["turmas", "mine"], queryFn: turmasApi.mine })
  const invites = useQuery({ queryKey: ["turmas", "invites"], queryFn: turmasApi.invites })

  const refresh = () => {
    qc.invalidateQueries({ queryKey: ["turmas", "mine"] })
    qc.invalidateQueries({ queryKey: ["turmas", "invites"] })
    qc.invalidateQueries({ queryKey: ["notifications", "count"] })
  }

  const accept = useMutation({
    mutationFn: (memberId: number) => turmasApi.accept(memberId),
    onSuccess: () => { refresh(); toast.success("Você entrou na turma!") },
    onError: () => toast.error("Falha ao aceitar."),
  })
  const decline = useMutation({
    mutationFn: (memberId: number) => turmasApi.decline(memberId),
    onSuccess: () => { refresh(); toast.success("Convite recusado.") },
    onError: () => toast.error("Falha ao recusar."),
  })
  const leave = useMutation({
    mutationFn: (turmaId: number) => turmasApi.leave(turmaId),
    onSuccess: () => { refresh(); toast.success("Você saiu da turma.") },
    onError: () => toast.error("Falha ao sair."),
  })

  const turmas       = mine.data ?? []
  const pendingList  = invites.data ?? []
  const nothing      = turmas.length === 0 && pendingList.length === 0

  return (
    <Card className="animate-pop-in" style={{ animationDelay: "150ms" }}>
      <CardHeader>
        <CardTitle className="text-sm font-display font-bold uppercase tracking-wider text-muted-foreground flex items-center gap-2">
          <GraduationCap className="h-4 w-4" />
          Minhas turmas{turmas.length > 0 && ` (${turmas.length})`}
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {/* Convites pendentes */}
        {pendingList.map((inv) => (
          <div key={inv.memberId} className="flex items-center gap-3 rounded-md border border-warning/40 bg-warning/5 px-3 py-2">
            <span className="text-xl leading-none">{inv.emblem || "🎓"}</span>
            <div className="flex-1 min-w-0">
              <p className="text-sm font-medium truncate">{inv.turmaName}</p>
              <p className="text-xs text-muted-foreground truncate">Convite de {inv.ownerName}</p>
            </div>
            <Button size="sm" className="h-8" onClick={() => accept.mutate(inv.memberId)} disabled={accept.isPending || decline.isPending}>
              <Check className="h-4 w-4 mr-1" />Aceitar
            </Button>
            <Button size="sm" variant="ghost" className="h-8 w-8 p-0 text-destructive hover:bg-destructive/10 hover:text-destructive"
              onClick={() => decline.mutate(inv.memberId)} disabled={accept.isPending || decline.isPending} title="Recusar">
              <X className="h-4 w-4" />
            </Button>
          </div>
        ))}

        {/* Turmas ativas */}
        {turmas.map((t) => (
          <div key={t.id} className="flex items-center gap-3 rounded-md border border-border bg-popover/40 px-3 py-2">
            <span className="text-xl leading-none">{t.emblem || "🎓"}</span>
            <div className="flex-1 min-w-0">
              <p className="text-sm font-medium truncate">{t.name}</p>
              <p className="text-xs text-muted-foreground truncate">Prof. {t.ownerName} · {t.memberCount} aluno(s)</p>
            </div>
            <Badge variant="outline" className="text-[10px] border-success/40 text-success">membro</Badge>
            <Button size="sm" variant="ghost" className="h-8 w-8 p-0 text-muted-foreground hover:text-destructive"
              onClick={() => { if (confirm(`Sair da turma "${t.name}"?`)) leave.mutate(t.id) }}
              disabled={leave.isPending} title="Sair da turma">
              <LogOut className="h-4 w-4" />
            </Button>
          </div>
        ))}

        {nothing && (
          <p className="text-sm text-muted-foreground text-center py-2">
            Você ainda não está em nenhuma turma. Quando um professor te convidar, o convite aparece aqui. 🎓
          </p>
        )}
      </CardContent>
    </Card>
  )
}
