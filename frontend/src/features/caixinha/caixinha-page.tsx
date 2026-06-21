import { useState } from "react"
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query"
import { Crown, Flame, LogOut, MoreVertical, Search, Send, Trophy, UserMinus, Users } from "lucide-react"
import { toast } from "sonner"
import { caixinhaApi } from "@/api/caixinha"
import { EventsSection } from "./caixinha-events"
import { useAuth } from "@/stores/auth"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "@/components/ui/dropdown-menu"
import { Input } from "@/components/ui/input"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"

const EMBLEMS = ["📦", "🐱", "🐈", "🐾", "👑", "⭐", "🔥", "🚀", "🧠", "💎"]

/**
 * PR 65 — Caixinha de Gatos (clã/grupo). Sem caixinha: criar ou buscar+entrar.
 * Com caixinha: painel (placar coletivo, membros c/ ofensiva, mural, ranking).
 */
export function CaixinhaPage() {
  const qc = useQueryClient()
  const mineQuery = useQuery({ queryKey: ["caixinha", "mine"], queryFn: caixinhaApi.mine })

  if (mineQuery.isLoading) {
    return <div className="p-6 lg:p-10 max-w-3xl space-y-4"><Skeleton className="h-10 w-48" /><Skeleton className="h-40" /></div>
  }

  return (
    <div className="p-6 lg:p-10 max-w-3xl space-y-6">
      <header className="space-y-1">
        <h1 className="text-3xl font-display font-extrabold tracking-tight flex items-center gap-2">
          📦 Caixinha de Gatos
        </h1>
        <p className="text-sm text-muted-foreground">
          Seu clã. Estudem juntos, somem pontos e subam no ranking das caixinhas.
        </p>
      </header>

      {mineQuery.data
        ? <Panel detail={mineQuery.data} onChange={() => qc.invalidateQueries({ queryKey: ["caixinha"] })} />
        : <NoCaixinha onChange={() => qc.invalidateQueries({ queryKey: ["caixinha"] })} />}

      <EventsSection canLead={mineQuery.data?.myRole === "Leader"} />
    </div>
  )
}

// ── Sem caixinha: criar ou entrar ───────────────────────────────────

function NoCaixinha({ onChange }: { onChange: () => void }) {
  const [name, setName]     = useState("")
  const [emblem, setEmblem] = useState("📦")
  const [query, setQuery]   = useState("")

  const browseQuery = useQuery({ queryKey: ["caixinha", "browse", query.trim()], queryFn: () => caixinhaApi.browse(query.trim() || undefined) })

  const createMut = useMutation({
    mutationFn: () => caixinhaApi.create(name.trim(), emblem),
    onSuccess: () => { toast.success("Caixinha criada! 🎉"); onChange() },
    onError:   () => toast.error("Não foi possível criar (nome em uso ou inválido?)."),
  })
  const joinMut = useMutation({
    mutationFn: (id: number) => caixinhaApi.join(id),
    onSuccess: () => { toast.success("Você entrou na caixinha!"); onChange() },
    onError:   () => toast.error("Não foi possível entrar (cheia?)."),
  })

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader><CardTitle className="text-base">Criar minha caixinha</CardTitle></CardHeader>
        <CardContent className="space-y-3">
          <div className="flex flex-wrap gap-1.5">
            {EMBLEMS.map((e) => (
              <button key={e} onClick={() => setEmblem(e)}
                className={cn("h-10 w-10 rounded-md border text-xl transition-colors",
                  emblem === e ? "border-primary bg-primary/10" : "border-border hover:border-primary/50")}>
                {e}
              </button>
            ))}
          </div>
          <div className="flex gap-2">
            <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="Nome da caixinha (ex: Clã dos Miados)" maxLength={40} />
            <Button onClick={() => createMut.mutate()} disabled={name.trim().length < 2 || createMut.isPending}>Criar</Button>
          </div>
        </CardContent>
      </Card>

      <div className="space-y-3">
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Buscar caixinhas pra entrar…" className="pl-9" />
        </div>
        {browseQuery.isLoading && <Skeleton className="h-20" />}
        {!browseQuery.isLoading && (browseQuery.data?.length ?? 0) === 0 && (
          <p className="text-sm text-muted-foreground text-center py-6">Nenhuma caixinha encontrada. Crie a primeira!</p>
        )}
        {(browseQuery.data ?? []).map((c) => {
          const full = c.memberCount >= 10
          return (
            <Card key={c.id}>
              <CardContent className="flex items-center gap-3 py-3">
                <span className="text-2xl">{c.emblem}</span>
                <div className="flex-1 min-w-0">
                  <p className="font-semibold truncate">{c.name}</p>
                  <p className="text-xs text-muted-foreground">{c.memberCount}/10 membros · {fmt(c.collectivePoints)} pts · #{c.rank}</p>
                </div>
                <Button size="sm" disabled={full || joinMut.isPending} onClick={() => joinMut.mutate(c.id)}>
                  {full ? "Cheia" : "Entrar"}
                </Button>
              </CardContent>
            </Card>
          )
        })}
      </div>
    </div>
  )
}

