import * as React from "react"
import { cn } from "@/lib/utils"

/**
 * Barra de progresso simples (sem Radix Progress pra não puxar mais
 * dep — Radix.Progress dá só semantic role e ARIA, que aplicamos manual).
 */
interface ProgressProps extends React.HTMLAttributes<HTMLDivElement> {
  value?: number  // 0..100
}

const Progress = React.forwardRef<HTMLDivElement, ProgressProps>(
  ({ className, value = 0, ...props }, ref) => (
    <div
      ref={ref}
      role="progressbar"
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={value}
      className={cn("relative h-2 w-full overflow-hidden rounded-full bg-secondary/40", className)}
      {...props}
    >
      <div
        className="h-full bg-gradient-to-r from-primary to-accent transition-all duration-500"
        style={{ width: `${Math.min(100, Math.max(0, value))}%` }}
      />
    </div>
  ),
)
Progress.displayName = "Progress"

export { Progress }
