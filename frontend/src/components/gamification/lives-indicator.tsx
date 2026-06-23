import { Heart, HeartCrack } from "lucide-react"
import { cn } from "@/lib/utils"

/**
 * Pílula de vidas reutilizável — coração + número. Fica vermelha e pulsa
 * quando o aluno está com poucas vidas (≤ 2) e mostra coração quebrado
 * quando zera. Usada no Hero do dashboard, no header das trilhas e no
 * percurso das perguntas (quiz/capítulos/adaptativo/reforço) pra deixar
 * SEMPRE visível quantas vidas restam.
 */
export function LivesIndicator({
  lives,
  max = 7,
  size = "md",
  className,
}: {
  lives: number
  max?: number
  size?: "sm" | "md" | "lg"
  className?: string
}) {
  const empty = lives <= 0
  const low = !empty && lives <= 2

  const icon = size === "lg" ? "h-5 w-5" : size === "sm" ? "h-3.5 w-3.5" : "h-4 w-4"
  const text = size === "lg" ? "text-base" : size === "sm" ? "text-xs" : "text-sm"
  const pad = size === "lg" ? "px-3 py-1.5" : "px-2.5 py-1"

  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 rounded-full border font-bold tabular-nums transition-colors",
        empty || low
          ? "border-destructive/50 bg-destructive/10 text-destructive"
          : "border-border bg-popover/60 text-foreground",
        low && "animate-pulse",
        pad,
        text,
        className,
      )}
      title={`${Math.max(lives, 0)} de ${max} vidas`}
      aria-label={`${Math.max(lives, 0)} vidas`}
    >
      {empty ? (
        <HeartCrack className={cn(icon, "text-destructive")} />
      ) : (
        <Heart className={cn(icon, low ? "text-destructive" : "text-accent", "fill-current")} />
      )}
      <span>{Math.max(lives, 0)}</span>
    </span>
  )
}