// ── Com caixinha: painel ────────────────────────────────────────────

function Panel({ detail, onChange }: { detail: import("@/types/api").CaixinhaDetail; onChange: () => void }) {
  const myId = useAuth((s) => s.user?.id)
  const isLeader = detail.myRole === "Leader"
  const [msg, setMsg] = useState("")

  const leaveMut = useMutation({
    mutationFn: () => caixinhaApi.leave(),
    onSuccess: (r) => { toast.success(r.disbanded ? "Caixinha dissolvida." : "Você saiu da caixinha."); onChange() },
    onError:   () => toast.error("Falha ao sair."),
  })
  const kickMut = useMutation({
    mutationFn: (id: string) => caixinhaApi.kick(id),
    onSuccess: () => { toast.success("Membro removido."); onChange() },
    onError:   () => toast.error("Falha ao remover."),
  })
  const postMut = useMutation({
    mutationFn: () => caixinhaApi.postMural(msg.trim()),
    onSuccess: () => { setMsg(""); onChange() },
    onError:   () => toast.error("Falha ao postar."),
  })

  const lbQuery = useQuery({ queryKey: ["caixinha", "leaderboard"], queryFn: () => caixinhaApi.leaderboard(10) })

  return (
    <div className="space-y-6">
      {/* Header da caixinha */}
      <Card className="bg-gradient-to-br from-primary/10 via-card to-card border-primary/30">
        <CardContent className="flex items-center gap-4 py-5">
          <span className="text-5xl">{detail.emblem}</span>
          <div className="flex-1 min-w-0">
            <h2 className="text-2xl font-display font-extrabold truncate">{detail.name}</h2>
            <p className="text-sm text-muted-foreground">
              #{detail.rank} no ranking · {detail.memberCount}/10 membros · {detail.activeTodayCount} ativos hoje
            </p>
          </div>
          <div className="text-right">
            <p className="font-display text-2xl font-extrabold text-primary">{fmt(detail.collectivePoints)}</p>
            <p className="text-[10px] uppercase tracking-wider text-muted-foreground">pontos coletivos</p>
          </div>
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="icon" className="self-start -mr-1 -mt-1" title="Opções" disabled={leaveMut.isPending}>
                <MoreVertical className="h-4 w-4" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem destructive onClick={() => leaveMut.mutate()}>
                <LogOut className="h-4 w-4 mr-2" /> Sair da caixinha
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </CardContent>
      </Card>

      {/* Membros */}
      <Card>
        <CardHeader>
          <CardTitle className="text-sm font-display font-bold uppercase tracking-wider text-muted-foreground flex items-center gap-2">
            <Users className="h-4 w-4" /> Membros ({detail.memberCount})
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          {detail.members.map((m) => (
            <div key={m.userId} className="flex items-center gap-3 rounded-md border border-border bg-popover/40 px-3 py-2">
              <span className="relative">
                <Avatar className="h-9 w-9"><AvatarFallback>{initials(m.name)}</AvatarFallback></Avatar>
                <span title={m.activeToday ? "Ativo hoje" : "Inativo hoje"}
                  className={cn("absolute -bottom-0.5 -right-0.5 h-3 w-3 rounded-full border-2 border-card",
                    m.activeToday ? "bg-success" : "bg-muted")} />
              </span>
              <div className="flex-1 min-w-0">
                <p className="font-semibold truncate flex items-center gap-1">
                  {m.name}
                  {m.role === "Leader" && <Crown className="h-3.5 w-3.5 text-warning" />}
                </p>
                <p className="text-xs text-muted-foreground flex items-center gap-2">
                  <span className="inline-flex items-center gap-1"><Trophy className="h-3 w-3" />{fmt(m.xp)}</span>
                  <span className="inline-flex items-center gap-1"><Flame className="h-3 w-3" />{m.streakDays}d</span>
                </p>
              </div>
              {isLeader && m.userId !== myId && (
                <Button variant="ghost" size="icon" title="Remover" onClick={() => kickMut.mutate(m.userId)} disabled={kickMut.isPending}>
                  <UserMinus className="h-4 w-4 text-muted-foreground" />
                </Button>
              )}
            </div>
          ))}
        </CardContent>
      </Card>

      {/* Mural */}
      <Card>
        <CardHeader><CardTitle className="text-sm font-display font-bold uppercase tracking-wider text-muted-foreground">Mural</CardTitle></CardHeader>
        <CardContent className="space-y-3">
          <div className="flex gap-2">
            <Input value={msg} onChange={(e) => setMsg(e.target.value)} placeholder="Escreva no mural…" maxLength={500}
              onKeyDown={(e) => { if (e.key === "Enter" && msg.trim()) postMut.mutate() }} />
            <Button size="icon" onClick={() => postMut.mutate()} disabled={!msg.trim() || postMut.isPending}><Send className="h-4 w-4" /></Button>
          </div>
          {detail.mural.length === 0 && <p className="text-sm text-muted-foreground text-center py-4">Sem mensagens ainda. Diga oi! 🐾</p>}
          {detail.mural.map((m) => (
            <div key={m.id} className="rounded-md border border-border bg-popover/40 px-3 py-2">
              <p className="text-xs text-muted-foreground"><span className="font-semibold text-foreground">{m.authorName}</span> · {m.createdAt}</p>
              <p className="text-sm">{m.text}</p>
            </div>
          ))}
        </CardContent>
      </Card>

      {/* Ranking entre caixinhas */}
      <Card>
        <CardHeader><CardTitle className="text-sm font-display font-bold uppercase tracking-wider text-muted-foreground">Ranking das caixinhas</CardTitle></CardHeader>
        <CardContent className="space-y-1.5">
          {(lbQuery.data ?? []).map((c) => (
            <div key={c.id} className={cn("flex items-center gap-3 rounded-md px-3 py-2 border",
              c.id === detail.id ? "border-primary bg-primary/10" : "border-border bg-popover/40")}>
              <span className={cn("w-6 text-center font-display font-extrabold",
                c.rank === 1 ? "text-warning" : c.rank === 2 ? "text-muted-foreground" : c.rank === 3 ? "text-accent" : "text-muted-foreground/60")}>{c.rank}</span>
              <span className="text-xl">{c.emblem}</span>
              <span className="flex-1 min-w-0 font-semibold truncate">{c.name}</span>
              <span className="text-sm text-muted-foreground">{fmt(c.collectivePoints)} pts</span>
            </div>
          ))}
        </CardContent>
      </Card>
    </div>
  )
}

function fmt(n: number): string {
  if (n < 1_000) return n.toString()
  return `${(n / 1_000).toFixed(n < 10_000 ? 1 : 0).replace(/\.0$/, "")}k`
}

function initials(name?: string | null) {
  if (!name) return "?"
  return name.trim().split(/\s+/).map((p) => p[0]?.toUpperCase() ?? "").slice(0, 2).join("")
}
