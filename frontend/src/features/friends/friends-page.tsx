import { useState } from "react"
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query"
import { Check, Flame, Search, Trophy, UserPlus, Users, UserMinus, X } from "lucide-react"
import { toast } from "sonner"
import { friendsApi } from "@/api/friends"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"
import type { FriendRelation, UserSearchResult } from "@/types/api"

type Tab = "amigos" | "pedidos" | "adicionar"

/**
 * PR 64 — Amigos/Parcerias. Três abas: lista de amigos (placar por XP),
 * pedidos pendentes (recebidos/enviados) e adicionar (busca + enviar).
 */
export function FriendsPage() {
  const qc = useQueryClient()
  const [tab, setTab]     = useState<Tab>("amigos")
  const [query, setQuery] = useState("")

  const friendsQuery  = useQuery({ queryKey: ["friends"], queryFn: friendsApi.list })
  const requestsQuery = useQuery({ queryKey: ["friends", "requests"], queryFn: friendsApi.requests })
  const searchQuery   = useQuery({
    queryKey: ["friends", "search", query.trim()],
    queryFn:  () => friendsApi.search(query.trim()),
    enabled:  tab === "adicionar" && query.trim().length >= 2,
  })

  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ["friends"] })
  }

  const sendMut = useMutation({
    mutationFn: (id: string) => friendsApi.send(id),
    onSuccess: () => { toast.success("Pedido enviado!"); invalidate() },
    onError:   () => toast.error("Não foi possível enviar o pedido."),
  })
  const acceptMut = useMutation({
    mutationFn: (id: number) => friendsApi.accept(id),
    onSuccess: () => { toast.success("Amizade aceita!"); invalidate() },
    onError:   () => toast.error("Falha ao aceitar."),
  })
  const declineMut = useMutation({
    mutationFn: (id: number) => friendsApi.decline(id),
    onSuccess: () => { toast.success("Pedido recusado."); invalidate() },
    onError:   () => toast.error("Falha ao recusar."),
  })
  const removeMut = useMutation({
    mutationFn: (id: string) => friendsApi.remove(id),
    onSuccess: () => { toast.success("Amigo removido."); invalidate() },
    onError:   () => toast.error("Falha ao remover."),
  })

  const friends      = friendsQuery.data ?? []
  const incoming     = requestsQuery.data?.incoming ?? []
  const outgoing     = requestsQuery.data?.outgoing ?? []
  const incomingCount = incoming.length

  return (
    <div className="p-6 lg:p-10 max-w-3xl space-y-6">
      <header className="space-y-1">
        <h1 className="text-3xl font-display font-extrabold tracking-tight flex items-center gap-2">
          <Users className="h-7 w-7 text-primary" /> Amigos
        </h1>
        <p className="text-sm text-muted-foreground">
          Estude junto, compare progresso e motivem-se.
        </p>
      </header>

      {/* Abas */}
      <div className="flex gap-1 rounded-lg border border-border bg-popover/40 p-1">
        <TabBtn active={tab === "amigos"}    onClick={() => setTab("amigos")}    label={`Amigos (${friends.length})`} />
        <TabBtn active={tab === "pedidos"}   onClick={() => setTab("pedidos")}   label="Pedidos" badge={incomingCount} />
        <TabBtn active={tab === "adicionar"} onClick={() => setTab("adicionar")} label="Adicionar" />
      </div>

      {/* ── Amigos ── */}
      {tab === "amigos" && (
        <section className="space-y-2">
          {friendsQuery.isLoading && <Skeleton className="h-24" />}
          {!friendsQuery.isLoading && friends.length === 0 && (
            <EmptyState icon={<Users />} text="Você ainda não tem amigos. Vá em “Adicionar” e mande o primeiro pedido!" />
          )}
          {friends.map((f, i) => (
            <Card key={f.friendshipId} className="animate-pop-in" style={{ animationDelay: `${i * 30}ms` }}>
              <CardContent className="flex items-center gap-3 py-3">
                <span className={cn(
                  "w-7 text-center font-display font-extrabold",
                  i === 0 ? "text-warning" : i === 1 ? "text-muted-foreground" : i === 2 ? "text-accent" : "text-muted-foreground/60",
                )}>{i + 1}</span>
                <Avatar className="h-10 w-10"><AvatarFallback>{initials(f.name)}</AvatarFallback></Avatar>
                <div className="flex-1 min-w-0">
                  <p className="font-semibold truncate">{f.name}
                    {f.activeTitle && <span className="ml-1.5 text-xs font-medium text-primary/80 italic">· {f.activeTitle}</span>}
                  </p>
                  <p className="text-xs text-muted-foreground flex items-center gap-2">
                    <span className="inline-flex items-center gap-1"><Trophy className="h-3 w-3" />{fmt(f.xp)} XP</span>
                    <span className="inline-flex items-center gap-1"><Flame className="h-3 w-3" />{f.streakDays}d</span>
                  </p>
                </div>
                <Button variant="ghost" size="icon" title="Remover amigo"
                  onClick={() => removeMut.mutate(f.userId)} disabled={removeMut.isPending}>
                  <UserMinus className="h-4 w-4 text-muted-foreground" />
                </Button>
              </CardContent>
            </Card>
          ))}
        </section>
      )}

      {/* ── Pedidos ── */}
      {tab === "pedidos" && (
        <section className="space-y-4">
          {requestsQuery.isLoading && <Skeleton className="h-24" />}
          {!requestsQuery.isLoading && incoming.length === 0 && outgoing.length === 0 && (
            <EmptyState icon={<UserPlus />} text="Nenhum pedido pendente." />
          )}

          {incoming.length > 0 && (
            <div className="space-y-2">
              <h2 className="text-xs uppercase tracking-wider text-muted-foreground font-bold">Recebidos ({incoming.length})</h2>
              {incoming.map((r) => (
                <Card key={r.friendshipId}>
                  <CardContent className="flex items-center gap-3 py-3">
                    <Avatar className="h-10 w-10"><AvatarFallback>{initials(r.name)}</AvatarFallback></Avatar>
                    <div className="flex-1 min-w-0">
                      <p className="font-semibold truncate">{r.name}</p>
                      <p className="text-xs text-muted-foreground">{fmt(r.xp)} XP · {r.createdAt}</p>
                    </div>
                    <Button size="sm" onClick={() => acceptMut.mutate(r.friendshipId)} disabled={acceptMut.isPending}>
                      <Check className="h-4 w-4 mr-1" />Aceitar
                    </Button>
                    <Button size="sm" variant="ghost" onClick={() => declineMut.mutate(r.friendshipId)} disabled={declineMut.isPending}>
                      <X className="h-4 w-4" />
                    </Button>
                  </CardContent>
                </Card>
              ))}
            </div>
          )}

          {outgoing.length > 0 && (
            <div className="space-y-2">
              <h2 className="text-xs uppercase tracking-wider text-muted-foreground font-bold">Enviados ({outgoing.length})</h2>
              {outgoing.map((r) => (
                <Card key={r.friendshipId}>
                  <CardContent className="flex items-center gap-3 py-3">
                    <Avatar className="h-10 w-10"><AvatarFallback>{initials(r.name)}</AvatarFallback></Avatar>
                    <div className="flex-1 min-w-0">
                      <p className="font-semibold truncate">{r.name}</p>
                      <p className="text-xs text-muted-foreground">{fmt(r.xp)} XP</p>
                    </div>
                    <Badge variant="outline" className="text-[10px]">Pendente</Badge>
                  </CardContent>
                </Card>
              ))}
            </div>
          )}
        </section>
      )}

      {/* ── Adicionar ── */}
      {tab === "adicionar" && (
        <section className="space-y-3">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
            <Input
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="Buscar por nome (mín. 2 letras)…"
              className="pl-9"
            />
          </div>

          {query.trim().length < 2 && (
            <EmptyState icon={<Search />} text="Digite ao menos 2 letras pra buscar." />
          )}
          {searchQuery.isLoading && <Skeleton className="h-20" />}
          {query.trim().length >= 2 && !searchQuery.isLoading && (searchQuery.data?.length ?? 0) === 0 && (
            <EmptyState icon={<Search />} text="Nenhum aluno encontrado." />
          )}
          {(searchQuery.data ?? []).map((u) => (
            <Card key={u.userId}>
              <CardContent className="flex items-center gap-3 py-3">
                <Avatar className="h-10 w-10"><AvatarFallback>{initials(u.name)}</AvatarFallback></Avatar>
                <div className="flex-1 min-w-0">
                  <p className="font-semibold truncate">{u.name}</p>
                  <p className="text-xs text-muted-foreground">{fmt(u.xp)} XP</p>
                </div>
                <SearchAction u={u} onSend={() => sendMut.mutate(u.userId)} pending={sendMut.isPending} />
              </CardContent>
            </Card>
          ))}
        </section>
      )}
    </div>
  )
}

