import { useEffect, useState } from "react"
import {
  ArrowLeft, ArrowRight, BookOpen, Check, Hash, HelpCircle,
  ShieldCheck, Sparkles, X,
} from "lucide-react"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from "@/components/ui/dialog"
import { cn } from "@/lib/utils"

/**
 * PR 60-d — Tutorial pro moderador na primeira vez que abre o editor
 * de conteúdo custom. Explica o modelo mental novo (H2 = capítulo,
 * mínimo 4 perguntas, opções IA vs manual, bloqueio de publicação).
 *
 * **Persistência**: localStorage `unravel.tutorial.contentEditor.seen`
 * marca como visto após "Entendi" no último slide ou "Pular tutorial".
 * Botão `(?)` permanente no header reabre a qualquer momento.
 *
 * **API simples**: parent renderiza `<ContentEditorTutorial />` sempre;
 * o componente decide internamente se mostra (auto-open ou via prop
 * `forceOpen`).
 */

const LS_KEY = "unravel.tutorial.contentEditor.seen"

export function shouldShowTutorialOnMount(): boolean {
  try { return localStorage.getItem(LS_KEY) !== "1" }
  catch { return false }   // SSR / localStorage indisponível: não mostra
}

export function markTutorialSeen() {
  try { localStorage.setItem(LS_KEY, "1") } catch { /* ignore */ }
}

type Slide = {
  icon:        React.ReactNode
  title:       string
  body:        React.ReactNode
}

const SLIDES: Slide[] = [
  {
    icon:  <Hash className="h-6 w-6" />,
    title: "Use ## pra dividir em capítulos",
    body: (
      <>
        <p>
          Cada linha começando com <Code>##</Code> vira um <strong>capítulo</strong>{" "}
          que o aluno estuda separadamente — modelo "Duolingo": estuda
          o trecho, pratica, próximo capítulo.
        </p>
        <ExampleBlock>{`# Composer e PSR  ← título (ignorado)

## O que é o Composer
Composer é o gerenciador de dependências...

## Comandos essenciais
\`composer install\`, \`composer update\`...

## Padrões PSR
PSR-1, PSR-4, PSR-12...`}</ExampleBlock>
        <p className="text-xs text-muted-foreground">
          Acima: 3 capítulos. Cada um vira uma "ilha" no fluxo de estudo do aluno.
        </p>
      </>
    ),
  },
  {
    icon:  <Sparkles className="h-6 w-6" />,
    title: "Perguntas: gere com IA ou crie manualmente",
    body: (
      <>
        <p>Duas formas de popular cada capítulo:</p>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 pt-1">
          <Path
            icon="🤖"
            title="Gere com IA"
            body="Botão Gerar perguntas dispara o OpenAI; cada chunk H2 ganha perguntas próprias."
          />
          <Path
            icon="✍️"
            title="Crie manualmente"
            body="Seção 'Curadas por você' permite escrever questões do zero com 4 opções."
          />
        </div>
        <p className="text-xs text-muted-foreground pt-2">
          Pode misturar: gere via IA, refine as boas como Gold, escreva as faltantes.
        </p>
      </>
    ),
  },
  {
    icon:  <BookOpen className="h-6 w-6" />,
    title: "Mínimo 4 perguntas por capítulo",
    body: (
      <>
        <p>
          Pra publicar a trilha, <strong>cada capítulo</strong> precisa de
          pelo menos <Badge variant="outline" className="text-[10px] ml-1">4</Badge>{" "}
          perguntas válidas. Se faltar, você verá um aviso pra completar.
        </p>
        <div className="rounded-md border border-warning/30 bg-warning/5 p-3 text-xs space-y-1">
          <p className="font-semibold text-warning">⚠ Por que esse mínimo?</p>
          <p className="text-foreground/80">
            Capítulos com 1-2 perguntas viram decoreba — aluno memoriza a
            única pergunta em vez de aprender o conceito. 4 dá variação
            mínima pra exigir entendimento real.
          </p>
        </div>
        <p className="text-xs text-muted-foreground">
          O sistema usa entre 4 e 7 perguntas adaptativamente, baseado na
          dificuldade do capítulo.
        </p>
      </>
    ),
  },
  {
    icon:  <ShieldCheck className="h-6 w-6" />,
    title: "Edição livre — regenere quando quiser",
    body: (
      <>
        <p>
          Editou o markdown? As perguntas antigas ficam, mas você pode
          regenerar a qualquer momento — sem perder tokens já gastos
          (rows antigas viram histórico).
        </p>
        <ul className="text-xs text-muted-foreground space-y-1 list-disc pl-5">
          <li>Cada geração consome <strong>1 cm de lã</strong> por pergunta (3 cm em modo urgente).</li>
          <li>Acompanhe progresso pelo chip <strong>Forge</strong> no header.</li>
          <li>Aluno só vê o conteúdo depois que você publica a trilha.</li>
        </ul>
        <div className="rounded-md border border-success/30 bg-success/5 p-3 text-xs">
          <p className="font-semibold text-success">✓ Pronto pra começar!</p>
          <p className="text-foreground/80 mt-1">
            Esse tutorial fica disponível no botão <kbd className="px-1 rounded border border-border bg-popover">?</kbd> do header — abra de novo quando quiser.
          </p>
        </div>
      </>
    ),
  },
]

