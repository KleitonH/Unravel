# Briefing Design — Onda 1: Foundation + Loja + Tags/Títulos

**Audiência:** Claude Design (mockup HTML standalone, preview ao vivo)
**Objetivo:** receber HTML+CSS funcionando que o Claude Code converta pra React/Tailwind no repo Unravel.
**Modo de entrega esperado:** 1 arquivo HTML por tela, autocontido (sem dependências externas além de Google Fonts), abrindo direto em `file://`. Inline `<style>` + inline SVG quando aplicável. Mocks navegáveis entre si via `<a href="outra-tela.html">`.

---

## 1. Contexto do produto

**Unravel** é uma plataforma web educacional pra estudantes de TI, inspirada em Duolingo. Mascote central: **NAVI**, um gato preto com óculos verdes redondos e camisa roxa com pata branca (ver `arquivos_unravel/concept-cat.png`). NAVI acompanha o usuário em toda a jornada — comemora acertos, sofre com erros, aparece em recompensas, é personalizável via Loja Cosmética.

**Hoje (PRs 21–32 concluídas)** a plataforma já tem em React + Vite:
- Dashboard, Trilhas, Jornada (plano diário), Quiz (MCQ com perguntas geradas por IA local), Perfil, Admin
- Autenticação JWT, gamificação (XP/Coins/Stars/Lives/Streak)
- Sidebar compacta colapsável em ≥1024px, bottom-nav em <1024px

**O que falta** (7 ideias documentadas em `arquivos_unravel/UNRAVEL_Ideia*.docx`): Novelo de Lã, Login Diário, Loja Cosmética, Modo Arena (PvP), Tags/Títulos/Rankings, Caixinha de Gatos (guilds), Modo Aula (Kahoot). **Esta Onda 1 cobre 3 entregas:** foundation (DS v2), Loja, Tags/Títulos/Rankings.

---

## 2. Identidade visual canônica (NÃO MUDAR)

### 2.1 Paleta — design tokens atuais (em uso)

```css
/* Backgrounds */
--bg:          #0e0a1e;   /* fundo global, mais escuro */
--card:        #181230;   /* cards, modais, dropdowns */
--popover:     #1f1839;   /* hover de itens, opções */
--border:      #2a2444;   /* divisões e bordas neutras */

/* Texto */
--text:        #f6f4ff;   /* texto principal */
--muted:       #a59fc8;   /* texto secundário, labels */

/* Brand */
--primary:     #a78bfa;   /* roxo Unravel — botões primários, links, destaque */
--primary-fg:  #0e0a1e;   /* texto sobre primary */
--accent:      #38db8c;   /* verde NAVI happy — sucesso, "LLM grounded", checkmarks */
--warning:     #facc15;   /* amarelo, alertas, streak fire */
--danger:      #f97373;   /* erro, perda de vida */

/* Estados especiais */
--navi-happy:  #38db8c;
--navi-sad:    #ed54f2;   /* rosa NAVI triste */
```

### 2.2 Tipografia

```css
font-family: 'DM Sans', system-ui, sans-serif; /* default body */
font-family: 'Syne', 'DM Sans', sans-serif;    /* headings (display) */

/* Escala */
--text-xs:   0.75rem;  /* 12px — micro labels */
--text-sm:   0.875rem; /* 14px — body secundário */
--text-base: 1rem;     /* 16px — body */
--text-lg:   1.125rem; /* 18px — subtítulos */
--text-xl:   1.25rem;  /* 20px — cards header */
--text-2xl:  1.5rem;   /* 24px — page titles */
--text-3xl:  1.875rem; /* 30px — hero */
--text-4xl:  2.25rem;  /* 36px — milestones */
```

Carregue do Google Fonts:
```html
<link href="https://fonts.googleapis.com/css2?family=DM+Sans:wght@400;500;600;700&family=Syne:wght@600;700;800&display=swap" rel="stylesheet">
```

### 2.3 Borders, radii, sombras

```css
--radius-sm: 6px;
--radius:    8px;       /* padrão pra cards/inputs */
--radius-lg: 12px;
--radius-xl: 16px;
--radius-pill: 9999px;

--shadow-sm: 0 2px 8px rgba(0, 0, 0, 0.25);
--shadow:    0 4px 16px rgba(0, 0, 0, 0.35);
--shadow-glow: 0 0 24px rgba(167, 139, 250, 0.35);  /* glow roxo pra reward */
--shadow-accent-glow: 0 0 24px rgba(56, 219, 140, 0.35);
```

### 2.4 NAVI (mascote) — referência visual

