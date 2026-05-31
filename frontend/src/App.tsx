import { useEffect } from "react"
import { QueryClientProvider } from "@tanstack/react-query"
import { RouterProvider } from "@tanstack/react-router"
import { Toaster } from "sonner"
import { queryClient } from "@/lib/query"
import { router } from "@/router"
import { useAuth } from "@/stores/auth"

/**
 * Shell raiz. Hydration do auth roda uma vez no mount — se há tokens
 * persistidos, valida via /users/me; se falhar, limpa o store. Só depois
 * o Router renderiza o conteúdo (evita "flash" de tela de login pra usuário
 * logado).
 */
export default function App() {
  const hydrate = useAuth((s) => s.hydrate)

  useEffect(() => {
    void hydrate()
  }, [hydrate])

  return (
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
      <Toaster richColors theme="dark" position="bottom-right" />
    </QueryClientProvider>
  )
}
