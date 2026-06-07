import { useQuery } from "@tanstack/react-query"
import { Award, Crown, Gift, Sparkles, TrendingUp, Users, X } from "lucide-react"
import { tokensApi } from "@/api/tokens"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import {
  Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle,
} from "@/components/ui/dialog"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"
import { YarnBall } from "./yarn-ball"
import type { TokenTransaction, YarnTier } from "@/types/tokens"

/**
 * PR 52 — Modal expandido do novelo. Mostra:
 * 1. Novelo XL animado (mesmo SVG, size grande)
 * 2. Saldo formatado + estimativa de perguntas
 * 3. Próximo refill mensal
 * 4. Histórico recente de transações
 * 5. Cards "Como ganhar mais cm" — gatilhos/ações pendentes
 */
export function YarnBallModal({
  tier, balanceCm, displayBalance, onClose,
}: {
  tier: YarnTier
  balanceCm: number
  displayBalance: string
  onClose: () => void
}) {
  const historyQuery = useQuery({
    queryKey: ["tokens", "history"],
    queryFn:  () => tokensApi.history(15),
    staleTime: 30_000,
  })

  const estimatedQuestions = Math.round(balanceCm * 0.69)

  return (
    <Dialog open onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Sparkles className="h-5 w-5 text-warning" />
            Seu novelo de lã
          </DialogTitle>
          <DialogDescription>
            Use centímetros pra gerar perguntas com IA. Ganhe mais quando alunos
            engajarem com seu conteúdo.
          </DialogDescription>
        </DialogHeader>

        {/* Cabeçalho com novelo XL */}
        <div className="flex flex-col items-center text-center py-4 space-y-3">
          <YarnBall
            tier={tier}
            balanceCm={balanceCm}
            displayBalance={displayBalance}
            size="xl"
            interactive={false}
          />
          <div>
            <p className={cn(
              "font-display font-extrabold text-4xl",
              tier === "Giant" && "text-warning",
              tier === "Empty" && "text-muted-foreground",
            )}>
              {displayBalance}
            </p>
            <p className="text-sm text-muted-foreground mt-1">
              ≈ <strong>{estimatedQuestions}</strong> perguntas restantes
              <span className="text-[11px] opacity-70 ml-1">(yield ~69%)</span>
            </p>
          </div>
          <TierBadge tier={tier} />
        </div>

        {/* Ações pra ganhar mais lã */}
        <section className="space-y-2">
          <h3 className="text-xs font-display font-bold uppercase tracking-wider text-muted-foreground">
            Como ganhar mais
          </h3>
          <div className="grid gap-2 sm:grid-cols-2">
            <EarnCard icon={<Users className="h-4 w-4" />} amount="+1 cm"
              title="Aluno conclui ilha" desc="Por aluno que termina conteúdo seu" />
            <EarnCard icon={<Sparkles className="h-4 w-4" />} amount="+2 cm"
              title="Aluno vota 👍" desc="Por marcação de pergunta excelente" />
            <EarnCard icon={<Crown className="h-4 w-4" />} amount="+5 cm"
              title="Boss vencido" desc="Por aluno que vence o desafio final" />
            <EarnCard icon={<Award className="h-4 w-4" />} amount="+20 cm"
              title="Pergunta vira Gold" desc="Quando você promove pra gold set" />
            <EarnCard icon={<TrendingUp className="h-4 w-4" />} amount="+50/+200/+500"
              title="Milestones de trilha" desc="10, 50 ou 100 alunos inscritos" />
            <EarnCard icon={<Gift className="h-4 w-4" />} amount="+500 cm"
              title="Refill mensal" desc="Bônus base todo mês" />
          </div>
          <p className="text-[11px] text-muted-foreground text-center pt-1">
            Sistema de eventos chega no PR 53 — por enquanto o saldo é alimentado
            apenas pelo welcome bonus e ajustes manuais.
          </p>
        </section>

        {/* Histórico recente */}
        <section className="space-y-2">
          <h3 className="text-xs font-display font-bold uppercase tracking-wider text-muted-foreground">
            Últimas movimentações
          </h3>
          {historyQuery.isLoading
            ? <>{[1, 2, 3].map((i) => <Skeleton key={i} className="h-10" />)}</>
            : historyQuery.data && historyQuery.data.length > 0
              ? (
                <ul className="space-y-1 max-h-48 overflow-y-auto">
                  {historyQuery.data.map((t) => <HistoryRow key={t.id} t={t} />)}
                </ul>
              )
              : <p className="text-xs text-muted-foreground text-center py-3">
                  Sem movimentações ainda.
                </p>}
        </section>

        <div className="flex justify-end gap-2 pt-2">
          <Button variant="outline" size="sm" onClick={onClose}>
            <X className="h-3.5 w-3.5 mr-1" />Fechar
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  )
}

