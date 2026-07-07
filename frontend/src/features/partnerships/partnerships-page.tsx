import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { Check, Loader2, UserPlus, X, Trophy, AlertTriangle } from "lucide-react"
import { toast } from "sonner"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { useAuth } from "@/stores/auth"
import { partnershipsApi, type Partnership } from "@/api/partnerships"
import { friendsApi } from "@/api/friends"
import { cn } from "@/lib/utils"

const STATE_LABEL: Record<string, { label: string; tone: string }> = {
  Ok:      { label: "Em dia 🧶",  tone: "text-success" },
  Tangled: { label: "Enrolado 😼", tone: "text-warning" },
  Dropped: { label: "Caiu 😿",    tone: "text-destructive" },
}

/**
 * Parceria ("Novelo de Lã / Parceiro de Trilha"). Mostra as parcerias ativas
 * com o estado do novelo (de quem é a vez, progresso da meta, ciclos, novelos
 * fechados), os convites pendentes, e a lista de amigos pra convidar.
 */
export function PartnershipsPage() {
  const me = useAuth((s) => s.user)?.id
  const qc = useQueryClient()

  const list = useQuery({ queryKey: ["partnerships"], queryFn: partnershipsApi.list })
  const requests = useQuery({ queryKey: ["partnerships", "requests"], queryFn: partnershipsApi.requests })
  const friends = useQuery({ queryKey: ["friends"], queryFn: friendsApi.list })

  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ["partnerships"] })
  }

  const inviteMut = useMutation({
    mutationFn: (id: string) => partnershipsApi.invite(id),
    onSuccess: () => { toast.success("Convite de parceria enviado! 🧶"); invalidate() },
    onError: () => toast.error("Não foi possível convidar (já são parceiros ou limite de 3?)."),
  })
  const acceptMut  = useMutation({ mutationFn: (id: number) => partnershipsApi.accept(id),  onSuccess: () => { toast.success("Parceria iniciada!"); invalidate() } })
  const declineMut = useMutation({ mutationFn: (id: number) => partnershipsApi.decline(id), onSuccess: invalidate })
  const leaveMut   = useMutation({ mutationFn: (id: number) => partnershipsApi.leave(id),   onSuccess: () => { toast.info("Parceria desfeita."); invalidate() } })

  const active = list.data ?? []
  const incoming = requests.data?.incoming ?? []
  const outgoing = requests.data?.outgoing ?? []

  // Amigos que ainda não têm parceria ativa / convite pendente comigo.
  const busy = new Set<string>([
    ...active.map((p) => p.partnerId),
    ...incoming.map((r) => r.partnerId),
    ...outgoing.map((r) => r.partnerId),
  ])
  const invitable = (friends.data ?? []).filter((f) => !busy.has(f.userId))

  return (
    <div className="mx-auto w-full max-w-3xl space-y-6 p-4 sm:p-6 lg:p-10">
      <header>
        <h1 className="font-display text-2xl font-extrabold">🧶 Parceiros de Trilha</h1>
        <p className="text-sm text-muted-foreground">
          Cumpra sua meta diária pra "passar o novelo" ao parceiro. Fechem o novelo juntos e ganhem moedas!
        </p>
      </header>

      {/* Convites recebidos */}
      {incoming.length > 0 && (
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-base">Convites recebidos</CardTitle></CardHeader>
          <CardContent className="space-y-2">
            {incoming.map((r) => (
              <div key={r.id} className="flex items-center justify-between gap-2">
                <span className="text-sm"><b>{r.partnerName}</b> quer ser seu parceiro</span>
                <div className="flex gap-2">
                  <Button size="sm" onClick={() => acceptMut.mutate(r.id)} disabled={acceptMut.isPending}>Aceitar</Button>
                  <Button size="sm" variant="ghost" onClick={() => declineMut.mutate(r.id)}>Recusar</Button>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      {/* Parcerias ativas */}
      <section className="space-y-3">
        <h2 className="text-sm font-display font-bold uppercase tracking-wider text-muted-foreground">
          Suas parcerias ({active.length})
        </h2>
        {list.isLoading ? (
          <Skeleton className="h-28 w-full" />
        ) : active.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            Você ainda não tem parcerias. Convide um amigo abaixo pra começar um novelo!
          </p>
        ) : (
          active.map((p) => <PartnershipCard key={p.id} p={p} me={me} onLeave={() => leaveMut.mutate(p.id)} />)
        )}
      </section>

      {/* Convidar amigos */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="flex items-center gap-2 text-base"><UserPlus className="h-4 w-4" /> Convidar amigo</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          {friends.isLoading ? (
            <Skeleton className="h-9 w-full" />
          ) : (friends.data ?? []).length === 0 ? (
            <p className="text-sm text-muted-foreground">Você precisa ter amigos antes — adicione na aba Amigos.</p>
          ) : invitable.length === 0 ? (
            <p className="text-sm text-muted-foreground">Todos os seus amigos já estão em parceria ou com convite pendente.</p>
          ) : (
            invitable.map((f) => (
              <div key={f.userId} className="flex items-center justify-between gap-2">
                <span className="min-w-0 truncate text-sm font-medium">{f.name}</span>
                <Button size="sm" variant="outline" onClick={() => inviteMut.mutate(f.userId)} disabled={inviteMut.isPending}>
                  Convidar
                </Button>
              </div>
            ))
          )}
          {outgoing.length > 0 && (
            <p className="pt-1 text-xs text-muted-foreground">
              Aguardando resposta de: {outgoing.map((o) => o.partnerName).join(", ")}
            </p>
          )}
        </CardContent>
      </Card>
    </div>
  )
}

function PartnershipCard({ p, me, onLeave }: { p: Partnership; me?: string; onLeave: () => void }) {
  const y = p.yarn
  const myTurn = y?.isMyTurn ?? false
  const st = y ? STATE_LABEL[y.state] ?? STATE_LABEL.Ok : STATE_LABEL.Ok
  const pct = y ? Math.min(100, Math.round((y.progress / Math.max(y.dailyGoal, 1)) * 100)) : 0

  return (
    <Card className={cn(myTurn && "border-primary/40 bg-primary/5")}>
      <CardContent className="space-y-4 py-4">
        <div className="flex items-center justify-between gap-2">
          <div className="min-w-0">
            <p className="font-semibold truncate">{p.partnerName}</p>
            <p className="text-xs text-muted-foreground">
              {p.novelosCompleted > 0 && <span className="mr-2 inline-flex items-center gap-1"><Trophy className="h-3 w-3 text-warning" />{p.novelosCompleted} novelo(s)</span>}
              <span className={st.tone}>{st.label}</span>
            </p>
          </div>
          <Button size="sm" variant="ghost" className="text-muted-foreground" onClick={onLeave}>Desfazer</Button>
        </div>

        {y && (
          <>
            {/* Cena do novelo: desliza entre você e o parceiro conforme a vez. */}
            <div className="flex items-center gap-2">
              <Side label="Você" here={myTurn} />
              <div className="relative h-16 flex-1">
                <div className="absolute inset-x-3 top-1/2 -translate-y-1/2 border-t-2 border-dashed border-primary/30" />
                <div
                  className="absolute top-1/2 -translate-y-1/2 transition-[left] duration-700 ease-out"
                  style={{ left: myTurn ? "0px" : "calc(100% - 60px)" }}
                >
                  <YarnBall pct={pct} active={myTurn} tangled={y.state === "Tangled"} dropped={y.state === "Dropped"} />
                </div>
              </div>
              <Side label={p.partnerName} here={!myTurn} />
            </div>

            {/* Ciclos do novelo */}
            <div className="flex items-center justify-center gap-1.5">
              {Array.from({ length: y.totalCycles }).map((_, i) => (
                <span
                  key={i}
                  className={cn("h-2 w-2 rounded-full transition-colors", i < y.cyclesCompleted ? "bg-primary" : "bg-border")}
                />
              ))}
              <span className="ml-1 text-[11px] tabular-nums text-muted-foreground">ciclo {y.cyclesCompleted}/{y.totalCycles}</span>
            </div>

            <div className="flex items-center justify-between text-xs">
              <span className={cn("font-medium", myTurn ? "text-primary" : "text-muted-foreground")}>
                {myTurn ? "🧶 O novelo está com você — cumpra a meta!" : "Aguardando seu parceiro cumprir a meta…"}
              </span>
              <span className="tabular-nums text-muted-foreground">meta do dia: {Math.min(y.progress, y.dailyGoal)}/{y.dailyGoal}</span>
            </div>

            {y.state === "Tangled" && (
              <p className="flex items-center gap-1 text-[11px] text-warning">
                <AlertTriangle className="h-3 w-3" /> Novelo enrolado — cumpra a meta hoje pra não deixar cair!
              </p>
            )}
          </>
        )}
      </CardContent>
    </Card>
  )
}

/** Avatar de ponta (você / parceiro). Destacado quando o novelo está com ele. */
function Side({ label, here }: { label: string; here: boolean }) {
  const initials = label === "Você"
    ? "EU"
    : label.trim().split(/\s+/).map((w) => w[0]).slice(0, 2).join("").toUpperCase()
  return (
    <div className="flex w-12 shrink-0 flex-col items-center gap-1">
      <div className={cn(
        "grid h-10 w-10 place-items-center rounded-full border-2 text-[11px] font-bold transition-colors",
        here ? "border-primary bg-primary/15 text-primary" : "border-border bg-muted/30 text-muted-foreground",
      )}>
        {initials}
      </div>
      <span className="max-w-[3rem] truncate text-[10px] text-muted-foreground">{label}</span>
    </div>
  )
}

/**
 * Novelo de lã em SVG. O anel externo mostra a meta do dia; conforme ela
 * enche, o novelo "desenrola". Gira devagar quando é a sua vez; muda de cor
 * quando enrolado/caído. (Ideia 1 — representação visual animada.)
 */
function YarnBall({ pct, active, tangled, dropped }: { pct: number; active: boolean; tangled?: boolean; dropped?: boolean }) {
  const r = 40
  const c = 2 * Math.PI * r
  const off = c * (1 - Math.min(1, Math.max(0, pct / 100)))
  const bodyClass = dropped ? "fill-destructive" : tangled ? "fill-warning" : "fill-primary"
  const ringClass = dropped ? "stroke-destructive" : tangled ? "stroke-warning" : "stroke-primary"

  return (
    <div className="relative h-[60px] w-[60px]">
      {active && <div className="absolute inset-1 rounded-full bg-primary/30 blur-md animate-pulse" />}
      <svg viewBox="0 0 100 100" className="relative h-full w-full">
        {/* trilho do anel */}
        <circle cx="50" cy="50" r={r} className="fill-none stroke-border" strokeWidth="7" />
        {/* progresso da meta do dia */}
        <circle
          cx="50" cy="50" r={r}
          className={cn("fill-none", ringClass)}
          strokeWidth="7" strokeLinecap="round"
          strokeDasharray={c} strokeDashoffset={off}
          transform="rotate(-90 50 50)"
          style={{ transition: "stroke-dashoffset .6s ease" }}
        />
        {/* corpo do novelo (gira devagar quando ativo) */}
        <g
          className={cn(active && "animate-[spin_16s_linear_infinite]")}
          style={{ transformBox: "fill-box", transformOrigin: "center" }}
        >
          <circle cx="50" cy="50" r="27" className={bodyClass} />
          <g className="stroke-white/45" strokeWidth="2.2" fill="none" strokeLinecap="round">
            <ellipse cx="50" cy="50" rx="27" ry="12" transform="rotate(25 50 50)" />
            <ellipse cx="50" cy="50" rx="27" ry="12" transform="rotate(-25 50 50)" />
            <ellipse cx="50" cy="50" rx="12" ry="27" transform="rotate(20 50 50)" />
            <path d="M32 42 Q50 34 68 44" />
            <path d="M32 58 Q50 66 68 56" />
          </g>
          {/* pontinha de linha */}
          <path d="M74 58 q10 6 6 16" className="stroke-white/50" strokeWidth="2.2" fill="none" strokeLinecap="round" />
        </g>
      </svg>
    </div>
  )
}
