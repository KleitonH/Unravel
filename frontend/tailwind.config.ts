import type { Config } from "tailwindcss"
import animate from "tailwindcss-animate"
import forms from "@tailwindcss/forms"

/**
 * PR 21 — config Tailwind do Unravel. CSS vars semânticas (padrão shadcn)
 * mapeadas em index.css. Paleta dark roxa preservada do front Angular.
 */
const config: Config = {
  darkMode: ["class"],
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    container: {
      center: true,
      padding: "1rem",
      screens: { "2xl": "1400px" },
    },
    extend: {
      colors: {
        border: "hsl(var(--border))",
        input: "hsl(var(--input))",
        ring: "hsl(var(--ring))",
        background: "hsl(var(--background))",
        foreground: "hsl(var(--foreground))",
        primary: {
          DEFAULT: "hsl(var(--primary))",
          foreground: "hsl(var(--primary-foreground))",
        },
        secondary: {
          DEFAULT: "hsl(var(--secondary))",
          foreground: "hsl(var(--secondary-foreground))",
        },
        destructive: {
          DEFAULT: "hsl(var(--destructive))",
          foreground: "hsl(var(--destructive-foreground))",
        },
        muted: {
          DEFAULT: "hsl(var(--muted))",
          foreground: "hsl(var(--muted-foreground))",
        },
        accent: {
          DEFAULT: "hsl(var(--accent))",
          foreground: "hsl(var(--accent-foreground))",
        },
        success: {
          DEFAULT: "hsl(var(--success))",
          foreground: "hsl(var(--success-foreground))",
        },
        warning: {
          DEFAULT: "hsl(var(--warning))",
          foreground: "hsl(var(--warning-foreground))",
        },
        popover: {
          DEFAULT: "hsl(var(--popover))",
          foreground: "hsl(var(--popover-foreground))",
        },
        card: {
          DEFAULT: "hsl(var(--card))",
          foreground: "hsl(var(--card-foreground))",
        },
      },
      fontFamily: {
        sans:    ["DM Sans", "ui-sans-serif", "system-ui", "sans-serif"],
        display: ["Syne", "DM Sans", "ui-sans-serif", "sans-serif"],
        mono:    ["ui-monospace", "SFMono-Regular", "Menlo", "monospace"],
      },
      borderRadius: {
        lg: "var(--radius)",
        md: "calc(var(--radius) - 2px)",
        sm: "calc(var(--radius) - 4px)",
      },
      keyframes: {
        // PR 21 — fade simples (usado em micro-feedback inline)
        "fade-in": {
          from: { opacity: "0", transform: "translateY(-4px)" },
          to:   { opacity: "1", transform: "none" },
        },
        // PR 27 — transição entre páginas: leve subida + fade.
        // Curva ease-out + 18ms de delay pra absorver o flush
        // do React após Suspense resolver (evita flicker do skeleton
        // virando direto).
        "page-in": {
          from: { opacity: "0", transform: "translateY(8px)" },
          to:   { opacity: "1", transform: "none" },
        },
        // PR 27 — entrada de card/chip. Escala discreta (0.96→1)
        // pra dar sensação de "pop" sem ser brincalhão. Stagger por
        // animation-delay no consumer.
        "pop-in": {
          "0%":   { opacity: "0", transform: "scale(0.96)" },
          "100%": { opacity: "1", transform: "scale(1)" },
        },
        // PR 27 — quando o número de XP/streak muda (após submit do
        // quiz, refetch do profile). Overshoot leve pra chamar atenção
        // sem ser intrusivo.
        "count-pop": {
          "0%":   { transform: "scale(0.85)" },
          "50%":  { transform: "scale(1.08)" },
          "100%": { transform: "scale(1)" },
        },
      },
      animation: {
        "fade-in":   "fade-in 0.25s ease-out",
        "page-in":   "page-in 0.32s cubic-bezier(0.22, 1, 0.36, 1)",
        "pop-in":    "pop-in 0.24s cubic-bezier(0.22, 1, 0.36, 1) both",
        "count-pop": "count-pop 0.32s ease-out",
      },
    },
  },
  plugins: [animate, forms],
}

export default config
