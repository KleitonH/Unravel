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
  metaDia: number          // meta efetiva de hoje (já inclui penalidade)
  today: JourneyItem[]
  upcoming: JourneyItem[]
  completedToday?: number   // PR 61 — desafios respondidos hoje nesta trilha
  metaPenalty?: number      // PR 61 — +N na meta por não ter batido ontem
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
  | "ModeratorAuthored"   // PR 60-f: escrita à mão pelo moderador

/**
 * Formato visual da pergunta (PR 34). Decidido no backend pelo
 * `IClaimShapeRouter`; o frontend usa pra escolher renderer.
 *
 * - `MultipleChoice` (default): cards de opção empilhados (UI clássica)
 * - `FillInTheBlank`: prompt com `_____` viram chip inline; opções
 *   como pílulas pequenas
 * - `TrueFalseGrounded`: reservado pro PR 34a-bis; cai em MCQ enquanto
 *   prompt/validators não existem
 *
 * Backend envia o campo no `BodyJson` (`shape`); `PoolChallengeDto`
 * sempre devolve string com fallback "MultipleChoice" pra rows
 * anteriores ao PR 34a (sem migration).
 */
export type QuestionShape =
  | "MultipleChoice"
  | "FillInTheBlank"
  | "TrueFalseGrounded"

// ── Loja cosmética (PR 63) ──────────────────────────────────────────

export type ShopRarity = "common" | "rare" | "epic" | "legendary" | "exclusive"

export type ShopItem = {
  id:           number
  name:         string
  description:  string
  category:     string         // chapeu | acessorio | pelagem | expressao
  slot:         string         // hat | accessory | fur | mood
  assetSlug:    string         // valor consumido pelo NAVI
  rarity:       ShopRarity
  currency:     "coins" | "stars" | null
  price:        number | null
  owned:        boolean
  equipped:     boolean
  lockedReason: string | null
}

export type ShopBalance = { coins: number; stars: number }
export type ShopCatalog = { items: ShopItem[]; balance: ShopBalance }

export type BuyOutcome = "Ok" | "NotFound" | "AlreadyOwned" | "Locked" | "InsufficientFunds"
export type PurchaseResponse = {
  outcome:    BuyOutcome
  cosmeticId: number
  balance:    ShopBalance | null
  currency:   string | null
  price:      number | null
}

export type PoolChallenge = {
  id: number
  strategy: ChallengeStrategy
  prompt: string
  options: string[]
  correctIndex: number       // exposto p/ fallback offline
  explanation: string | null
  estimatedDifficulty: number
  contentId: number          // PR 37: necessário pro reinforcement quiz rotear submit
  shape?: QuestionShape      // PR 34a: opcional; ausente = "MultipleChoice"
}

// ── Adaptive Quiz (PR 42) ───────────────────────────────────────────

export type AdaptiveHistoryItem = {
  challengeId: number
  wasCorrect:  boolean
}

export type AdaptiveStopReason = "MaxReached" | "Converged" | "PoolExhausted"

export type AdaptiveNextResponse = {
  done:              boolean
  stopReason:        AdaptiveStopReason | null
  abilityEstimate:   number           // [0,1] estimativa atual
  question:          PoolChallenge | null
  questionsAnswered: number
}

export type ChallengePool = {
  contentId: number
  contentTitle: string
  trailId: number
  targetUserMastery: number
  challenges: PoolChallenge[]
  moreComing?: boolean   // PR 51 — true se backend enfileirou jobs urgent (pool curto, replenish em andamento)
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
  // PR 40 progressão SMW
  progress: ProgressUpdate | null
}

// ── Mastery Radar (PR 41) ───────────────────────────────────────────

export type MasterySeverity = "Weak" | "Stale" | "Solid"

export type TopicMasteryItem = {
  topicId:        number
  topicSlug:      string
  contentId:      number
  contentTitle:   string
  order:          number
  hasMastery:     boolean
  rawScore:       number       // [0,1] no LastSeenAt
  effectiveScore: number       // [0,1] com decay até agora
  confidence:     number       // n tentativas
  lastSeenAt:     string | null
  nextDueAt:      string | null
  isSrsDue:       boolean
  severity:       MasterySeverity
}

export type TrailMasteryReport = {
  trailId:               number
  trailName:             string
  averageEffectiveScore: number
  weakCount:             number
  srsDueCount:           number
  untouchedCount:        number
  topics:                TopicMasteryItem[]
}

// ── Boss Fight (PR 50) ──────────────────────────────────────────────