Ver `arquivos_unravel/concept-cat.png`. Características:
- Gato preto, corpo arredondado/amigável
- Óculos redondos verdes
- Camisa roxa (~#5a3a8f) com **pata branca estampada**
- Olhos verdes brilhantes
- 4 moods: `neutral` 🐱, `happy` 😸, `sad` 😿, `excited` 🤩

**Pra Onda 1**, use:
- Versão **emoji estilizada** como placeholder (😸 / 🐱 / 😿) com fundo redondo colorido por mood
- OU **inline SVG** simples (silhueta de gato preto com óculos verdes) — versão 1 minimalista
- Cosméticos = overlays sobre essa silhueta (chapéu, gravata, cor de pelagem)

Cosméticos exemplo (sugestões textuais — pode ser SVG decorativo):
- Chapéus: cartola, boné de programador, headset de gamer, antenas alien, coroa
- Acessórios: gravata-borboleta, jaleco branco, capa de super-herói, mochila
- Pelagens: preto (default), cinza, laranja, branco, tigrado, dourado (raro)
- Expressões: surpreso, dormindo (Z's), digitando (laptop), concentrado (óculos focados)

---

## 3. Foundation — Componentes do Design System v2

Estes são **building blocks reutilizáveis** que aparecem em várias telas. Cada um deve ser entregue como **1 seção do HTML showcase** (`design-system.html`) mostrando estados.

### 3.1 Status Chips (header)

Hoje a Hero do dashboard tem chips simples: XP/Streak/Vidas/Coins. Onda 1 expande pra um **status bar global** sempre visível no topo da página (entre o título e o conteúdo), com:

| Chip | Ícone | Valor | Cor | Animação |
|------|-------|-------|-----|----------|
| **XP** | ⭐ | número (fmt k/M) | `--primary` | Pulsa quando ganha XP |
| **Streak** | 🔥 | dias | `--warning` | Fogo animado, flame com tilt sutil contínuo |
| **Vidas** | ❤️ | 0–5 corações | `--danger` | Corações tremem quando perde 1 |
| **Coins** | 🪙 | número | `--warning` (gold tint) | Coin gira ao ganhar |
| **Stars** | ⭐💎 | número | `--accent` (raro) | Brilho ao ganhar |

**Estado "Booster ativo"** (Dia 6 do ciclo, Ideia 2): chip XP fica com gradient + texto "x2" sobreposto + ícone raio ⚡.

**Visual:** chip rounded-pill, fundo `--popover/60`, border `1px solid --border`, padding `4px 12px`, gap interno `4px` entre ícone e número. Tooltip on hover explica o significado.

### 3.2 NAVI Persistent Companion

Ícone do NAVI flutuante no canto inferior-direito (ou em widget dedicado no Dashboard). Reage a eventos:

- **idle**: 🐱 NAVI neutro, animação respirando suave (chest pulse 4s)
- **happy** (acerto, level up, reward): 😸 NAVI animation bounce + speech bubble curto ("Acertou!", "+50 XP!", "Arrasou!")
- **sad** (erro, perda de vida): 😿 NAVI fica fade-out 2s, speech bubble ("Não desista!", "Tenta de novo")
- **excited** (achievement, novelo completed, level up): 🤩 NAVI gira, particles de coração ao redor, speech bubble destacada

**Speech bubble**: card pequeno `--card` com border-left 3px `--primary`, padding 8px 12px, max-width 200px, aparece à esquerda do NAVI com seta apontando.

**Tamanho**: 64px círculo, fundo translúcido `rgba(167,139,250,0.15)` border `2px --primary/40`.

### 3.3 Reward Modal (CENTRAL pra Onda 1)

Modal celebratório que aparece quando user ganha algo significativo (level up, conquista de tag, completar novelo, vencer arena, comprar cosmético).

**Estrutura visual:**

```
┌─────────────────────────────────────┐
│      [confetti particles bg]        │
│                                     │
│         🎉  RECOMPENSA  🎉          │  ← Syne 28px
│                                     │
│           [NAVI excited]            │  ← 120px círculo
│                                     │
│        +250 XP · +50 🪙             │  ← chips ganhos
│                                     │
│      🏆  Tag desbloqueada           │  ← Syne 18px
│      "Ninja do Novelo"              │  ← Syne 20px primary
│                                     │
│         [   Continuar   ]           │  ← btn primary, pulsando
└─────────────────────────────────────┘
```

**Animação de entrada:**
- Backdrop fade-in 0.3s
- Card scale 0.7 → 1.05 → 1.0 (overshoot) em 0.6s ease-out-back
- Confetti dispara após 0.4s (particles caem 2s)
- NAVI bounce + speech bubble "Você conseguiu!"
- Chips de ganho aparecem em stagger 100ms entre cada

**Variantes** (mesma estrutura, badge muda):
- `🏆 Tag desbloqueada` — accent verde
- `⬆️ Level up` — primary roxo + número grande do nível
- `🎁 Cosmético adquirido` — gold tint + preview do item
- `👑 Título conquistado` — gradient roxo-rosa + texto do título

### 3.4 Toast notification (efêmero, side notifications)

Já existe via `sonner` no React, mas precisa de variantes gamificadas:

- **success** (verde): "✓ +20 XP" — borda esquerda accent
- **gain** (gold): "🪙 +10 moedas" — borda esquerda warning
- **loss** (red): "❤️ -1 vida" — borda esquerda danger, tremor sutil
- **info** (purple): "📅 Nova meta gerada" — borda esquerda primary
- **streak** (orange): "🔥 7 dias seguidos!" — gradient warning, fogo animado

Posição: bottom-right, stack vertical. Auto-dismiss 4s.

### 3.5 Progress Bars temáticas

- **Streak (ciclo 1-7)**: 7 segmentos, cada um vira flame quando completo
- **Mastery por tópico**: barra horizontal arredondada com fill primary, gradient leve
- **Novelo de Lã**: barra com textura de fios (linhas paralelas), encolhendo da direita pra esquerda

### 3.6 Cards de conteúdo

Padrão atual já existe (CardHeader, CardContent, CardFooter). Onda 1 adiciona:
- **Card com border-left 4px** colorido (já usado em Trilhas pelo accentColor) — vira padrão pra tudo categorizável
- **Card "raridade"**: pra cosméticos, com gradient sutil em ângulo:
  - common → cinza
  - rare → azul
  - epic → roxo (com glow leve)
  - legendary → gold-warning (com shimmer animation)

---

## 4. Entrega 1: Loja Cosmética — "Toca do NAVI Mercador"

**Arquivo HTML:** `loja.html`
**Route alvo no React (depois):** `/loja`

### 4.0 Direção visual (LER PRIMEIRO)

> **Não é um catálogo de e-commerce. É uma cena 2D acolhedora onde um NAVI mercador atende você.**

**Referências mentais** (vibe a invocar):
- **Stardew Valley — Pierre's General Store**: balcão, vendedor sempre no quadro, ambiente caloroso
- **Hollow Knight — Salubra/Iselda's shops**: personagem dono carismático, items dispostos ao redor, atmosfera intimista
- **Animal Crossing — Nook's Cranny**: cenário físico navegável com NPC presente
- **Hades — Charon's shop**: vendedor único e memorável, items flutuando como ofertas
- **Tunic — atelier-style** (paleta dark cozy com lanternas)

**Atmosfera:**
- Cenário ilustrado de fundo: **interior aconchegante de loja de gato** — prateleiras com itens cosméticos, almofadas no chão, lanterna de papel pendurada, cortina, talvez uma janela mostrando céu noturno estrelado
- Iluminação morna sobreposta ao dark purple base (warm overlay com gradient)
- Particulas sutis flutuando (poeira mágica, faíscas) pra trazer vida

**Identidade do NAVI Mercador (NPC):**
- É um **segundo NAVI distinguível do cliente**: cor de pelagem **dourada-amarelada** (vs preto do cliente), com **avental marrom** e **boina de feltro**, óculos meia-lua redondos pendurados na pontinha do nariz
- Sempre presente atrás de um **balcão de madeira**
- Postura: braços apoiados no balcão, atento, sorriso amigável
- Speech bubble dele aparece com fala contextual

**Identidade do NAVI Cliente (você):**
- O NAVI atual do usuário, **com todos os itens equipados**, em **destaque maior que tudo o resto da tela**
- Postura: em pé, levemente girado em 3/4, idle animation respirando
- Quando user **passa hover** num item da lista: NAVI cliente **veste temporariamente o item** com animação suave de overlay aparecendo, e mostra um "spotlight" amarelo embaixo dele
- Quando user **clica num item**: lock no preview, mostra `Antes ↔ Depois` toggle pequeno

---

### 4.1 Layout da tela (desktop ≥1024px)

```
┌──────────────────────────────────────────────────────────────────┐
│ Sidebar │                  CENA COZY (fundo ilustrado)            │
│ compacta│                                                          │
│         │  ╔═══════════════════════════════════════════════════╗  │
│         │  ║  Header transparente sobre cenário                ║  │
│         │  ║  🛍️ Toca do NAVI       🪙 1.250  💎 18           ║  │
│         │  ╚═══════════════════════════════════════════════════╝  │
│         │                                                          │
│         │  ┌─── ZONA DE PALCO (60-65% width) ─────┐  ┌─PRATELEIRA┐│
│         │  │                                       │  │ (35-40%)  ││
│         │  │   ┌───────────┐                       │  │            ││
│         │  │   │  NAVI     │     ✨                │  │ Tabs:      ││
│         │  │   │ MERCADOR  │  ┌──────────────┐    │  │ [Loja]     ││
│         │  │   │ dourado   │  │ Speech:      │    │  │ [Coleção]  ││
│         │  │   │ c/avental │  │ "Hoje temos  │    │  │            ││
│         │  │   │ atrás do  │  │ novidade pra │    │  │ Filtros:   ││
│         │  │   │ balcão    │  │ você! 🐾"    │    │  │ [tudo]     ││
│         │  │   └───────────┘  └──────────────┘    │  │ [chapéu]   ││
│         │  │                                       │  │ [acessório]││
│         │  │           ✨ partículas ✨            │  │ [pelagem]  ││
│         │  │                                       │  │ [expressão]││
│         │  │       ┌──────────────────┐            │  │ [✨ excl]  ││
│         │  │       │                  │            │  │            ││
│         │  │       │   NAVI CLIENTE   │            │  │ ┌────────┐ ││
│         │  │       │   (200-280px)    │            │  │ │[card 1]│ ││
│         │  │       │   com items      │            │  │ │preview │ ││
│         │  │       │   equipados +    │            │  │ │small   │ ││
│         │  │       │   hover preview  │            │  │ │+name   │ ││
│         │  │       │                  │            │  │ │+price  │ ││
│         │  │       │  ← spotlight ↓   │            │  │ └────────┘ ││
│         │  │       └──────────────────┘            │  │ ┌────────┐ ││
│         │  │       ▔▔▔▔▔▔▔▔▔▔▔▔▔▔                │  │ │[card 2]│ ││
│         │  │                                       │  │ └────────┘ ││
│         │  │  ┌──── Toolbar do palco ────────┐    │  │ ┌────────┐ ││
│         │  │  │ [Antes ↔ Depois]  [↻ Reset]  │    │  │ │ ...    │ ││
│         │  │  │ Equipado: Cartola + Gravata  │    │  │ │        │ ││
│         │  │  └──────────────────────────────┘    │  │ │ scroll │ ││
│         │  │                                       │  │ │ vert.  │ ││
│         │  │  ┌── CTA grande quando item selec ─┐ │  │ │        │ ││
│         │  │  │  🪙 200   Comprar Cartola       │ │  │ └────────┘ ││
│         │  │  └─────────────────────────────────┘ │  │            ││
│         │  └──────────────────────────────────────┘  └────────────┘│
│         │                                                          │
└──────────────────────────────────────────────────────────────────┘
```

**Princípios de layout:**

1. **Zona de palco** (60-65% width) — o "espaço da loja" com fundo ilustrado, NAVI mercador, NAVI cliente em destaque, toolbar abaixo
2. **Prateleira lateral** (35-40% width) — lista vertical scrollable de items, agrupados/filtráveis
3. **Sem grid 4x4 chato** — a lista lateral é vertical (1 ou 2 colunas), com **cards menores que o foco do NAVI**
4. **Hierarquia visual**: NAVI cliente > NAVI mercador > items na prateleira (em decrescente de tamanho/proeminência)
5. **Sem header tradicional** — Status chips flutuam translúcidos sobre o cenário (top-right)

---

### 4.2 Cenário de fundo (CRÍTICO pra vibe)

**Como ilustrar** (entregue como inline SVG no HTML, estilo geométrico-soft):

Camadas (do fundo pro frente):
1. **Parede de fundo**: gradient roxo dark `#1a1438 → #0e0a1e` com algumas estrelas pintadas (pequenos dots brancos)
2. **Janela** (canto esquerdo ou direito): retângulo com céu noturno (azul escuro + lua crescente amarela suave) — opcional, dá charm
3. **Prateleiras decorativas** (não funcionais, só decoração): 2-3 prateleiras horizontais com silhuetas de potes/livros/novelos
4. **Lanterna de papel** pendurada do teto (círculo amarelo suave com glow leve `--warning/30`)
5. **Balcão de madeira** em primeiro plano (~30% da altura, ocupa toda largura) — gradient `#5a3520 → #3a2310`, com textura sutil de tábua (linhas verticais finas mais escuras)
6. **Partículas flutuando**: 8-12 pontos brancos/amarelos `opacity 0.4`, animação de subir lentamente em loop (4-8s cada), reset ao topo

**Cor overlay morno** sobreposto ao tudo: `radial-gradient(ellipse at center, transparent 0%, rgba(255, 180, 100, 0.05) 60%, rgba(0,0,0,0.3) 100%)` — vinheta + warm tint sutil

---

### 4.3 NAVI Mercador (NPC) — design

**Como renderizar** (inline SVG):

```
Diferenciação visual do NAVI cliente:
- Pelagem: laranja-dourada (#d4a045) em vez de preta
- Avental: marrom escuro (#5a3520) com bolsinho frontal
- Boina de feltro: verde-musgo (#5a7340) inclinada
- Óculos: meia-lua dourados (#fbbf24) pendurados na pontinha do nariz
- Postura: braços apoiados no balcão, gato sentado/agachado atrás
- Expressão: olhos fechados de sorriso (^_^), boca em sorriso suave
- Animação idle: respiração leve (chest pulse 4s) + cauda balançando 2s
```

**Speech bubble** ao lado dele (à direita, com seta apontando):
- Card pequeno fundo `--card` + border 1px `--primary/40`, padding 12px, max-width 220px
- Texto Syne 14px
- **Falas contextuais rotativas** (troca a cada 8s ou em eventos):
  - Idle: *"Bem-vindo à Toca! Dá uma olhada nos novos itens, viu? 🐾"*
  - Quando item raro está selecionado: *"Oh! Esse aí é especial mesmo!"*
  - Quando user passa hover muitas vezes: *"Já se decidiu? Sem pressa, viu!"*
  - Após compra: *"Boa escolha! Combinou demais com você!"*
  - Saldo insuficiente: *"Hmm, parece que falta um pouquinho de moeda..."*

---

### 4.4 NAVI Cliente — preview ao vivo

**O foco principal da tela.** Renderizar 240-280px de altura, centralizado na zona de palco, postura 3/4 voltada pra esquerda (olhando pra mercador).

**Estados:**

| Estado | O que mostra | Animação |
|--------|--------------|----------|
| **Idle** (sem hover/click) | NAVI com itens **atualmente equipados** | Respiração 4s + blink ocasional |
| **Hover** num item da prateleira | NAVI **veste temporariamente o item** | Fade-in do overlay 0.3s + glow amarelo embaixo do NAVI |
| **Click** (item lockado pra preview) | Como hover, mas persistente | Toolbar "Antes ↔ Depois" aparece |
| **Antes** (toggle) | Volta pra equipados originais | Crossfade 0.3s |
| **Depois** (toggle) | Mostra com item selecionado | Crossfade 0.3s |
| **Comprou item** | NAVI faz **bounce + spin** com confetti | Reward modal abre |
| **Saldo insuficiente** | NAVI fica triste 😿 por 1s | Shake leve + bubble do mercador |

**Spotlight no chão**:
- Elipse `rgba(255, 200, 100, 0.25)` abaixo dos pés do NAVI, blur 8px
- Pulsa suave quando item lockado pra preview (scale 1.0 ↔ 1.05 em 2s loop)

**"Sticker" do item flutuando** (visual feedback):
- Quando user passa hover num card da prateleira, mini-thumbnail do item **voa do card até a posição correspondente no NAVI** (ex: chapéu voa pra cabeça, gravata pro pescoço) com Bezier curve em 0.4s
- No mouseleave, **volta** com mesma animação invertida

---

### 4.5 Toolbar abaixo do palco

Aparece quando item está **lockado em preview** (após click):

```
┌──────────────────────────────────────────────────────────┐
│ [Antes ↔ Depois]  [↻ Reset]  Equipado: Cartola + Gravata │
└──────────────────────────────────────────────────────────┘
```

- **Antes ↔ Depois**: toggle entre estado atual vs preview do item
- **Reset**: limpa preview, volta pro idle
- **Lista equipado**: chips pequenos com X pra desequipar (se já é item da coleção)

Abaixo da toolbar, quando item NOVO está preview:

```
┌──────────────────────────────────────────────┐
│  🪙 200                                       │
│  ┌────────────────────────────────────────┐  │
│  │  ✨ Comprar Cartola Premium             │  │  ← btn grande
│  └────────────────────────────────────────┘  │
└──────────────────────────────────────────────┘
```

Botão **grande** (full-width da zona de palco), `--primary` com glow, ícone moeda à esquerda do texto, animation pulse sutil pra atrair atenção.

Quando user **já tem** o item:
```
┌────────────┬────────────────┐
│ ✓ Você tem │  Equipar agora │
└────────────┴────────────────┘
```

Quando user **não tem saldo**:
```
┌────────────────────────────────────────┐
│  🔒 Faltam 50 moedas                    │
│     [Continue estudando pra ganhar!]   │
└────────────────────────────────────────┘
```
Botão desabilitado, tooltip explicando.

---

### 4.6 Prateleira lateral (lista de items)

**Layout:** scroll vertical, 1 coluna no desktop estreito ou 2 colunas no desktop largo.

**Estrutura:**

```
┌─────────────────────────────┐
│  📑 Tabs                     │
│  [Loja Base] [Coleção]      │
│                              │
│  Filtros chip-style:         │
│  [Tudo] [👒] [👔]            │
│  [🎨 Pelagens] [😺 Expr]    │
│  [✨ Exclusivos]             │
│                              │
│  Ordenar: [↓ Preço] [Recente]│
│                              │
│  ▰▰▰▰▰▰▰▰▰▰▰▰▰▰▰▰▰▰▰▰▰▰    │
│  ┌──────────────────────┐    │
│  │ [icon item]          │    │  ← card menor (não tem
│  │ Cartola Premium      │    │     preview do NAVI dentro)
│  │ 🪙 200    [Comum]    │    │
│  └──────────────────────┘    │
│  ┌──────────────────────┐    │
│  │ [icon item]          │    │
│  │ Boné Programador     │    │
│  │ 🪙 150    [Raro]     │    │
│  └──────────────────────┘    │
│  ... (scroll vertical)        │
└─────────────────────────────┘
```

**Cada card da prateleira (NÃO mostra NAVI dentro — apenas o item):**

```
┌──────────────────────────────────┐
│ ┌───┐                            │
│ │ 👒│  Cartola Premium           │  ← ícone item 40x40 (left)
│ └───┘  Acessório de cabeça       │  ← Syne 14px / DM 11px muted
│        🪙 200       [Comum]      │
└──────────────────────────────────┘
```

**Estados visuais do card:**

| Estado | Visual |
|--------|--------|
| Normal | fundo `--card`, border 1px `--border` |
| **Hover** | scale 1.02, border `--primary/60`, glow leve, mini-thumb do item **voa pro NAVI** |
| **Click** (selecionado/lockado) | border 2px `--primary`, glow forte, ícone "✓" canto superior direito |
| **Já possuído** | badge fitinha verde "✓ Adquirido" canto superior esquerdo, opacity 0.85 |
| **Saldo insuficiente** | opacity 0.55, ícone 🔒 sobreposto no ícone do item |
| **Exclusivo (bloqueado)** | overlay dourado + cadeado, click abre tooltip "Como obter" |

**Raridade no card:**
- Border-left 3px na cor da raridade
- Badge canto inferior-direito: `[Comum]` cinza, `[Raro]` azul, `[Épico]` roxo com glow, `[Lendário]` dourado com shimmer, `[Exclusivo]` rosa
- **Lendário/Exclusivo**: card tem `box-shadow` colorido pulsando 3s loop

---

### 4.7 Aba "Minha Coleção"

Mesma prateleira lateral, mas **só itens já adquiridos**, com botão de ação no card:

```
┌──────────────────────────────────┐
│ ┌───┐                            │
│ │ 👒│  Cartola Premium           │
│ └───┘  ⚡ Equipado                │  ← badge "equipado" verde
│        [Desequipar]              │
└──────────────────────────────────┘
```

**Filtros adicionais:** `[Equipados] [Não equipados] [Recentes]`.

NAVI cliente continua no palco mostrando o que está equipado.

NAVI mercador muda fala: *"Sua coleção tá ficando bonita! Quer trocar de visual?"*

---

### 4.8 Empty state da coleção

Quando user não tem nada adquirido:

```
┌──────────────────────────────────────────┐
│       [NAVI cliente sem acessórios]      │
│                                          │
│        "Hmm, vazio por aqui..."          │  ← Syne 18px
│   Que tal dar uma cara nova pro NAVI?    │  ← DM 14px muted
│                                          │
│        [   ✨ Visitar a Loja   ]         │  ← btn primary
└──────────────────────────────────────────┘
```

NAVI mercador acena: *"Volta sempre, viu? Sempre tenho novidade!"*

---

### 4.9 Mobile (<1024px)

Layout muda radicalmente — espaço não permite cena cozy completa. Adaptação:

```
┌──────────────────────────────┐
│ ← Toca do NAVI    🪙 1.250  │
├──────────────────────────────┤
│                              │
│   [NAVI cliente em foco]     │  ← topo 40% da tela
│   (sem cenário elaborado,    │
│    fundo gradient simples)   │
│                              │
│   [Antes ↔ Depois] [↻]       │
│                              │
│   ┌────────────────────┐     │
│   │ 🪙 200 Comprar     │     │  ← CTA fixo
│   └────────────────────┘     │
├──────────────────────────────┤
│ [Loja] [Coleção]   📑 Tabs   │
│ [Tudo][👒][👔][🎨][😺][✨]   │  ← filtros scroll horizontal
├──────────────────────────────┤
│  ┌──────────────────┐         │
│  │ [card item]      │         │  ← lista vertical
│  └──────────────────┘         │
│  ┌──────────────────┐         │
│  │ [card item]      │         │
│  └──────────────────┘         │
│  ... (scroll)                 │
└──────────────────────────────┘
```

NAVI mercador no mobile: **fica como speech bubble flutuante** (FAB no canto superior direito com avatar pequeno do mercador) — click expande pra mostrar fala completa. Não ocupa espaço no palco principal.

---

### 4.10 Categorias e raridade (cores e efeitos)

```css
--rarity-common:    #9ca3af;  /* cinza */
--rarity-rare:      #60a5fa;  /* azul claro */
--rarity-epic:      #c084fc;  /* roxo claro */
--rarity-legendary: #fbbf24;  /* dourado */
--rarity-exclusive: #f472b6;  /* rosa neon */
```

**Tratamento hierárquico (importante):**

| Raridade | Border | Badge | Background card | Animation |
|----------|--------|-------|-----------------|-----------|
| Comum | 1px cinza | chip cinza | `--card` | nenhuma |
| Raro | 2px azul | chip azul | `--card` + leve tint azul `linear-gradient(...transparente, rgba(96,165,250,0.05))` | nenhuma |
| Épico | 2px roxo | chip roxo | tint roxo + **2-3 partículas roxas flutuando** dentro do card | partículas loop 4s |
| Lendário | 2px dourado | chip dourado | tint dourado + **shimmer diagonal** atravessa o card | shimmer 3s loop |
| Exclusivo | 2px rosa | chip rosa | **moldura ornamentada** (corner brackets dourados) | leve "respiração" scale 1.0↔1.02 4s |

---

### 4.11 Animações detalhadas (ordem de prioridade)

1. **Hover em card da prateleira** → ícone do item voa pro NAVI cliente, NAVI veste, glow embaixo (300ms)
2. **Mouseleave** → ícone volta pro card, NAVI desveste (300ms reverso)
3. **Click em card** → lock; toolbar Antes/Depois aparece com slide-up; mercador fala "Boa escolha!"
4. **Botão Comprar** → moedas voam do chip de saldo (top-right) pro botão; botão dá scale 0.95→1.0; confetti dispara; NAVI bounce 2x; reward modal abre 0.4s depois com item conquistado
5. **Equipar item já possuído** → ícone voa do card pra NAVI; NAVI bounce 1x; toast "✓ Equipado!"
6. **Saldo insuficiente** → tentativa de comprar dá shake no botão + NAVI cliente fica triste 0.8s + speech bubble mercador
7. **Switch entre tabs Loja/Coleção** → crossfade dos cards da prateleira; NAVI mercador troca de fala
8. **Loading inicial** → mercador "puxa caixa de items debaixo do balcão" (animação stagger dos cards aparecendo)

---

### 4.12 Comportamentos a renderizar no HTML showcase

Pra o Claude Design implementar JS mínimo demonstrativo:

- [ ] Hover em qualquer card → ícone voa pra cabeça/pescoço/etc do NAVI cliente (transform animado)
- [ ] Click em card → lock no preview, toolbar aparece
- [ ] Click em "Comprar" → moedas voam, confetti, reward modal abre
- [ ] Toggle "Antes ↔ Depois" → crossfade do NAVI cliente
- [ ] Click no NAVI mercador → cycle pelas falas contextuais
- [ ] Click em ícone de raridade no card → tooltip explica o que significa
- [ ] Click em filtro → highlight ativo, mockar filtragem da prateleira

---

### 4.13 Checklist de qualidade

A entrega só está OK se:

- [ ] **Cenário ilustrado** (não fundo liso) — balcão + janela/lanterna + prateleiras decorativas
- [ ] **NAVI Mercador presente e claramente distinto** do cliente (cor + acessório + posição)
- [ ] **NAVI Cliente é o maior elemento da tela**, em pose 3/4, com items equipados visíveis
- [ ] **Speech bubble do mercador funcional** (rotativa)
- [ ] **Hover em item produz preview imediato** no NAVI cliente (transform suave do item indo pra ele)
- [ ] **CTA de compra é grande, dourado-roxo, glow** — não um botão genérico tipo "Comprar"
- [ ] **Raridade tem 5 tratamentos visuais distintos** (não só cor de borda)
- [ ] **Partículas flutuantes sutis** no cenário (3-10) sem ser distração
- [ ] **Mobile colapsa pra layout funcional** mantendo NAVI cliente em foco

---

## 5. Entrega 2: Tags, Títulos e Rankings

**Arquivos HTML:**
- `perfil.html` (perfil expandido com tags, título, badges)
- `rankings.html` (leaderboard global + por área)

### 5.1 Perfil expandido (`perfil.html`)

Estrutura:

```
┌─────────────────────────────────────────────────┐
│                                                 │
│        [NAVI grande personalizado]              │  ← 180px
│                                                 │
│            Kleiton Martins                      │  ← Syne 32px
│         👑 CSSiamês Profissional                │  ← título ativo, gold
│                                                 │
│      ⭐ 12.5k XP    🔥 47 dias    💎 18         │  ← status big
│                                                 │
├─────────────────────────────────────────────────┤
│                                                 │
│  📍 RANKINGS                                    │
│  ┌─────────────┬─────────────┬─────────────┐  │
│  │ Global #142 │ Web #23 ↑   │ Arena #87   │  │
│  │   ⬆ +5      │   ⬆ +12     │   ⬇ -3      │  │
│  └─────────────┴─────────────┴─────────────┘  │
│                                                 │
├─────────────────────────────────────────────────┤
│                                                 │
│  🏆 TAGS CONQUISTADAS  (8 de 47)               │
│                                                 │
│  ┌────┬────┬────┬────┬────┬────┬────┬────┐    │
│  │ 🔥 │ ⚡ │ 🎯 │ ⚔️ │ 🐱 │ 📚 │ 🧩 │ +1 │    │
│  │30d │Vel │Web │ 10V│Soc │AI  │Alg │   │    │
│  └────┴────┴────┴────┴────┴────┴────┴────┘    │
│                                                 │
│  [Ver todas as conquistas]                     │
│                                                 │
├─────────────────────────────────────────────────┤
│                                                 │
│  ✨ TÍTULOS DESBLOQUEADOS  (3 de 47)            │
│                                                 │
│  ┌──────────────────────────────────────────┐ │
│  │ ●  CSSiamês Profissional  [Ativo]        │ │  ← radio selected
│  │    Domínio de frontend                   │ │
│  ├──────────────────────────────────────────┤ │
│  │ ○  Arranhador de Bugs                    │ │
│  │    Destaque em lógica e depuração        │ │
│  ├──────────────────────────────────────────┤ │
│  │ ○  Ninja do Novelo                       │ │
│  │    Primeiro novelo de lã completado      │ │
│  └──────────────────────────────────────────┘ │
│                                                 │
├─────────────────────────────────────────────────┤
│                                                 │
│  📊 TRILHAS                                     │
│  [cards trilhas com progresso]                 │
│                                                 │
└─────────────────────────────────────────────────┘
```

#### Tag chips (grid)

Cada tag = card 80x80px, com:
- Ícone grande (emoji ou SVG) no centro
- Label embaixo (10px uppercase)
- Borda gradient pela categoria (Ofensiva = warning, Arena = danger, Conhecimento = primary, Social = accent, etc.)
- Hover: scale + tooltip explicando critério e quando foi obtida

**Tag locked** (não conquistada): cinza, com cadeado, tooltip "Como obter: ..."

#### Title selector (radio list)

Lista de cards com radio button à esquerda. Title ativo tem:
- Border `--primary` 2px
- Glow leve
- Badge "Ativo"

Click em outro = atualiza visualmente. (Backend: PUT `/api/profile/title`.)

**Exemplos de títulos do doc (use estes):**
- CSSiamês Profissional — domínio de frontend
- Gato de Schrödinger Sênior — mestre em lógica e algoritmos
- Mestre dos Gatilhos SQL — domínio de banco de dados
- Felino Full-Stack — conclusão de trilhas de frontend e backend
- Purr-feito em Python — alta taxa de acerto em trilha Python
- Ninja do Novelo — primeiro novelo de lã completado
- Arranhador de Bugs — destaque em lógica e depuração
- Campeão da Caixinha — vitória em evento de caixinhas
- Arquiteto de Arranhadores — domínio de arquitetura
- Caçador de Tokens — atividade expressiva em IA/APIs
- Pelagem de Platina — ofensiva de 100 dias
- Inquebrantável — streak nunca quebrado em 30 dias

#### Ranking cards

Cada rank = card horizontal:
```
┌──────────────────────────┐
│ #142  ↑ +5     Global    │  ← #posicao [up/down]  tipo
│ ●●●●●●●●○○  78%          │  ← barra até próximo nível
└──────────────────────────┘
```

Click → abre `rankings.html` filtrado.

### 5.2 Leaderboard (`rankings.html`)

```
┌─────────────────────────────────────────────────┐
│  🏆 Rankings                                    │
│                                                 │
│  [Global] [Por Área] [Arena] [Caixinhas]       │  ← tabs
│                                                 │
│  Período: [Semana] [Mês] [Geral]               │
│                                                 │
│  ┌──────────────────────────────────────────┐ │
│  │ #1 🥇  Maria Silva     Pelagem Platina   │ │
│  │       12.500 XP        +250 hoje         │ │
│  ├──────────────────────────────────────────┤ │
│  │ #2 🥈  João Oliveira   CSSiamês          │ │
│  │       11.800 XP        +180 hoje         │ │
│  ├──────────────────────────────────────────┤ │
│  │ #3 🥉  Ana Costa       Ninja do Novelo   │ │
│  │       10.200 XP        +95 hoje          │ │
│  ├──────────────────────────────────────────┤ │
│  │ ...                                       │ │
│  │ #142 🐱 VOCÊ (Kleiton) CSSiamês          │ │  ← highlight
│  │       8.450 XP         +120 hoje         │ │
│  └──────────────────────────────────────────┘ │
│                                                 │
│  [Carregar mais]                               │
└─────────────────────────────────────────────────┘
```

**Cada linha:**
- Posição com medalha 🥇🥈🥉 nos top 3, depois só número
- NAVI thumbnail (32px, personalizado se possível)
- Nome do usuário + título ativo abaixo (12px muted)
- XP total à direita + delta diário (+250 com seta verde)
- Linha do user atual com background `--primary/10` + border-left 3px `--primary`

**Top 3 destacado** com background gradient sutil (gold/silver/bronze).

---

## 6. Telas-extra úteis (opcionais nesta onda)

Se sobrar tempo, gerar também:

### 6.1 `dashboard-v2.html`
Dashboard com **header global expandido** (status chips ricos + NAVI persistente) + cards de trilhas como já está + nova seção "Recompensas hoje" mostrando o ciclo semanal (preview do dia atual e próximo).

### 6.2 `reward-modal-showcase.html`
Página standalone com 4 botões: "Trigger Tag", "Trigger Level Up", "Trigger Cosmetic", "Trigger Title". Cada um abre o modal correspondente pra Claude Code estudar animações e estrutura.

---

## 7. Restrições técnicas pro HTML

1. **Standalone**: cada arquivo abre direto em `file://`, sem servidor
2. **Inline `<style>`** dentro de `<head>` — pode duplicar entre arquivos
3. **SVG inline** pro NAVI e ícones (Lucide-style minimalista) — sem dependência de bibliotecas
4. **Sem JS framework** — vanilla JS pra interações (toggle tab, click em cards, modal open/close)
5. **Sem build step** — HTML/CSS/JS plano
6. **Fonts**: apenas Google Fonts (link no head)
7. **Responsive**: desktop-first (1280px content), com breakpoint `@media (max-width: 1023px)` virando bottom-nav
8. **Acessibilidade**: `prefers-reduced-motion: reduce` mata animações longas; cores com contrato AA mínimo
9. **Mocks navegáveis**: links entre os 4 HTMLs (sidebar tem Loja/Perfil/Rankings/Dashboard)
10. **Sem imagens externas** — tudo SVG ou emoji. Cosméticos podem ser ilustrações SVG simples (silhuetas, glyphs)

---

## 8. Checklist de entrega

Cada arquivo HTML deve:

- [ ] Importar Google Fonts (DM Sans + Syne)
- [ ] Definir todos os tokens CSS em `:root`
- [ ] Reset/normalize CSS no início
- [ ] Sidebar compacta (desktop) + bottom-nav (mobile) — pode ser duplicado entre arquivos
- [ ] NAVI persistent companion no canto inferior-direito
- [ ] Status chips no topo da página
- [ ] Implementar a tela principal conforme spec
- [ ] Mostrar pelo menos 1 estado interativo (hover, click no card)
- [ ] Comentar no HTML qual seção corresponde a qual spec acima

Arquivos esperados na entrega:

```
design-onda-1/
├── design-system.html       ← showcase de tokens + componentes
├── dashboard-v2.html        ← header expandido + NAVI persistente
├── loja.html                ← entrega 1
├── colecao.html             ← aba minha coleção (pode ser tab dentro de loja.html)
├── perfil.html              ← entrega 2: tags + títulos + rankings overview
├── rankings.html            ← leaderboard completo
├── reward-modal-showcase.html  ← opcional: showcase de modais
└── README.md                ← como navegar entre os arquivos
```

---

## 9. Decisões pendentes (peça ao Claude Code se travar)

- **Layout NAVI no mobile**: persistent companion atrapalha em tela pequena? Sugestão: vira ícone tap-to-open em mobile.
- **Ranking — quantos itens carregar inicial**: 50? 100? Sugestão: 20 + paginação.
- **Cosmético "preview duplo"**: e se user seleciona 2 chapéus? Sugestão: só 1 por categoria (overlay substitui).
- **Title gating**: títulos exibem locked com critério, ou só os desbloqueados? Sugestão: locked aparecem em seção separada "Próximos a desbloquear" (motivacional).

---

## 10. Referências citadas

- `arquivos_unravel/concept-cat.png` — design do NAVI canônico
- `arquivos_unravel/UNRAVEL_Ideia3_Loja_Cosmetica.docx` — fonte da spec da loja
- `arquivos_unravel/UNRAVEL_Ideia5_Tags_Titulos_Rankings.docx` — fonte da spec de tags/títulos
- `arquivos_unravel/UNRAVEL_Sistema_Recompensas.docx` — visão geral economia
- `arquivos_unravel/Prototipos/prototipo22042026/Unravel Prototype.html` — protótipo prévio com NAVI mood detection (`#38db8c` happy / `#ed54f2` sad)
- Frontend atual: `frontend/src/index.css` (tokens) + `frontend/src/features/*` (componentes existentes)

---

**Próximas ondas (NÃO entregar agora):**
- Onda 2: Caixinha de Gatos (guilds) + Novelo de Lã + Login Diário
- Onda 3: Modo Arena (PvP real-time) + Modo Aula (Kahoot)
