import { cn } from "@/lib/utils"

/** Placeholder animado pra loading states. */
function Skeleton({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={cn("animate-pulse rounded-md bg-muted/40", className)}
      {...props}
    />
  )
}

export { Skeleton }
