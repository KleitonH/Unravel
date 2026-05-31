import { QueryClient } from "@tanstack/react-query"

/**
 * Cliente único do TanStack Query — singleton compartilhado por toda a app.
 * Defaults conservadores: 30s stale (servidor já é fonte da verdade), retry
 * só em 5xx, refetch ao focar a janela.
 */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      gcTime:    5 * 60_000,
      retry: (failureCount, error: unknown) => {
        const status = (error as { status?: number })?.status
        if (status && status < 500) return false   // não retenta 4xx
        return failureCount < 2
      },
      refetchOnWindowFocus: true,
    },
    mutations: { retry: 0 },
  },
})
