# Assets do NAVI — sistema de customização por camadas

O NAVI é montado como **paper-doll**: o renderer (`navi.tsx`) empilha CAMADAS
por z-index. Cada camada usa a imagem cujo nome é o `slug`; sem arte → cai no
SVG de placeholder. Arte concept de referência (estilo-alvo): gato preto de
óculos, camiseta roxa com pata, olhos verdes — cel-shading suave, traço escuro.

> **Implementado hoje (v1):** slugs simples — `fur` (`preto/cinza/laranja/
> branco/dourado`), `hat`, `accessory`, `mood`. As seções marcadas **[roadmap]**
> descrevem a visão completa (poses, raças, olhos, cauda) e o naming
> forward-compatible; o resolver será estendido pra compô-las.

---

## Onde os assets são servidos (bundle vs CDN)

Resolver config-driven via `VITE_NAVI_CDN` (ver `src/lib/env.ts`):

- **Vazio (default):** arquivos desta pasta são **bundlados** pelo Vite.
- **Setado** (ex.: `VITE_NAVI_CDN=https://pub-xxxx.r2.dev/navi`): busca
  `${VITE_NAVI_CDN}/<slug>.<ext>`. Suba os MESMOS arquivos pro bucket/CDN
  (Cloudflare R2 = egress grátis). Esta pasta segue como **manifesto** de quais
  slugs têm arte. Migrar = 1 env, sem tocar código nem banco.

---

## Regra de ouro: canvas fixo + registro

**Toda peça é exportada no MESMO canvas, na posição onde fica no corpo,
transparente no resto.** Empilhar vira `z-index` puro — zero offset por item.

- **Canvas:** `1000 × 1050 px` · **fundo transparente** · **não centralizar** o item.

### Âncoras (% do canvas) — pose "parado" de referência

| Âncora | x | y |
|---|---|---|
| Topo da cabeça | 50% | ~12% |
| Centro dos olhos | 40% / 60% | ~39% |
| Nariz | 50% | ~43% |
| Pescoço/gola | 50% | ~57% |
| Centro do tronco | 50% | ~72% |
| Base da cauda | ~75% | ~67% |
| Pés | 40% / 60% | ~94% |

> Cada **pose** tem seu próprio mapa de âncoras (a "parado" é a primeira).

---

## O conceito-chave: BAKED vs OVERLAY (evita explosão combinatória)

O problema: **a pelagem influencia tudo** (corpo, cabeça, orelhas, cauda,
patas) e **pose muda a silhueta inteira**. Se cada dimensão fosse uma camada
independente, o nº de imagens explodiria. A solução de jogos de customização é
classificar cada dimensão:

- **BAKED** (assada na base) — multiplica o nº de imagens. Use pra dimensões
  que mudam a silhueta/cor do corpo todo.
- **OVERLAY** (camada independente) — soma, não multiplica. Use pra dimensões
  localizadas (um região pequena) e que não precisam casar com a pelagem no
  corpo inteiro.

### Taxonomia recomendada pro NAVI

| Dimensão | Classe | Por quê |
|---|---|---|
| **Pose** | BAKED (multiplicador-mor) | muda a silhueta inteira |
| **Raça** (formato rosto/orelha) | BAKED na base | muda silhueta da cabeça; pelagem tem que casar |
| **Pelagem** (cor + padrão) | BAKED na base | "influencia tudo" → tem que ser pintada na base |
| **Cauda** (tipo) | BAKED na base *(simples)* ou OVERLAY `pose×cauda×pelagem` | cor = pelagem; bake junto da raça é o mais simples |
| **Cor dos olhos** | OVERLAY (íris) *ou* baked | região minúscula, independente da pelagem → barato orthogonal |
| **Expressão** (mood) | OVERLAY `pose×raça` | localizada no rosto, mas casa com o formato do rosto |
| **Chapéu / Acessório** | OVERLAY `pose` | encaixa nas âncoras de cabeça/tronco |

### A fórmula de custo

```
nº de BASES   = poses × raças × pelagens           (× caudas, se baked)
nº de OVERLAYS = poses × ( Σchapéus + Σacessórios
                          + raças × Σmoods          (expressão casa c/ rosto)
                          + ΣcoresDeOlho )
TOTAL = bases + overlays
```

**Mover uma dimensão de BAKED → OVERLAY derruba o total.** Ex.: olhos e cauda
como overlay em vez de baked tiram um fator multiplicativo.

### Orçamento de lançamento (mantenha pequeno)

| Dimensão | v1 sugerido |
|---|---|
| poses | **1** (parado) — adicione depois |
| raças | 2–3 (shorthair, persa-fofo, oriental) |
| pelagens | 5 (preto, cinza, laranja-tabby, branco, dourado) |
| moods | 4 (neutral, happy, sad, excited) |
| cores de olho | 3–4 (verde, âmbar, azul, heterocromia) — overlay |
| chapéus/acessórios | os que já existem |

→ Ex.: `1 pose × 3 raças × 5 pelagens = 15 bases` + `~12 moods + 5 chapéus +
4 acessórios + 4 olhos = ~25 overlays` ≈ **40 arquivos** pra um v1 rico.
Storage trivial (~4MB); o custo real é **esforço de geração** — por isso
comece com **1 pose**.