function SearchAction({ u, onSend, pending }: { u: UserSearchResult; onSend: () => void; pending: boolean }) {
  const labels: Record<FriendRelation, string> = {
    none: "Adicionar", pending_out: "Enviado", pending_in: "Te adicionou",
    friends: "Amigos", blocked: "Indisponível",
  }
  if (u.relationStatus === "none") {
    return <Button size="sm" onClick={onSend} disabled={pending}><UserPlus className="h-4 w-4 mr-1" />Adicionar</Button>
  }
  return <Badge variant={u.relationStatus === "friends" ? "default" : "outline"} className="text-[10px]">{labels[u.relationStatus]}</Badge>
}

function TabBtn({ active, onClick, label, badge }: { active: boolean; onClick: () => void; label: string; badge?: number }) {
  return (
    <button
      onClick={onClick}
      className={cn(
        "flex-1 rounded-md px-3 py-2 text-sm font-medium transition-colors relative",
        active ? "bg-background text-foreground shadow-sm" : "text-muted-foreground hover:text-foreground",
      )}
    >
      {label}
      {!!badge && badge > 0 && (
        <span className="absolute -top-1 -right-1 h-5 min-w-5 px-1 rounded-full bg-accent text-accent-foreground text-[10px] font-bold inline-flex items-center justify-center">
          {badge}
        </span>
      )}
    </button>
  )
}

function EmptyState({ icon, text }: { icon: React.ReactNode; text: string }) {
  return (
    <div className="flex flex-col items-center gap-2 py-10 text-center text-muted-foreground">
      <span className="[&_svg]:h-8 [&_svg]:w-8 opacity-50">{icon}</span>
      <p className="text-sm max-w-xs">{text}</p>
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
