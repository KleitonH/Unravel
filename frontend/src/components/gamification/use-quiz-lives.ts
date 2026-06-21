import { useState } from "react"
import { useQuery } from "@tanstack/react-query"
import { profileApi } from "@/api/profile"

/**
 * Estado de vidas durante um quiz.
 *
 * A semente vem do `profile.me` (vidas atuais do aluno); cada resposta
 * atualiza via `applyTotalLives(r.totalLives)` — o servidor é a fonte da
 * verdade (clamp 0..MaxLives). `gameOver` fica `true` quando as vidas zeram,
 * seja entrando já sem vidas, seja zerando no meio do quiz; nesse caso a
 * tela deve bloquear novas respostas e mostrar <OutOfLivesCard />.
 *
 * `ready` indica que já sabemos o valor (profile carregado ou primeiro
 * submit), pra não piscar "0 vidas" enquanto o profile carrega.
 */
export function useQuizLives() {
  const profileQuery = useQuery({
    queryKey: ["profile", "me"],
    queryFn:  profileApi.me,
    staleTime: 60_000,
  })

  const [livesState, setLivesState] = useState<number | null>(null)

  const seeded =
    profileQuery.data && profileQuery.data.role === "Student"
      ? profileQuery.data.lives
      : null

  const lives = livesState ?? seeded
  const ready = lives !== null
  const gameOver = ready && (lives as number) <= 0

  function applyTotalLives(totalLives: number) {
    setLivesState(totalLives)
  }

  return { lives, ready, gameOver, applyTotalLives }
}
