import { UserNavi } from "./user-navi"
import { cn } from "@/lib/utils"

/**
 * Preview circular do ROSTO do NAVI do usuário (com cosméticos equipados).
 *
 * O <Navi> desenha o corpo inteiro num viewBox 200×210; a cabeça fica
 * centrada em ~(50%, 38%) do container. Aqui renderizamos o NAVI ampliado
 * (k×) dentro de um círculo com overflow hidden, deslocado pra enquadrar só
 * a cabeça — vira um "avatar" do mascote pra header/perfil.
 */
export function NaviFace({
  size = 40,
  ring = true,
  className,
}: {
  size?: number
  ring?: boolean
  className?: string
}) {
  const k = 1.9                       // zoom: cabeça preenche o círculo
  const navi = size * k
  const left = size * (0.5 - 0.5 * k) // centra X da cabeça (50%)
  const top = size * (0.5 - 0.39 * k) // centra Y da cabeça (~39%)

  return (
    <div
      style={{ width: size, height: size }}
      className={cn(
        "relative overflow-hidden rounded-full bg-popover/70 shrink-0",
        ring && "ring-2 ring-primary/30",
        className,
      )}
    >
      <div style={{ position: "absolute", left, top, width: navi, height: navi }}>
        <UserNavi size={navi} />
      </div>
    </div>
  )
}