---

## Camadas e z-order (back → front) **[roadmap]**

| z | Camada | slug (naming) |
|---|---|---|
| 5  | Capa | `acc__<pose>__capa` |
| 8  | Cauda (se overlay) | `tail__<pose>__<tipo>__<pelagem>` |
| 20 | **Base** (corpo+cabeça+orelhas+braços+patas+camiseta; **sem rosto**) | `base__<pose>__<raça>__<pelagem>` |
| 30 | Acessório de tronco | `acc__<pose>__jaleco` / `mochila` |
| 45 | Pescoço | `acc__<pose>__gravata` |
| 68 | Cor dos olhos (íris) | `eyes__<cor>` |
| 70 | Rosto/expressão (olhos+boca+óculos+nariz) | `mood__<pose>__<raça>__<mood>` |
| 90 | Chapéu | `hat__<pose>__<slug>` |

**Naming:** separador `__`, sufixo `.webp`. O resolver compõe o look:
`base + (cauda) + olhos + mood + acessório + chapéu`, cada um resolvido pra
imagem-ou-fallback. (v1 atual usa só `preto`, `cartola`, etc. — sem `__`.)

> **Por que a base não tem rosto:** o rosto é overlay pra trocar expressão sem
> refazer cada pelagem/raça. A íris é uma camada separada (z68) **abaixo** do
> rosto — o `mood` desenha o contorno do olho + branco + pupila com a área da
> íris transparente; o `eyes__<cor>` pinta só a íris embaixo. (Se for difícil,
> no v1 deixe a cor do olho assada na base e adicione a customização depois.)

---

## Spec passo a passo de produção

Ancorando na arte concept aprovada (o gato preto). **Nunca gere peças soltas** —
gere sempre **editando a base** pra proporção/traço baterem.

### 0. Bíblia de estilo (sufixo fixo de todo prompt)
> *"chibi cat mascot, soft 2-tone cel-shading, thin dark uniform outline, large
> round glasses, big expressive eyes, pink nose, head ≈ 1/3 of body height,
> flat lighting, transparent background, children's storybook style"* — ajuste
> com base na concept aprovada.

### 1. Base canônica mestre (1×) — a âncora de consistência
- Da concept, gere o NAVI **isolado, fundo transparente, pose "parado" neutra**
  (sem novelo/laptop/props), canvas `1000×1050`, âncoras da tabela.
- Raça = shorthair, pelagem = preto, **sem rosto e sem óculos** (são overlay).
- **Aprove esta imagem** — ela é a referência de TODAS as outras gerações.

### 2. Variantes de pelagem (BAKED) — `base__parado__shorthair__<pelagem>`
- Edite a mestre recolorindo a pelagem (cinza, laranja-**tabby** com listras,
  branco, dourado). Mesma pose/linha/silhueta. Isole o corpo, exporte full-frame.

### 3. Variantes de raça (BAKED) — `base__parado__<raça>__<pelagem>`
- Edite a mestre mudando **formato de rosto/orelha** (persa: rosto redondo +
  pelo longo; oriental: rosto anguloso + orelha grande). Depois × cada pelagem.

### 4. Rosto/expressão (OVERLAY) — `mood__parado__<raça>__<mood>`
- A partir de uma base, desenhe **olhos + boca + óculos + nariz** (neutral/
  happy/sad/excited). Isole só a região do rosto. Uma vez por (raça × mood),
  pois a expressão casa com o formato do rosto.

### 5. Cor dos olhos (OVERLAY) — `eyes__<cor>`
- Isole só as **íris** (verde/âmbar/azul…), transparente no resto. (Opcional no
  v1 — ver nota de z-order.)

### 6. Cosméticos (OVERLAY) — `hat__parado__<slug>` / `acc__parado__<slug>`
- Edite a base adicionando o item; isole **só o item**; exporte full-frame.
- Capa atrás (z5); jaleco/mochila no tronco (z30); gravata no pescoço (z45);
  chapéu por cima (z90).

### 7. Exportação (todo asset)
- `1000×1050`, transparente, **registro full-frame** (item na posição, não
  centralizado), nome = slug exato, **WebP < ~150KB**.

### 8. Novas poses (depois)
- Refaça a matriz de **bases** por pose e **re-encaixe** os cosméticos na nova
  pose (cada pose tem âncoras próprias). Por isso pose é o multiplicador mais
  caro — adicione uma de cada vez.

### Ferramentas de geração
Gemini image / DALL·E edit (inpaint), Midjourney `--cref` + `--sref`, ou
Stable Diffusion + ControlNet (lineart) + IP-Adapter (mais controlável). Cortar
fundo: `rembg` / SAM.

---

## Checklist por asset
- [ ] `1000×1050`, fundo transparente
- [ ] gerado **editando a base** (proporção/traço batendo com a concept)
- [ ] item na posição correta (registro full-frame, não centralizado)
- [ ] base SEM rosto/óculos; `mood` COM óculos+nariz
- [ ] nome = slug exato (v1: `preto`/`cartola`…; roadmap: `base__pose__raça__pelagem`)
- [ ] WebP otimizado (< ~150 KB)
