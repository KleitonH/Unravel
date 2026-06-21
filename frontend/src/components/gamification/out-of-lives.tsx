import { HeartCrack } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"

/**
 * Tela de "sem vidas" — mostrada quando o aluno zera as vidas no meio do
 * quiz (ou entra já sem nenhuma). Bloqueia novas respostas e oferece a
 * saída. As vidas voltam aos poucos no login dos próximos dias
 * (ChallengeService.ProcessDailyLoginAsync: +1 por dia nos dias 1-3 do ciclo).
 */
export function OutOfLivesCard({
  onLeave,
  leaveLabel = "Voltar",
}: {
  onLeave: () => void
  leaveLabel?: string
}) {
  return (
    <Card className="border-destructive/40 bg-destructive/5 text-center">
      <CardContent className="pt-10 pb-10 space-y-3">
        <HeartCrack className="h-14 w-14 text-destructive mx-auto" />
        <h2 className="font-display text-2xl font-extrabold">Você ficou sem vidas 💔</h2>
        <p className="text-sm text-muted-foreground max-w-md mx-auto">
          Cada erro custa uma vida. Por hoje o quiz para por aqui — suas vidas
          voltam aos poucos quando você estuda nos próximos dias. Entre amanhã
          pra recuperar e continuar de onde parou.
        </p>
        <div className="pt-2">
          <Button onClick={onLeave}>{leaveLabel}</Button>
        </div>
      </CardContent>
    </Card>
  )
}