function TierBadge({ tier }: { tier: YarnTier }) {
  const meta: Record<YarnTier, { label: string; cls: string }> = {
    Empty:  { label: "Vazio",   cls: "border-border text-muted-foreground" },
    Tiny:   { label: "Pequeno", cls: "border-border" },
    Small:  { label: "Médio",   cls: "border-warning/40 text-warning" },
    Medium: { label: "Cheio",   cls: "border-primary/40 text-primary" },
    Giant:  { label: "Gigante", cls: "border-warning text-warning bg-warning/10" },
  }
  return <Badge variant="outline" className={cn("text-[10px]", meta[tier].cls)}>{meta[tier].label}</Badge>
}

function EarnCard({ icon, title, desc, amount }: {
  icon: React.ReactNode; title: string; desc: string; amount: string
}) {
  return (
    <div className="rounded-md border border-border bg-popover/40 p-3">
      <div className="flex items-start justify-between gap-2">
        <div className="text-primary mt-0.5">{icon}</div>
        <Badge variant="outline" className="text-[10px] border-success/40 text-success">
          {amount}
        </Badge>
      </div>
      <p className="font-display font-semibold text-sm mt-2">{title}</p>
      <p className="text-[11px] text-muted-foreground">{desc}</p>
    </div>
  )
}

function HistoryRow({ t }: { t: TokenTransaction }) {
  const isCredit = t.deltaCm > 0
  const ago      = fmtAgo(t.createdAt)
  return (
    <li className={cn(
      "flex items-center justify-between gap-2 rounded-md border px-2.5 py-1.5 text-xs",
      isCredit
        ? "border-success/30 bg-success/5"
        : "border-destructive/30 bg-destructive/5",
    )}>
      <div className="min-w-0 flex-1">
        <p className="font-medium truncate">{reasonLabel(t.reason)}</p>
        <p className="text-[10px] text-muted-foreground">{ago}</p>
      </div>
      <span className={cn(
        "font-bold tabular-nums",
        isCredit ? "text-success" : "text-destructive",
      )}>
        {isCredit ? "+" : ""}{t.deltaCm} cm
      </span>
    </li>
  )
}

function reasonLabel(reason: string): string {
  const map: Record<string, string> = {
    WelcomeBonus:           "🎉 Bem-vindo, moderador!",
    MonthlyRefill:          "🌙 Refill mensal",
    StudentCompletedIsland: "🏝️ Aluno completou ilha",
    StudentVotedGood:       "👍 Aluno votou na pergunta",
    BossFightWon:           "👑 Boss vencido",
    QuestionPromotedToGold: "⭐ Pergunta virou gold",
    TrailMilestone10:       "🎯 Trilha alcançou 10 alunos",
    TrailMilestone50:       "🎯 Trilha alcançou 50 alunos",
    TrailMilestone100:      "🎯 Trilha alcançou 100 alunos",
    ForgeNormal:            "⚡ Geração de perguntas",
    ForgeUrgent:            "⚡ Geração urgent",
    PromoteToGold:          "⭐ Promoveu pergunta a gold",
    AdminGrant:             "🛠 Ajuste manual (crédito)",
    AdminRevoke:            "🛠 Ajuste manual (débito)",
  }
  return map[reason] ?? reason
}

function fmtAgo(iso: string): string {
  const ms = Date.now() - new Date(iso).getTime()
  const min = Math.floor(ms / 60_000)
  if (min < 1)  return "agora"
  if (min < 60) return `há ${min}min`
  const hr = Math.floor(min / 60)
  if (hr < 24)  return `há ${hr}h`
  const day = Math.floor(hr / 24)
  if (day < 7) return `há ${day}d`
  return new Date(iso).toLocaleDateString("pt-BR")
}
