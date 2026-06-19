# Assets do NAVI — paper-doll por camadas (PR 63e)

Solte aqui as imagens dos cosméticos. O renderer (`navi.tsx`) descobre os
arquivos automaticamente: **o nome do arquivo é o `slug` do cosmético**
(o mesmo `AssetSlug` do banco). Sem arte → cai no SVG de placeholder.

```
assets/preto.webp      ← base (pelagem) preta
assets/cartola.webp    ← chapéu cartola
assets/happy.webp      ← rosto "feliz"
...
```

Formatos aceitos: `.webp` (preferido) ou `.png`. Use **WebP** com transparência.

---

## Regra de ouro: canvas fixo + registro (registration)

**Toda peça é exportada no MESMO canvas, na posição onde ela fica no corpo,
transparente no resto.** Assim o front só empilha imagens do mesmo tamanho por
z-index — zero cálculo de offset por item. É o que faz tudo "encaixar".

- **Canvas:** `1000 × 1050 px` (proporção 200:210, igual ao viewBox SVG).
- **Fundo:** transparente.
- **Não recortar/centralizar por item.** O chapéu sai no topo, a capa atrás,
  os pés embaixo — cada um na sua posição, no canvas inteiro.

### Mapa de posição (em % do canvas — referência do desenho atual)

| Âncora | x | y |
|---|---|---|
| Topo da cabeça | 50% | ~12% |
| Centro dos olhos | 40% / 60% | ~39% |
| Nariz | 50% | ~43% |
| Pescoço/gola | 50% | ~57% |
| Centro do tronco | 50% | ~72% |
| Base da cauda | ~75% | ~67% |
| Pés | 40% / 60% | ~94% |

---

## As camadas (z-order: back → front)

| Camada | slot | slug(s) | z | O que desenhar |
|---|---|---|---|---|
| Capa | accessory | `capa` | 5 | atrás do corpo |
| **Base (pelagem)** | fur | `preto` `cinza` `laranja` `branco` `dourado` | 20 | corpo + cabeça + orelhas + braços + cauda + pés + camiseta roxa c/ pata. **SEM rosto, SEM óculos, SEM cosméticos** |
| Tronco | accessory | `jaleco` `mochila` | 30 | sobre a camiseta |
| Pescoço | accessory | `gravata` | 45 | gravata-borboleta no colarinho |
| **Rosto** | mood | `neutral` `happy` `sad` `excited` | 70 | olhos + boca + **óculos + nariz** (rosto completo, na mesma posição em todas as pelagens) |
| Chapéu | hat | `cartola` `bone` `headset` `antenas` `coroa` | 90 | por cima de tudo |

> Por que o rosto é overlay separado da base: permite trocar expressão (mood)
> sem refazer cada pelagem. Por isso a **base não tem rosto** e os óculos vêm
> junto do rosto (idênticos nos 4 moods).

---

## Como gerar peças com proporção/estilo iguais às referências

Não gere itens "soltos" — proporção e traço nunca batem. Gere **editando a base**:

1. **Base canônica (1×):** crie o NAVI em pose neutra, fundo transparente, no
   canvas `1000×1050`, posição travada (tabela acima). Use as refs aprovadas
   (gato preto de camiseta roxa; mercador; gata branca) como guia de estilo.
2. **Bíblia de estilo (sufixo fixo de prompt):** traço escuro fino e uniforme,
   cel-shading suave de 2 tons, olhos verdes grandes, focinho rosa, cabeça ≈ ⅓
   da altura, paleta consistente.
3. **Cada peça = editar a base** (mantém a gata exata):
   - Ferramentas: Gemini image / DALL·E edit (inpaint), Midjourney `--cref` +
     `--sref`, ou Stable Diffusion + ControlNet (lineart) + IP-Adapter.
   - Prompt: *"a mesma gata, mesma proporção e pose, adicione [item]; só o
     [item] visível; fundo transparente"* + sufixo de estilo.
4. **Isole a peça:** mascare/remova o resto (`rembg`/SAM) deixando só o item.
5. **Exporte no canvas inteiro `1000×1050`**, transparente fora do item.
6. **Nomeie = o slug** (`cartola.webp`) e solte nesta pasta. Pronto.

### Pelagens (recolor)
Como é raster, cada pelagem é uma **imagem de base própria** (`preto/cinza/
laranja/branco/dourado`), todas com o rosto na MESMA posição (pra o overlay de
mood servir em todas). Tabby (laranja) já com listras pintadas.

### Checklist por asset
- [ ] `1000×1050`, fundo transparente
- [ ] item na posição correta (não centralizado)
- [ ] base SEM rosto/óculos; mood COM óculos+nariz
- [ ] nome do arquivo = slug exato do banco
- [ ] WebP otimizado (< ~150 KB)
