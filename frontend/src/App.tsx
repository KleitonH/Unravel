import { QueryClientProvider } from "@tanstack/react-query"
import { RouterProvider } from "@tanstack/react-router"
import { queryClient } from "@/lib/query"
import { router } from "@/router"

/**
 * Shell raiz: providers de Query + Router. Outras telas vêm via router.
 */
export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  )
}
