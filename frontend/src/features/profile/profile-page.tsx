import { useAuth } from "@/stores/auth"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"

/**
 * Página de perfil mínima (PR 23). Detalhes ricos (badges, cosméticos,
 * ranking, etc) vêm de /api/profile/me em PR futuro dedicado.
 */
export function ProfilePage() {
  const user        = useAuth((s) => s.user)
  const role        = useAuth((s) => s.role)
  const isModerator = useAuth((s) => s.isModerator())
  const logout      = useAuth((s) => s.logout)

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
          <div className="flex-1">
            <CardTitle className="text-xl">{user?.name ?? "—"}</CardTitle>
            <CardDescription>{user?.email}</CardDescription>
            <p className="text-xs text-muted-foreground mt-1">
              Role: <span className={isModerator ? "text-primary font-medium" : ""}>{role ?? "—"}</span>
            </p>
          </div>
        </CardHeader>
        <CardContent className="flex gap-2">
          <Button variant="destructive" onClick={logout}>Sair</Button>
        </CardContent>
      </Card>
    </div>
  )
}

function initials(name?: string | null) {
  if (!name) return "?"
  return name.trim().split(/\s+/).map((p) => p[0]?.toUpperCase() ?? "").slice(0, 2).join("")
}
