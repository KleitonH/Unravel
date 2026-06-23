import { HeartCrack } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { LifeRefillTimer } from "./life-refill-timer"

/**
 * Tela de "sem vidas" — mostrada quando o aluno zera as vidas no meio do
 * quiz (ou entra já sem nenhuma). Bloqueia novas respostas e oferece a saída.
 * As vidas se regeneram com o tempo (+1/h até o teto — UC34); o contador
 * mostra quanto falta pra próxima, e ao zerar o profile é revalidado, então
 * o quiz pode recuperar sozinho assim que voltar uma vida.
 */
export function OutOfLivesCard({
  onLeave,
  leaveLabel = "Voltar",
  secondsToNextLife = null,
}: {
  onLeave: () => void
  leaveLabel?: string
  secondsToNextLife?: number | null
}) {
  return (
    <Card className="border-destructive/40 bg-destructive/5 text-center">
      <CardContent className="pt-10 pb-10 space-y-3">
        <HeartCrack className="h-14 w-14 text-destructive mx-auto" />
        <h2 className="font-display text-2xl font-extrabold">Você ficou sem vidas 💔</h2>
        <p className="text-sm text-muted-foreground max-w-md mx-auto">
          Cada erro custa uma vida. Suas vidas voltam sozinhas com o tempo —
          uma a cada hora. Você pode esperar a próxima ou voltar e continuar
          depois.
        </p>
        {secondsToNextLife !== null && (
          <div className="flex justify-center pt-1">
            <span className="inline-flex items-center gap-2 rounded-full border border-border bg-popover/60 px-3 py-1.5">
              <span className="text-sm font-medium text-muted-foreground">Próxima vida:</span>
              <LifeRefillTimer seconds={secondsToNextLife} showLabel={false} className="text-foreground" />
            </span>
          </div>
        )}
        <div className="pt-2">
          <Button onClick={onLeave}>{leaveLabel}</Button>
        </div>
      </CardContent>
    </Card>
  )
}
