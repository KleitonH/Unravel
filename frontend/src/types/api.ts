// PR 22 — Tipos TS espelhando DTOs do backend Unravel.
// Mantemos um arquivo único para reduzir overhead de imports.

// ── Auth ────────────────────────────────────────────────────────────

export type User = {
  id: string
  name: string
  email: string
  isActive: boolean
  createdAt: string
}

export type AuthResponse = {
  accessToken: string
  refreshToken: string
  expiresAt: string
  user: User
}

export type LoginRequest    = { email: string; password: string }
export type RegisterRequest = { name: string; email: string; password: string }

// ── Trails ──────────────────────────────────────────────────────────

export type Trail = {
  id: number
  name: string
  description: string
  icon: string
  accentColor: string
  level: string
  totalContents: number
  userProgress: number  // -1 = não inscrito
}

// ── Journey (PR 3) ──────────────────────────────────────────────────

export type JourneyReason = "NewLearning" | "DueReview" | "Reinforce"

export type JourneyItem = {
  topicId: number
  contentId: number
  slug: string
  title: string
  reason: JourneyReason
  priority: number
  effectiveMastery: number
  difficultyScore: number
}

export type JourneyPlan = {
  userId: string
  trailId: number
  trailName: string
  generatedAt: string
  metaDia: number
  today: JourneyItem[]
  upcoming: JourneyItem[]
}

// ── Onboarding (PR 6) ───────────────────────────────────────────────

export type LevelingQuestion = {
  topicId: number
  contentId: number
  contentTitle: string
  strategy: string
  prompt: string
  options: string[]
  difficultyTarget: number
}

export type LevelingTrailGroup = {
  trailId: number
  trailName: string
  questions: LevelingQuestion[]
}

export type OnboardingTest = { trails: LevelingTrailGroup[] }

export type LevelingAnswer = { topicId: number; selectedOptionIndex: number }
export type OnboardingSubmit = { answers: LevelingAnswer[] }

export type TrailLevelEstimate = {
  trailId: number
  trailName: string
  estimatedMastery: number
  label: "Iniciante" | "Intermediário" | "Avançado"
}

export type OnboardingResult = {
  estimates: TrailLevelEstimate[]
  enrolledTrailIds: number[]
}

// ── Challenge Pool (PR 4 + PR 13) ───────────────────────────────────

export type ChallengeStrategy =
  | "Cloze" | "Definition" | "TrueFalse"
  | "Ordering" | "Match" | "Code"
  | "LlmGrounded"

export type PoolChallenge = {
  id: number
  strategy: ChallengeStrategy
  prompt: string
  options: string[]
  correctIndex: number       // exposto p/ fallback offline
  explanation: string | null
  estimatedDifficulty: number
  contentId: number          // PR 37: necessário pro reinforcement quiz rotear submit
}

export type ChallengePool = {
  contentId: number
  contentTitle: string
  trailId: number
  targetUserMastery: number
  challenges: PoolChallenge[]
}

export type SubmitPoolChallengeRequest = {
  generatedChallengeId: number
  selectedOptionIndex: number
}

export type SubmitPoolChallengeResponse = {
  isCorrect: boolean
  correctOptionIndex: number
  explanation: string | null
  newMasteryScore: number
  newMasteryConfidence: number
  // PR 15 gamificação
  xpEarned: number
  coinsEarned: number
  starsEarned: number
  lifeDelta: number
  totalXp: number
  totalCoins: number
  totalStars: number
  totalLives: number
  streakDays: number
}

// ── Reinforcement quiz (PR 37) ──────────────────────────────────────

export type WeakTopic = {
  topicId:            number
  topicSlug:          string
  effectiveMastery:   number   // [0,1] — quanto mais baixo, mais fraco
  questionsAvailable: number   // pool fresco específico desse topic pro user
}

export type ReinforcementQuiz = {
  trailId:      number
  weakTopics:   WeakTopic[]
  challenges:   PoolChallenge[]
  moreComing:   boolean        // true se backend disparou jobs de geração
  jobsEnqueued: number
  reason:       string | null  // "no_weaknesses" | "pool_exhausted" | "no_content_for_weakness" | null
}

// ── Admin (PR 7/10) ─────────────────────────────────────────────────

export type DailyReplanReport = {
  asOf: string
  processed: number
  failures: number
  yesterdayGoalMet: number
}

// ── Profile (PR 26) ─────────────────────────────────────────────────
// Espelha ProfileController do backend. Student/Moderator divergem;
// discriminamos por `role` (string vinda do enum .NET).

export type ProfileBadge = {
  id: number
  name: string
  description: string
  icon: string
  category: string
  earnedAt: string   // já formatado "dd/MM/yyyy" pelo backend
}

export type ProfileCosmetic = {
  id: number
  name: string
  type: string
  rarity: string
  isEquipped: boolean
}

export type ProfileTrailProgress = {
  trailId: number
  trailName: string
  progress: number
  isCompleted: boolean
}

export type StudentProfile = {
  id: string
  name: string
  email: string
  role: "Student"
  xp: number
  coins: number
  stars: number
  lives: number
  streakDays: number
  longestStreak: number
  loginCycleDay: number
  activeTitle: string | null
  badges: ProfileBadge[]
  cosmetics: ProfileCosmetic[]
  trailProgress: ProfileTrailProgress[]
}

export type PlatformMetrics = {
  totalStudents: number
  totalTrails: number
  totalContents: number
  totalChallenges: number
  totalXpDistributed: number
}

export type ModeratorProfile = {
  id: string
  name: string
  email: string
  role: "Moderator"
  metrics: PlatformMetrics
  trails: Array<{ id: number; name: string; contentCount: number; challengeCount: number; enrolledCount: number }>
}

export type Profile = StudentProfile | ModeratorProfile

export type RankingEntry = {
  id: string
  name: string
  xp: number
  streakDays: number
  activeTitle: string | null
  badgeCount: number
}

// ── SignalR events (PR 8) ───────────────────────────────────────────

export type DailyPlanGeneratedEvent = {
  userId: string
  trailId: number
  planDate: string
  metaDia: number
  extraPenalty: number
  metGoalYesterday: boolean | null
}

export type StreakResetEvent = {
  userId: string
  previousStreak: number
  resetAt: string
}
