import { useEffect, useState } from "react"
import { useQueryClient } from "@tanstack/react-query"
import { Hourglass } from "lucide-react"
import { cn } from "@/lib/utils"

function fmt(total: number) {
  const m = Math.floor(total / 60)
  const s = total % 60
  return `${m}:${String(s).padStart(2, "0")}`
}

/**
 * UC34 — contador "+1 vida em mm:ss". Recebe os segundos do servidor (já com o
 * refill lazy aplicado no GET /api/profile) e conta localmente de 1 em 1s. Ao
 * zerar, invalida o profile pra puxar o novo total de vidas + o próximo
 * intervalo. Não renderiza nada quando o aluno está no teto (seconds = null).
 */
export function LifeRefillTimer({
  seconds,
  className,
  showLabel = true,
}: {
  seconds: number | null
  className?: string
  showLabel?: boolean
}) {
  const qc = useQueryClient()
  const [remaining, setRemaining] = useState(seconds ?? 0)

  useEffect(() => {
    if (seconds === null) return
    setRemaining(seconds)

    if (seconds <= 0) {
      qc.invalidateQueries({ queryKey: ["profile", "me"] })
      return
    }

    const id = setInterval(() => {
      setRemaining((r) => {
        const next = r - 1
        if (next <= 0) {
          clearInterval(id)
          qc.invalidateQueries({ queryKey: ["profile", "me"] }) // puxa a vida nova
          return 0
        }
        return next
      })
    }, 1000)

    return () => clearInterval(id)
  }, [seconds, qc])

  if (seconds === null) return null

  return (
    <span
      className={cn(
        "inline-flex shrink-0 items-center gap-1 text-xs font-medium tabular-nums text-muted-foreground",
        className,
      )}
      title="Tempo até recuperar +1 vida"
      aria-label={`Próxima vida em ${fmt(remaining)}`}
    >
      <Hourglass className="h-3 w-3" />
      {showLabel ? <>+1 em {fmt(remaining)}</> : fmt(remaining)}
    </span>
  )
}