export type BossFightStartResponse = {
  trailId:        number
  trailName:      string
  unlocked:       boolean
  lockReason:     string | null
  passThreshold:  number       // ex 7
  totalQuestions: number
  attemptCount:   number
  bestScore:      number
  firstWonAt:     string | null
  questions:      PoolChallenge[]
}

export type BossFightAnswer = {
  challengeId:         number
  selectedOptionIndex: number
}

export type BossFightSubmitRequest = {
  answers: BossFightAnswer[]
}

export type BossFightAnswerOutcome = {
  challengeId:        number
  isCorrect:          boolean
  correctOptionIndex: number
  explanation:        string | null
}

export type BossFightResultResponse = {
  trailId:        number
  score:          number
  totalQuestions: number
  passThreshold:  number
  passed:         boolean
  isFirstWin:     boolean
  xpEarned:       number
  badgeAwarded:   string | null
  outcomes:       BossFightAnswerOutcome[]
}

// ── Trail Map (PR 40) ───────────────────────────────────────────────

export type UserContentStatus = "Locked" | "Available" | "InProgress" | "Completed"

export type TrailMapNode = {
  contentId:           number
  title:               string
  slug:                string | null
  order:               number
  challengesRequired:  number
  challengesCompleted: number
  status:              UserContentStatus
  isRecommended:       boolean   // PR 42b — JourneyPlanner marcou pra hoje
}

export type TrailMap = {
  trailId:   number
  trailName: string
  nodes:     TrailMapNode[]
}

/** PR 40 — progressão retornada no submit. null se backend não calculou
 *  (ex: testes legacy sem o ITrailProgressService). */
export type ProgressUpdate = {
  contentId:             number
  challengesCompleted:   number
  challengesRequired:    number
  justCompleted:         boolean
  nextContentIdUnlocked: number | null
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

// ── Social: Amigos/Parcerias (PR 64) ────────────────────────────────

export type Friend = {
  userId:       string
  name:         string
  xp:           number
  streakDays:   number
  activeTitle:  string | null
  badgeCount:   number
  friendshipId: number
}

export type FriendRequest = {
  friendshipId: number
  userId:       string
  name:         string
  xp:           number
  activeTitle:  string | null
  createdAt:    string
  direction:    "incoming" | "outgoing"
}

export type FriendRequests = {
  incoming: FriendRequest[]
  outgoing: FriendRequest[]
}

export type FriendRelation = "none" | "pending_out" | "pending_in" | "friends" | "blocked"

export type UserSearchResult = {
  userId:         string
  name:           string
  xp:             number
  activeTitle:    string | null
  relationStatus: FriendRelation
}

// ── Social: Caixinha de Gatos / clã (PR 65) ─────────────────────────

export type CaixinhaMember = {
  userId:      string
  name:        string
  xp:          number
  streakDays:  number
  activeToday: boolean
  role:        "Leader" | "Member"
}

export type CaixinhaMessage = {
  id:         number
  userId:     string
  authorName: string
  text:       string
  createdAt:  string
}

export type CaixinhaDetail = {
  id:               number
  name:             string
  emblem:           string
  leaderId:         string
  collectivePoints: number
  memberCount:      number
  activeTodayCount: number
  rank:             number
  myRole:           "Leader" | "Member"
  dailyGoal:        number
  dailyPoints:      number
  goalReachedToday: boolean
  streakDays:       number
  members:          CaixinhaMember[]
  mural:            CaixinhaMessage[]
}

export type CaixinhaSummary = {
  id:               number
  name:             string
  emblem:           string
  memberCount:      number
  collectivePoints: number
  rank:             number
}

export type CaixinhaEventStatus = "upcoming" | "active" | "finished"

export type CaixinhaEvent = {
  id:               number
  name:             string
  theme:            string | null
  startsAt:         string
  endsAt:           string
  status:           CaixinhaEventStatus
  participantCount: number
  myCaixinhaJoined: boolean
}

export type EventRankingEntry = {
  caixinhaId: number
  name:       string
  emblem:     string
  points:     number
  rank:       number
  isMine:     boolean
}

export type CaixinhaEventDetail = {
  event:   CaixinhaEvent
  ranking: EventRankingEntry[]
}

// ── Ligas semanais (PR 66) ──────────────────────────────────────────

export type LeagueMember = {
  userId:   string
  name:     string
  weeklyXp: number
  rank:     number
  isMine:   boolean
}

export type MyLeague = {
  tier:         string
  nextTier:     string | null
  prevTier:     string | null
  weeklyXp:     number
  rank:         number
  size:         number
  promoteZone:  number
  relegateZone: number
  lastResult:   "promoted" | "relegated" | "stayed" | null
  lastRank:     number | null
  weekEndsAt:   string
  leaderboard:  LeagueMember[]
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
