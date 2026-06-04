import { useState } from "react"
import { useMutation, useQueryClient } from "@tanstack/react-query"
import { Loader2, Plus } from "lucide-react"
import { toast } from "sonner"
import { adminCustomApi } from "@/api/admin-custom"
import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Textarea } from "@/components/ui/textarea"
import type { CreateCustomTrailRequest } from "@/types/admin-custom"

/**
 * Dialog modal pra criar trilha custom. Disparado pelo botão "Nova trilha"
 * na TrailsListPage. Slug, ícone e cor são opcionais — backend gera
 * defaults sensatos (slugify do nome, "📘", "#7038f2").
 *
 * <para>Trilha criada começa como rascunho (<c>isPublished=false</c>) —
 * moderador publica explicitamente via toggle na lista.</para>
 */
export function NewTrailDialog() {
  const [open, setOpen] = useState(false)
  const [form, setForm] = useState<CreateCustomTrailRequest>({
    name: "",
    description: "",
    icon: "",
    accentColor: "",
    level: "Beginner",
  })
  const qc = useQueryClient()

  const mutation = useMutation({
    mutationFn: () => adminCustomApi.createTrail({
      name:        form.name.trim(),
      description: form.description?.trim() || undefined,
      icon:        form.icon?.trim()        || undefined,
      accentColor: form.accentColor?.trim() || undefined,
      level:       form.level,
    }),
    onSuccess: (created) => {
      qc.invalidateQueries({ queryKey: ["admin", "trails"] })
      toast.success(`Trilha "${created.name}" criada (rascunho).`)
      setOpen(false)
      setForm({ name: "", description: "", icon: "", accentColor: "", level: "Beginner" })
    },
    onError: (err: unknown) => {
      const status = (err as { response?: { status?: number; data?: { message?: string } } })?.response
      if (status?.status === 409) toast.error(status.data?.message ?? "Slug já está em uso.")
      else                        toast.error("Falha ao criar trilha.")
    },
  })

  const canSubmit = form.name.trim().length >= 3 && !mutation.isPending

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button>
          <Plus className="h-4 w-4 mr-1" />
          Nova trilha
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Criar trilha custom</DialogTitle>
          <DialogDescription>
            Trilhas criadas aqui ficam como rascunho até você publicar.
            Após criar, adicione conteúdos e gere perguntas pra ela.
          </DialogDescription>
        </DialogHeader>

        <form
          onSubmit={(e) => { e.preventDefault(); if (canSubmit) mutation.mutate() }}
          className="space-y-3"
        >
          <Field
            id="trail-name"
            label="Nome *"
            hint="Mínimo 3 caracteres."
          >
            <Input
              id="trail-name"
              value={form.name}
              onChange={(e) => setForm({ ...form, name: e.target.value })}
              placeholder="Ex: Banco de Dados Avançado"
              autoFocus
            />
          </Field>

          <Field
            id="trail-desc"
            label="Descrição"
            hint="Texto curto que aparece no card da trilha pros alunos."
          >
            <Textarea
              id="trail-desc"
              value={form.description}
              onChange={(e) => setForm({ ...form, description: e.target.value })}
              placeholder="Trilha focada em modelagem relacional, índices e otimização."
              rows={3}
            />
          </Field>

          <div className="grid grid-cols-3 gap-3">
            <Field id="trail-icon" label="Ícone" hint="Emoji.">
              <Input
                id="trail-icon"
                value={form.icon}
                onChange={(e) => setForm({ ...form, icon: e.target.value })}
                placeholder="🗄"
                maxLength={4}
              />
            </Field>
            <Field id="trail-color" label="Cor" hint="Hex.">
              <Input
                id="trail-color"
                value={form.accentColor}
                onChange={(e) => setForm({ ...form, accentColor: e.target.value })}
                placeholder="#7038f2"
              />
            </Field>
            <Field id="trail-level" label="Nível">
              <select
                id="trail-level"
                value={form.level}
                onChange={(e) =>
                  setForm({ ...form, level: e.target.value as CreateCustomTrailRequest["level"] })
                }
                className="flex h-10 w-full rounded-md border border-input bg-popover px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              >
                <option value="Beginner">Iniciante</option>
                <option value="Intermediate">Intermediário</option>
                <option value="Advanced">Avançado</option>
              </select>
            </Field>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setOpen(false)}>
              Cancelar
            </Button>
            <Button type="submit" disabled={!canSubmit}>
              {mutation.isPending
                ? <><Loader2 className="h-4 w-4 mr-1 animate-spin" />Criando…</>
                : "Criar"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function Field({
  id, label, hint, children,
}: { id: string; label: string; hint?: string; children: React.ReactNode }) {
  return (
    <div className="space-y-1">
      <Label htmlFor={id} className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
        {label}
      </Label>
      {children}
      {hint && <p className="text-[11px] text-muted-foreground">{hint}</p>}
    </div>
  )
}
