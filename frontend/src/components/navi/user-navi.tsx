import { Navi, type NaviMood } from "./navi"
import { useNaviAppearance } from "./use-navi-appearance"

/**
 * NAVI do usuário com a aparência ATIVA (cosméticos equipados) — o espaço
 * reservado pra personalização do mascote em perfil, batalhas, dashboard, etc.
 *
 * Hoje renderiza o NAVI default (ou o que o aluno já equipou). Quando os
 * cosméticos/assets chegarem, a personalização aparece aqui automaticamente,
 * sem mudar os consumidores. `mood` opcional sobrepõe o humor equipado (ex.:
 * "excited" numa vitória).
 */
export function UserNavi({
  size = 120, mood, style,
}: { size?: number; mood?: NaviMood; style?: React.CSSProperties }) {
  const { appearance } = useNaviAppearance()
  return (
    <Navi
      size={size}
      fur={appearance.fur}
      hat={appearance.hat}
      accessory={appearance.accessory}
      mood={mood ?? appearance.mood}
      style={style}
    />
  )
}
