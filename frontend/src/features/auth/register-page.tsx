import { useState, type FormEvent } from "react"
import { Link, useNavigate } from "@tanstack/react-router"
import { GraduationCap, UserRound } from "lucide-react"
import { useAuth } from "@/stores/auth"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { cn } from "@/lib/utils"

type Role = "student" | "moderator"

export function RegisterPage() {
  const register = useAuth((s) => s.register)
  const navigate = useNavigate()

  const [name, setName] = useState("")
  const [email, setEmail] = useState("")
  const [password, setPassword] = useState("")
  const [role, setRole] = useState<Role>("student")
  const [inviteCode, setInviteCode] = useState("")
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const isModerator = role === "moderator"

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      await register({
        name, email, password, role,
        inviteCode: isModerator ? inviteCode.trim() : undefined,
      })
      navigate({ to: "/onboarding" })  // novo usuário cai direto no onboarding
    } catch (err) {
      const data = (err as { response?: { data?: { error?: string; message?: string } } })?.response?.data
      setError(data?.error ?? data?.message ?? "Não foi possível criar a conta. Tente outro e-mail.")
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="min-h-dvh grid place-items-center p-4 py-8 bg-gradient-to-br from-background via-background to-card">
      <Card className="w-full max-w-md animate-fade-in">
        <CardHeader className="text-center space-y-2">
          <img src="/logo-novelo.svg" alt="Unravel" className="mx-auto h-14 w-14" />
          <CardTitle className="text-2xl">Crie sua conta</CardTitle>
          <CardDescription>Em segundos você está estudando</CardDescription>
        </CardHeader>
        <form onSubmit={onSubmit}>
          <CardContent className="space-y-4">
            {error && (
              <div className="rounded-md border border-destructive/50 bg-destructive/10 px-3 py-2 text-sm text-destructive">
                {error}
              </div>
            )}

            {/* Seleção de perfil: estudante (padrão) ou educador (exige convite) */}
            <div className="space-y-2">
              <Label>EU SOU</Label>
              <div className="grid grid-cols-2 gap-2" role="radiogroup" aria-label="Tipo de conta">
                <RoleButton
                  active={role === "student"}
                  onClick={() => setRole("student")}
                  Icon={UserRound}
                  label="Estudante"
                  hint="Estudar e praticar"
                />
                <RoleButton
                  active={role === "moderator"}
                  onClick={() => setRole("moderator")}
                  Icon={GraduationCap}
                  label="Educador"
                  hint="Criar e moderar conteúdo"
                />
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="name">NOME</Label>
              <Input id="name" required value={name} onChange={(e) => setName(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="email">E-MAIL</Label>
              <Input id="email" type="email" autoComplete="email" required value={email}
                     onChange={(e) => setEmail(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="password">SENHA</Label>
              <Input id="password" type="password" autoComplete="new-password" required
                     minLength={8} value={password} onChange={(e) => setPassword(e.target.value)} />
            </div>

            {/* Código de convite — só aparece (e é exigido) para Educador */}
            {isModerator && (
              <div className="space-y-2 animate-fade-in">
                <Label htmlFor="inviteCode">CÓDIGO DE CONVITE</Label>
                <Input
                  id="inviteCode"
                  required
                  autoComplete="off"
                  placeholder="Chave de educador da instituição"
                  value={inviteCode}
                  onChange={(e) => setInviteCode(e.target.value)}
                />
                <p className="text-xs text-muted-foreground">
                  Contas de educador exigem um código fornecido pela instituição.
                </p>
              </div>
            )}
          </CardContent>
          <CardFooter className="flex-col gap-3">
            <Button
              type="submit"
              className="w-full"
              disabled={submitting || (isModerator && inviteCode.trim().length === 0)}
            >
              {submitting ? "Criando…" : "Criar conta"}
            </Button>
            <p className="text-sm text-muted-foreground text-center">
              Já tem conta?{" "}
              <Link to="/auth/login" className="text-primary font-medium hover:underline">
                Entrar
              </Link>
            </p>
          </CardFooter>
        </form>
      </Card>
    </div>
  )
}

function RoleButton({
  active, onClick, Icon, label, hint,
}: {
  active:  boolean
  onClick: () => void
  Icon:    React.ComponentType<{ className?: string }>
  label:   string
  hint:    string
}) {
  return (
    <button
      type="button"
      role="radio"
      aria-checked={active}
      onClick={onClick}
      className={cn(
        "flex flex-col items-center gap-1 rounded-md border px-3 py-3 text-center transition-colors",
        active
          ? "border-primary bg-primary/10 text-foreground"
          : "border-border hover:bg-foreground/5 text-muted-foreground",
      )}
    >
      <Icon className={cn("h-5 w-5", active && "text-primary")} />
      <span className="text-sm font-medium">{label}</span>
      <span className="text-[11px] leading-tight">{hint}</span>
    </button>
  )
}
