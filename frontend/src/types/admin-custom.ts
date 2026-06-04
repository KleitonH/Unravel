// PR 36 — DTOs espelhando os endpoints /api/admin de PR 35.
// Mantidos em arquivo separado de api.ts pra isolar superfície de admin
// (que não é consumida pelo fluxo de aluno).

export type DifficultyLevelInt = 1 | 2 | 3  // Beginner | Intermediate | Advanced

export type CustomTrailDto = {
  id:            number
  slug:          string | null
  name:          string
  description:   string
  icon:          string
  accentColor:   string
  level:         DifficultyLevelInt
  isActive:      boolean
  isPublished:   boolean
  createdAt:     string
  contentsCount: number
}

export type CreateCustomTrailRequest = {
  name:         string
  slug?:        string
  description?: string
  icon?:        string
  accentColor?: string
  level?:       "Beginner" | "Intermediate" | "Advanced"
}

export type UpdateCustomTrailRequest = {
  name?:         string
  description?:  string
  icon?:         string
  accentColor?:  string
  level?:        "Beginner" | "Intermediate" | "Advanced"
  isPublished?:  boolean
}

export type CustomContentDto = {
  id:        number
  slug:      string | null
  title:     string
  body:      string
  order:     number
  level:     DifficultyLevelInt
  isActive:  boolean
  createdAt: string
  editedAt:  string | null
}

export type CreateCustomContentRequest = {
  title:   string
  body:    string
  slug?:   string
  order?:  number
  level?:  "Beginner" | "Intermediate" | "Advanced"
}

export type UpdateCustomContentRequest = {
  title?:    string
  body?:     string
  order?:    number
  level?:    "Beginner" | "Intermediate" | "Advanced"
  isActive?: boolean
}

// Resposta do POST /forge/{contentId} — espelha o ResponseObject do PR 32
export type ForgeEnqueueResponse = {
  contentId:         number
  contentTitle:      string
  claimsCandidates?: number
  enqueued:          number
  message?:          string
}
