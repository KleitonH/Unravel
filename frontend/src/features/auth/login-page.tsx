import { useState, type FormEvent } from "react"
import { Link, useNavigate, useRouterState } from "@tanstack/react-router"
import { useAuth } from "@/stores/auth"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"

export function LoginPage() {
  const login = useAuth((s) => s.login)
  const navigate = useNavigate()
  const search   = useRouterState({ select: (s) => s.location.search }) as Record<string, unknown>
  const redirect = typeof search.redirect === "string" ? search.redirect : "/dashboard"

  const [email, setEmail] = useState("")
  const [password, setPassword] = useState("")
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      await login({ email, password })
      navigate({ to: redirect })
    } catch {
      setError("E-mail ou senha inválidos.")
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="min-h-dvh grid place-items-center p-4 py-8 bg-gradient-to-br from-background via-background to-card">
      <Card className="w-full max-w-md animate-fade-in">
        <CardHeader className="text-center space-y-2">
          <div className="text-4xl">💎</div>
          <CardTitle className="text-2xl">Bem-vindo de volta!</CardTitle>
          <CardDescription>Entre para continuar sua trilha</CardDescription>
        </CardHeader>
        <form onSubmit={onSubmit}>
          <CardContent className="space-y-4">
            {error && (
              <div className="rounded-md border border-destructive/50 bg-destructive/10 px-3 py-2 text-sm text-destructive">
                {error}
              </div>
            )}
            <div className="space-y-2">
              <Label htmlFor="email">E-MAIL</Label>
              <Input id="email" type="email" autoComplete="email" required value={email}
                     onChange={(e) => setEmail(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="password">SENHA</Label>
              <Input id="password" type="password" autoComplete="current-password" required
                     value={password} onChange={(e) => setPassword(e.target.value)} />
            </div>
          </CardContent>
          <CardFooter className="flex-col gap-3">
            <Button type="submit" className="w-full" disabled={submitting}>
              {submitting ? "Entrando…" : "Entrar"}
            </Button>
            <p className="text-sm text-muted-foreground text-center">
              Não tem conta?{" "}
              <Link to="/auth/register" className="text-primary font-medium hover:underline">
                Cadastre-se
              </Link>
            </p>
          </CardFooter>
        </form>
      </Card>
    </div>
  )
}
