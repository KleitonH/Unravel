import { useQuery } from "@tanstack/react-query"
import { Link } from "@tanstack/react-router"
import { Coins, Flame, Heart, Sparkles, Star, Trophy } from "lucide-react"
import { profileApi } from "@/api/profile"
import { MyTurmasSection } from "@/features/turmas/my-turmas-section"
import { TitlesSection, GlobalRankingSection } from "@/features/titles/titles-section"
import { UserNavi } from "@/components/navi/user-navi"
import { useAuth } from "@/stores/auth"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Separator } from "@/components/ui/separator"
import { Skeleton } from "@/components/ui/skeleton"
import type { StudentProfile } from "@/types/api"

/**
 * Página de perfil (PR 26 — agora com dados reais).
 *
 * Student: stats (xp/coins/streak/lives), trilhas com progresso,
 * badges conquistados.
 *
 * Moderator: métricas globais da plataforma.
 */
export function ProfilePage() {
  const user        = useAuth((s) => s.user)
  const role        = useAuth((s) => s.role)
  const isModerator = useAuth((s) => s.isModerator())
  const logout      = useAuth((s) => s.logout)

  const profileQuery = useQuery({
    queryKey: ["profile", "me"],
    queryFn:  profileApi.me,
    staleTime: 60_000,
  })

  const isStudent = profileQuery.data?.role === "Student"
  const s = isStudent ? (profileQuery.data as StudentProfile) : null

  return (
    <div className="p-6 lg:p-10 max-w-3xl space-y-6">
      <header>
        <h1 className="text-3xl font-display font-extrabold tracking-tight">👤 Perfil</h1>
      </header>

      <Card>
        <CardHeader className="flex-row items-center gap-4">
          <Avatar className="h-16 w-16 text-2xl">
            <AvatarFallback>{initials(user?.name)}</AvatarFallback>
          </Avatar>
          <div className="flex-1 min-w-0">
            <CardTitle className="text-xl truncate">
              {user?.name ?? "—"}
              {s?.activeTitle && (
                <span className="ml-2 text-xs font-medium text-primary/80 italic">
                  · {s.activeTitle}
                </span>
              )}
            </CardTitle>
            <CardDescription className="truncate">{user?.email}</CardDescription>
            <p className="text-xs text-muted-foreground mt-1">
              Role: <span className={isModerator ? "text-primary font-medium" : ""}>{role ?? "—"}</span>
            </p>
          </div>
        </CardHeader>
        <CardContent className="flex gap-2">
          <Button variant="destructive" onClick={logout}>Sair</Button>
        </CardContent>
      </Card>

      {/* NAVI do usuário — espaço da personalização ativa (Student only) */}
      {s && (
        <Card className="animate-pop-in" style={{ animationDelay: "30ms" }}>
          <CardHeader className="flex-row items-center justify-between">
            <CardTitle className="text-sm font-display font-bold uppercase tracking-wider text-muted-foreground">
              Seu NAVI
            </CardTitle>
            <Button asChild variant="outline" size="sm">
              <Link to="/loja">Personalizar</Link>
            </Button>
          </CardHeader>
          <CardContent className="flex justify-center py-2">
            <UserNavi size={168} mood="happy" />
          </CardContent>
        </Card>
      )}

      {/* Stats — Student only */}
      {profileQuery.isLoading && <Skeleton className="h-32" />}
      {s && (
        <Card className="animate-pop-in" style={{ animationDelay: "60ms" }}>
          <CardHeader>
            <CardTitle className="text-sm font-display font-bold uppercase tracking-wider text-muted-foreground">
              Estatísticas
            </CardTitle>
          </CardHeader>
          <CardContent className="grid grid-cols-2 sm:grid-cols-4 gap-3">
            <StatBig icon={<Star  />} value={s.xp}            label="XP total" />
            <StatBig icon={<Flame />} value={s.streakDays}    label={`sequência (max ${s.longestStreak})`} />
            <StatBig icon={<Heart />} value={s.lives}         label="vidas" />
            <StatBig icon={<Coins />} value={s.coins}         label="moedas" />
          </CardContent>
        </Card>
      )}

      {/* Minhas turmas (aluno) */}
      {s && <MyTurmasSection />}

      {/* Trilhas */}
      {s && s.trailProgress.length > 0 && (
        <Card className="animate-pop-in" style={{ animationDelay: "120ms" }}>
          <CardHeader>
            <CardTitle className="text-sm font-display font-bold uppercase tracking-wider text-muted-foreground">
              Suas trilhas ({s.trailProgress.length})
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {s.trailProgress.map((t) => (
              <div key={t.trailId} className="flex items-center gap-3 rounded-md border border-border bg-popover/40 px-3 py-2">
                <span className="flex-1 text-sm font-medium truncate">{t.trailName}</span>
                <Badge variant={t.isCompleted ? "default" : "outline"} className="text-[10px]">
                  {t.progress}%{t.isCompleted && " ✓"}
                </Badge>
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      {/* Badges */}
      {s && s.badges.length > 0 && (
        <Card className="animate-pop-in" style={{ animationDelay: "180ms" }}>
          <CardHeader>
            <CardTitle className="text-sm font-display font-bold uppercase tracking-wider text-muted-foreground flex items-center gap-2">
              <Trophy className="h-4 w-4" />Conquistas ({s.badges.length})
            </CardTitle>
          </CardHeader>
          <CardContent>
            <ul className="grid grid-cols-1 sm:grid-cols-2 gap-2">
              {s.badges.map((b) => (
                <li key={b.id} className="flex items-start gap-2 rounded-md border border-border bg-popover/40 px-3 py-2">
                  <span className="text-2xl leading-none">{b.icon}</span>
                  <div className="min-w-0">
                    <p className="text-sm font-semibold truncate">{b.name}</p>
                    <p className="text-xs text-muted-foreground line-clamp-2">{b.description}</p>
                    <p className="text-[10px] text-muted-foreground/80 mt-0.5">Em {b.earnedAt}</p>
                  </div>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}

      {/* Títulos + ranking global — Student only */}
      {s && <TitlesSection />}
      {s && <GlobalRankingSection />}

      {/* Moderator panel */}
      {profileQuery.data?.role === "Moderator" && (
        <Card>
          <CardHeader>
            <CardTitle className="text-sm font-display font-bold uppercase tracking-wider text-muted-foreground flex items-center gap-2">
              <Sparkles className="h-4 w-4" />Métricas globais
            </CardTitle>
          </CardHeader>
          <CardContent className="grid grid-cols-2 sm:grid-cols-3 gap-3">
            <StatBig icon={<Star />} value={profileQuery.data.metrics.totalStudents}      label="alunos" />
            <StatBig icon={<Star />} value={profileQuery.data.metrics.totalTrails}        label="trilhas" />
            <StatBig icon={<Star />} value={profileQuery.data.metrics.totalContents}      label="conteúdos" />
            <StatBig icon={<Star />} value={profileQuery.data.metrics.totalChallenges}    label="desafios" />
            <StatBig icon={<Star />} value={profileQuery.data.metrics.totalXpDistributed} label="XP distribuído" />
          </CardContent>
          <Separator />
        </Card>
      )}
    </div>
  )
}

function StatBig({ icon, value, label }: { icon: React.ReactNode; value: number; label: string }) {
  return (
    <div className="flex flex-col items-start rounded-md border border-border bg-popover/40 p-3">
      <div className="flex items-center gap-1.5 text-primary mb-0.5">
        <span className="[&_svg]:h-4 [&_svg]:w-4">{icon}</span>
        <strong className="font-display text-xl">{fmt(value)}</strong>
      </div>
      <span className="text-[10px] uppercase tracking-wider text-muted-foreground">{label}</span>
    </div>
  )
}

function fmt(n: number): string {
  if (n < 1_000)     return n.toString()
  if (n < 1_000_000) return `${(n / 1_000).toFixed(n < 10_000 ? 1 : 0).replace(/\.0$/, "")}k`
  return `${(n / 1_000_000).toFixed(1).replace(/\.0$/, "")}M`
}

function initials(name?: string | null) {
  if (!name) return "?"
  return name.trim().split(/\s+/).map((p) => p[0]?.toUpperCase() ?? "").slice(0, 2).join("")
}
