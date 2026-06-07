/**
 * Types pros endpoints de gold-set curado por moderador (PR 33d + PR 56-a).
 *
 * Gold = pergunta curada que o moderador considera "padrão ouro" pro
 * conteúdo dele. Complementa as perguntas geradas pela IA: usado pra
 * avaliar yield do pipeline (ForgeEvaluator) e como base manual quando
 * o moderador quer questões com fraseado/distratores próprios.
 *
 * **Duas origens** (`sourceGeneratedChallengeId` distingue):
 * - **null**: item manual — moderador escreveu do zero
 * - **número**: promovido — copiado de uma `GeneratedChallenge` específica
 */

export type ModeratorGoldItem = {
  id:                          number
  sourceGeneratedChallengeId:  number | null
  sourceClaim:                 string | null
  prompt:                      string
  correctAnswer:               string
  /** JSON string — array de 3 strings. Parse via JSON.parse antes de usar. */
  distractorsJson:             string
  explanation:                 string | null
  difficultyHint:              number | null
  createdAt:                   string
}

export type CreateGoldRequest = {
  /** Modo "promover gerada": passe sourceGeneratedChallengeId; demais
   * campos são copiados da challenge fonte. */
  sourceGeneratedChallengeId?: number
  /** Modo manual: obrigatórios. */
  prompt?:                     string
  correctAnswer?:              string
  /** Exatamente 3 distratores não-vazios distintos. */
  distractors?:                string[]
  /** Opcionais em ambos modos. */
  sourceClaim?:                string
  explanation?:                string
  difficultyHint?:             number
}

export type UpdateGoldRequest = {
  prompt:         string
  correctAnswer:  string
  distractors:    string[]      // 3 itens
  explanation?:   string
  sourceClaim?:   string
  difficultyHint?: number
}
