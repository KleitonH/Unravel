// PR 52 — Tokens "centímetros de lã" do moderador

export type YarnTier = "Empty" | "Tiny" | "Small" | "Medium" | "Giant"

export type TokenBalance = {
  balanceCm:                    number
  tier:                         YarnTier
  displayBalance:               string   // "1m 87cm" ou "87 cm"
  estimatedQuestionsRemaining:  number   // balanceCm * 0.69 (yield)
}

export type TokenTransaction = {
  id:        number
  deltaCm:   number
  reason:    string    // ex "ForgeNormal", "WelcomeBonus", ...
  metadata:  string | null
  createdAt: string    // ISO
}
