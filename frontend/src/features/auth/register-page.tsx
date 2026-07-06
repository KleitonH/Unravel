import { useState, type FormEvent } from "react"
import { Link, useNavigate } from "@tanstack/react-router"
import { useAuth } from "@/stores/auth"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"

export function RegisterPage() {
  const register = useAuth((s) => s.register)
  const navigate = useNavigate()

  const [name, setName] = useState("")
  const [email, setEmail] = useState("")
  const [password, setPassword] = useState("")
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      await register({ name, email, password })
      navigate({ to: "/onboarding" })  // novo usuário cai direto no onboarding
    } catch (err) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      setError(msg ?? "Não foi possível criar a conta. Tente outro e-mail.")
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
          </CardContent>
          <CardFooter className="flex-col gap-3">
            <Button type="submit" className="w-full" disabled={submitting}>
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