export function ContentEditorTutorial({
  forceOpen = false, onClose,
}: {
  /** Quando true, abre independente do localStorage (vindo do botão ?). */
  forceOpen?: boolean
  onClose?:   () => void
}) {
  const [open, setOpen] = useState(false)
  const [idx,  setIdx]  = useState(0)

  // Auto-abre na primeira vez ou quando forceOpen.
  useEffect(() => {
    if (forceOpen) { setOpen(true); setIdx(0); return }
    if (shouldShowTutorialOnMount()) {
      setOpen(true)
      setIdx(0)
    }
  }, [forceOpen])

  function close() {
    markTutorialSeen()
    setOpen(false)
    onClose?.()
  }

  const slide  = SLIDES[idx]
  const isLast = idx === SLIDES.length - 1

  return (
    <Dialog open={open} onOpenChange={(v) => !v && close()}>
      <DialogContent className="max-w-xl">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <span className="flex h-9 w-9 items-center justify-center rounded-full bg-primary/15 text-primary">
              {slide.icon}
            </span>
            <span>{slide.title}</span>
          </DialogTitle>
          <DialogDescription>
            Tutorial · passo {idx + 1} de {SLIDES.length}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-3 text-sm">
          {slide.body}
        </div>

        {/* Progress dots */}
        <div className="flex justify-center gap-1.5 pt-1">
          {SLIDES.map((_, i) => (
            <button
              key={i}
              type="button"
              onClick={() => setIdx(i)}
              aria-label={`Ir pro passo ${i + 1}`}
              className={cn(
                "h-1.5 rounded-full transition-all duration-200",
                i === idx     ? "w-6 bg-primary"
                : i < idx     ? "w-1.5 bg-success"
                              : "w-1.5 bg-muted/50",
              )}
            />
          ))}
        </div>

        <DialogFooter className="flex !justify-between gap-2 flex-wrap">
          <Button variant="ghost" size="sm" onClick={close}>
            <X className="h-3.5 w-3.5 mr-1" />
            Pular tutorial
          </Button>
          <div className="flex gap-2">
            {idx > 0 && (
              <Button variant="outline" size="sm" onClick={() => setIdx(idx - 1)}>
                <ArrowLeft className="h-3.5 w-3.5 mr-1" />
                Anterior
              </Button>
            )}
            {!isLast
              ? <Button size="sm" onClick={() => setIdx(idx + 1)}>
                  Próximo <ArrowRight className="h-3.5 w-3.5 ml-1" />
                </Button>
              : <Button size="sm" onClick={close}>
                  <Check className="h-3.5 w-3.5 mr-1" />
                  Entendi
                </Button>}
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

/**
 * Botão `(?)` pequeno pro header reabrir o tutorial. Renderiza o
 * próprio Tutorial controlado.
 */
export function ContentEditorTutorialHelpButton() {
  const [forced, setForced] = useState(false)
  return (
    <>
      <Button
        variant="ghost"
        size="icon"
        className="h-9 w-9"
        onClick={() => setForced(true)}
        title="Tutorial: como funciona o editor de conteúdo"
        aria-label="Reabrir tutorial"
      >
        <HelpCircle className="h-4 w-4" />
      </Button>
      {forced && <ContentEditorTutorial forceOpen onClose={() => setForced(false)} />}
    </>
  )
}

// ── Subcomponentes ──────────────────────────────────────────────────

function Code({ children }: { children: React.ReactNode }) {
  return (
    <code className="px-1.5 py-0.5 rounded bg-popover/60 border border-border text-[12px] font-mono text-primary">
      {children}
    </code>
  )
}

function ExampleBlock({ children }: { children: string }) {
  return (
    <pre className="text-[11px] leading-relaxed font-mono whitespace-pre-wrap rounded-md border border-border bg-popover/60 p-3 overflow-x-auto">
      {children}
    </pre>
  )
}

function Path({ icon, title, body }: { icon: string; title: string; body: string }) {
  return (
    <div className="rounded-md border border-border bg-popover/40 p-3 space-y-1">
      <p className="font-semibold text-sm flex items-center gap-1.5">
        <span>{icon}</span>{title}
      </p>
      <p className="text-[11px] text-muted-foreground">{body}</p>
    </div>
  )
}
