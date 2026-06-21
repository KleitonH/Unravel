import { useState } from "react"
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query"
import { CalendarDays, ChevronDown, Plus, Swords, Trophy } from "lucide-react"
import { toast } from "sonner"
import { caixinhaApi } from "@/api/caixinha"
import { useAuth } from "@/stores/auth"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"
import type { CaixinhaEvent, CaixinhaEventStatus } from "@/types/api"

const STATUS: Record<CaixinhaEventStatus, { label: string; cls: string }> = {
  active:   { label: "Ao vivo",  cls: "bg-success/15 text-success border-success/30" },
  upcoming: { label: "Em breve", cls: "bg-warning/15 text-warning border-warning/30" },
  finished: { label: "Encerrado", cls: "bg-muted/40 text-muted-foreground border-border" },
}

/**
 * PR 65c — eventos entre caixinhas. Lista eventos (ativo→em breve→encerrado),
 * ranking ao vivo expansível e participar (líder). Moderador vê o criador.
 */
export function EventsSection({ canLead }: { canLead: boolean }) {
  const qc = useQueryClient()
  const isModerator = useAuth((s) => s.isModerator())
  const eventsQuery = useQuery({ queryKey: ["caixinha", "events"], queryFn: caixinhaApi.events.list })

  const invalidate = () => qc.invalidateQueries({ queryKey: ["caixinha"] })

  return (
    <Card>
      <CardHeader className="flex-row items-center gap-2">
        <Swords className="h-4 w-4 text-primary" />
        <CardTitle className="text-sm font-display font-bold uppercase tracking-wider text-muted-foreground">
          Eventos entre caixinhas
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {isModerator && <CreateEventForm onCreated={invalidate} />}

        {eventsQuery.isLoading && <Skeleton className="h-20" />}
        {!eventsQuery.isLoading && (eventsQuery.data?.length ?? 0) === 0 && (
          <p className="text-sm text-muted-foreground text-center py-4">
            Nenhum evento por enquanto. {isModerator ? "Crie o primeiro acima!" : "Volte em breve! 🐾"}
          </p>
        )}
        {(eventsQuery.data ?? []).map((ev) => (
          <EventCard key={ev.id} ev={ev} canLead={canLead} onJoined={invalidate} />
        ))}
      </CardContent>
    </Card>
  )
}

function EventCard({ ev, canLead, onJoined }: { ev: CaixinhaEvent; canLead: boolean; onJoined: () => void }) {
  const qc = useQueryClient()
  const [open, setOpen] = useState(false)
  const st = STATUS[ev.status]

  const detailQuery = useQuery({
    queryKey: ["caixinha", "event", ev.id],
    queryFn:  () => caixinhaApi.events.detail(ev.id),
    enabled:  open,
  })

  const joinMut = useMutation({
    mutationFn: () => caixinhaApi.events.join(ev.id),
    onSuccess: () => { toast.success("Sua caixinha entrou no evento! 🏆"); qc.invalidateQueries({ queryKey: ["caixinha", "event", ev.id] }); onJoined() },
    onError:   () => toast.error("Não foi possível participar."),
  })

  const canJoin = ev.status === "active" && canLead && !ev.myCaixinhaJoined

  return (
    <div className="rounded-lg border border-border bg-popover/40">
      <div className="flex items-center gap-3 px-3 py-2.5">
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <span className="font-semibold truncate">{ev.name}</span>
            <span className={cn("text-[10px] px-1.5 py-0.5 rounded-full border font-bold uppercase tracking-wide", st.cls)}>{st.label}</span>
            {ev.myCaixinhaJoined && <span className="text-[10px] text-success font-semibold">✓ participando</span>}
          </div>
          <p className="text-xs text-muted-foreground flex items-center gap-2 mt-0.5">
            {ev.theme && <span className="text-primary/80">{ev.theme}</span>}
            <span className="inline-flex items-center gap-1"><CalendarDays className="h-3 w-3" />{ev.startsAt}–{ev.endsAt}</span>
            <span>· {ev.participantCount} caixinhas</span>
          </p>
        </div>
        {canJoin && (
          <Button size="sm" onClick={() => joinMut.mutate()} disabled={joinMut.isPending}>Participar</Button>
        )}
        <Button variant="ghost" size="icon" onClick={() => setOpen((o) => !o)} title="Ranking">
          <ChevronDown className={cn("h-4 w-4 transition-transform", open && "rotate-180")} />
        </Button>
      </div>

      {open && (
        <div className="border-t border-border px-3 py-2 space-y-1.5">
          {detailQuery.isLoading && <Skeleton className="h-10" />}
          {detailQuery.data && detailQuery.data.ranking.length === 0 && (
            <p className="text-xs text-muted-foreground text-center py-2">Nenhuma caixinha participando ainda.</p>
          )}
          {(detailQuery.data?.ranking ?? []).map((r) => (
            <div key={r.caixinhaId} className={cn("flex items-center gap-2 rounded-md px-2 py-1.5 text-sm border",
              r.isMine ? "border-primary bg-primary/10" : "border-transparent")}>
              <span className={cn("w-5 text-center font-display font-extrabold",
                r.rank === 1 ? "text-warning" : "text-muted-foreground/70")}>{r.rank}</span>
              <span className="text-lg">{r.emblem}</span>
              <span className="flex-1 min-w-0 truncate font-medium">{r.name}</span>
              <span className="inline-flex items-center gap-1 text-muted-foreground"><Trophy className="h-3 w-3" />{r.points}</span>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

function CreateEventForm({ onCreated }: { onCreated: () => void }) {
  const [name, setName]   = useState("")
  const [theme, setTheme] = useState("")
  const [days, setDays]   = useState(5)

  const createMut = useMutation({
    mutationFn: () => {
      const startsAt = new Date().toISOString()
      const endsAt   = new Date(Date.now() + days * 86_400_000).toISOString()
      return caixinhaApi.events.create({ name: name.trim(), theme: theme.trim() || undefined, startsAt, endsAt })
    },
    onSuccess: () => { toast.success("Evento criado!"); setName(""); setTheme(""); onCreated() },
    onError:   () => toast.error("Falha ao criar evento."),
  })

  return (
    <div className="rounded-lg border border-dashed border-primary/40 p-3 space-y-2">
      <p className="text-xs font-semibold text-primary flex items-center gap-1"><Plus className="h-3.5 w-3.5" /> Novo evento (moderador)</p>
      <div className="flex flex-wrap gap-2">
        <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="Nome (ex: Semana de Backend)" className="flex-1 min-w-[180px]" maxLength={80} />
        <Input value={theme} onChange={(e) => setTheme(e.target.value)} placeholder="Tema (opcional)" className="flex-1 min-w-[140px]" maxLength={120} />
        <div className="flex items-center gap-1">
          <Input type="number" value={days} min={1} max={7} onChange={(e) => setDays(Math.min(7, Math.max(1, Number(e.target.value) || 1)))} className="w-16" />
          <span className="text-xs text-muted-foreground">dias</span>
        </div>
        <Button size="sm" onClick={() => createMut.mutate()} disabled={name.trim().length < 2 || createMut.isPending}>Criar</Button>
      </div>
    </div>
  )
}
