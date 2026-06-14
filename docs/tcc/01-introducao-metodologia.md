# Unravel — Documentação Acadêmica (TCC)

> Autor: Kleiton Homem Martins · Orientador: Lucas Saraiva Cardoso · ULBRA Torres, 2026
> Parte teórica: Introdução, Metodologia e Modelagem de Casos de Uso.

---

# 1. Introdução

## 1.1 O que é a aplicação

O **Unravel** é uma aplicação web de aprendizagem voltada a estudantes da área
de Tecnologia da Informação (TI). A plataforma organiza o conhecimento técnico
em **trilhas** temáticas (por exemplo, desenvolvimento web, banco de dados,
inteligência artificial, segurança) e conduz o estudante por **jornadas**
personalizadas, nas quais o conteúdo é estudado em pequenos capítulos
intercalados com a prática de exercícios.

Ao redor desse núcleo de aprendizagem, o Unravel adota uma camada de
**gamificação** (experiência/XP, ofensiva de dias consecutivos, vidas, moedas,
conquistas e personalização do mascote) e um conjunto de **mecânicas sociais**
(parcerias, grupos e competições) cujo objetivo é sustentar o hábito de estudo
ao longo do tempo. A identidade visual gira em torno do mascote **NAVI**, um
gato que acompanha o usuário em toda a jornada.

Um diferencial estruturante do projeto é o uso de **Inteligência Artificial
para geração de questões fundamentadas no conteúdo (LLM-grounded)**: a partir do
material textual cadastrado por um moderador, o sistema extrai afirmações
verificáveis (*claims*) e gera questões de múltipla escolha e de completar
lacunas, submetidas a uma bateria de validadores automáticos de qualidade antes
de chegarem ao aluno.

**Público-alvo:**

- **Primário:** estudantes de cursos superiores de tecnologia em TI.
- **Secundário:** professores/facilitadores (mediação de atividades) e
  autodidatas interessados em aprendizagem técnica.

## 1.2 Qual problema resolve

A pesquisa exploratória que originou o projeto (roteiro de entrevista com
estudantes) identificou um conjunto recorrente de dores no estudo autônomo de
TI, que o Unravel se propõe a atacar:

1. **Falta de direção** — o estudante não sabe *o que* estudar em uma semana
   típica; a quantidade de tecnologias gera paralisia por excesso de opções.
2. **Abandono de tópicos** — começa-se a estudar um assunto e ele é abandonado
   antes da conclusão, por falta de um caminho claro e de feedback de progresso.
3. **Inconsistência / falta de hábito** — sem um gatilho de retorno diário, o
   estudo autônomo é irregular e perde tração.
4. **Aprendizagem superficial ("decoreba")** — conteúdos são memorizados de
   forma rasa e esquecidos, sem real compreensão nem retenção de longo prazo.
5. **Isolamento** — o estudo solitário não cria responsabilidade social nem
   senso de pertencimento que ajudem na motivação.

O Unravel responde a essas dores combinando: **planejamento automático da
jornada** (direção), **estudo guiado em capítulos com prática intercalada**
(combate à decoreba e ao abandono), **gamificação de retenção** (hábito) e
**mecânicas sociais** (responsabilidade mútua e pertencimento).

## 1.3 Diferencial

O Unravel se posiciona de forma distinta em relação às principais referências
do mercado:

| Referência | O que faz bem | O que o Unravel acrescenta |
|---|---|---|
| **Duolingo** | Gamificação de hábito (ofensiva, login diário, mascote) | Foco exclusivo em TI; **geração de questões por IA fundamentada no conteúdo**; trilhas técnicas; mascote felino próprio e customizável |
| **Anki** (repetição espaçada) | Memorização eficiente via SRS | Gamificação completa, **prática intercalada por capítulos** (não só flashcards isolados) e componente social explícito |
| **Khan Academy** | Conteúdo enciclopédico e aulas | Aprendizagem ludificada e **autossuficiente**, centrada em prática e hábito, não em videoaula |
| **Kahoot** | Quiz ao vivo em sala | Quiz ao vivo (Modo Aula, planejado) como *complemento*, sobre um núcleo autônomo de estudo individual |

Os diferenciais centrais e próprios do Unravel são:

1. **Pipeline de IA com qualidade auditada** — as questões não são “alucinadas”:
   são ancoradas em afirmações extraídas do material e passam por validadores
   (vazamento de resposta, fundamentação semântica, qualidade dos distratores,
   posicionamento de lacuna, etc.), com taxa de aproveitamento e custo
   monitorados.
2. **Desenho pedagógico anti-decoreba** — o conteúdo é fatiado em capítulos com
   exigência mínima de questões por capítulo e prática intercalada, alinhado às
   evidências de *practice testing* e *interleaving*.
3. **Identidade felina integral** — o mascote NAVI, a moeda “Novelo de Lã”, as
   conquistas (raças de gato + jargão de TI) e os grupos (“Caixinha de Gatos”)
   formam um tema coeso e memorável.
4. **Curadoria humana sobre geração automática** — o moderador pode gerar,
   **escrever, editar e remover** questões por capítulo, mantendo controle
   editorial sobre o que a IA produz.

## 1.4 Inspirações

**Produtos:** Duolingo (gamificação de hábito e mascote), Anki (repetição
espaçada), Kahoot (quiz ao vivo mediado), e a metáfora de progressão em mapa
inspirada em jogos (mapa de fases estilo *Super Mario World*).

**Fundamentos pedagógicos** (ver também Seção 1.2):

- **Practice Testing / Testing Effect** — aprender pela própria ação de
  responder questões, não apenas reler.
- **Interleaving (prática intercalada)** — alternar tópicos/capítulos em vez de
  blocos massivos, favorecendo retenção e transferência.
- **Spaced Repetition (repetição espaçada / SRS)** — revisões distribuídas no
  tempo para consolidar a memória de longo prazo.
- **Mastery Learning** — progressão condicionada ao domínio do conteúdo
  (refletida no *radar de fraquezas* e no quiz adaptativo).
- **Behavior Reinforcement / formação de hábito** — login diário, ofensiva e
  recompensas como reforço de consistência.
- **Compromisso e prova social (Cialdini)** — mecânicas de parceria e grupo que
  criam responsabilidade mútua.

---

# 2. Metodologia

O desenvolvimento do Unravel adota uma abordagem **híbrida Scrum + Kanban**
(comumente referida como **Scrumban**), combinando o planejamento iterativo e
incremental do Scrum com a gestão de fluxo contínuo e visual do Kanban. Esta
seção apresenta o referencial teórico de cada método e, em seguida, descreve
como eles foram aplicados na prática ao projeto.

## 2.1 Referencial teórico — Scrum

O **Scrum** é um framework ágil para desenvolvimento de produtos complexos,
formalizado por **Ken Schwaber e Jeff Sutherland** no *Scrum Guide*. Baseia-se
no **empirismo** (conhecimento vem da experiência e decisões a partir do que se
observa) e no **pensamento enxuto** (reduzir desperdício, focar no essencial),
sustentados por três pilares: **transparência, inspeção e adaptação**.

Seus elementos principais:

- **Papéis (Scrum Team):** *Product Owner* (responsável por maximizar o valor do
  produto e gerir o Backlog), *Scrum Master* (garante a aplicação do framework e
  remove impedimentos) e *Developers* (constroem o incremento).
- **Artefatos:** *Product Backlog* (lista priorizada de tudo que o produto pode
  precisar), *Sprint Backlog* (subconjunto selecionado para a iteração atual) e
  *Increment* (resultado funcional entregue ao fim de cada Sprint).
- **Eventos:** a *Sprint* (ciclo de duração fixa, tipicamente de 1 a 4 semanas),
  o *Sprint Planning* (planejamento), a *Daily Scrum* (sincronização diária), a
  *Sprint Review* (inspeção do incremento) e a *Sprint Retrospective* (melhoria
  do processo).

O Scrum entrega valor em **incrementos potencialmente utilizáveis** ao final de
cada Sprint, permitindo feedback frequente e replanejamento contínuo.

## 2.2 Referencial teórico — Kanban

O **Kanban** (do japonês “cartão/sinal visual”) tem origem no **Sistema Toyota
de Produção (Lean)**, sistematizado por **Taiichi Ohno**, e foi adaptado ao
trabalho de conhecimento e ao desenvolvimento de software por **David J.
Anderson**. Diferente do Scrum, o Kanban não prescreve papéis ou iterações de
duração fixa; é um **método evolutivo** que se sobrepõe ao processo existente e o
aprimora gradualmente. Seus princípios centrais:

- **Visualizar o fluxo de trabalho** — representar as etapas em um quadro
  (tipicamente colunas *A Fazer → Em Progresso → Concluído*), tornando o trabalho
  e seus gargalos visíveis.
- **Limitar o trabalho em progresso (WIP)** — restringir quantos itens podem
  estar simultaneamente “em andamento”, reduzindo a troca de contexto e expondo
  gargalos.
- **Gerenciar o fluxo** — buscar um fluxo suave e previsível, medindo *lead
  time* e *throughput*.
- **Tornar as políticas explícitas** — deixar claras as regras de pronto
  (*Definition of Done*) e de passagem entre etapas.
- **Sistema puxado (*pull*)** — um novo item só é iniciado quando há capacidade,
  e não empurrado por cronograma.

O Kanban favorece **entrega contínua** e melhoria incremental, sem a cadência
fixa de Sprints.

## 2.3 Scrumban aplicado ao Unravel

Por se tratar de um Trabalho de Conclusão de Curso conduzido por um
desenvolvedor, o Scrum “puro” foi adaptado: os papéis de Product Owner, Scrum
Master e Developer concentram-se na mesma pessoa, e cerimônias de equipe (Daily,
Review e Retrospective entre pares) são substituídas por **autogestão e revisão
com o orientador**. A combinação adotada funciona assim:

**Do Scrum, o projeto preserva:**

- **Sprints** como blocos de planejamento incremental. O backlog foi organizado
  por sprints temáticas (p. ex. *Sprint 3 — gamificação base*; *Sprint 5 —
  algoritmo de dificuldade*; *Sprints 7–8 — loja, arena, grupos e modo aula*),
  cada uma fechando um incremento utilizável.
- **Product Backlog** priorizado: as funcionalidades são quebradas em itens
  pequenos e ordenados por valor e dependência.
- **Incremento utilizável** ao fim de cada entrega, com a aplicação sempre
  executável.

**Do Kanban, o projeto preserva:**

- **Quadro visual de fluxo** com as colunas *A Fazer → Em Progresso → Concluído*,
  por onde cada item de trabalho transita.
- **Limite de WIP** — manter poucos itens “em progresso” simultaneamente
  (idealmente um), reduzindo troca de contexto.
- **Políticas explícitas / Definition of Done** — um item só é considerado
  concluído quando: o código compila, os testes automatizados passam, a mudança
  é validada (verificação visual ou empírica) e é integrada à branch principal
  por meio de um *Pull Request* (PR) dedicado.

**Operacionalização (rastreabilidade):**

Cada item do backlog é implementado como um **Pull Request** numerado e
autocontido (p. ex. *PR 31 — gerador de questões com IA*, *PR 42 — quiz
adaptativo*, *PR 60 — redesenho em capítulos*), com mensagem de commit
descrevendo a motivação, a solução e a verificação realizada. Essa
correspondência *item de backlog ↔ cartão Kanban ↔ Pull Request* dá
rastreabilidade completa: o histórico de versionamento (Git) materializa o fluxo
de trabalho e serve de registro de evolução do produto.

> **Referências (Metodologia):** SCHWABER, K.; SUTHERLAND, J. *The Scrum
> Guide*. ANDERSON, D. J. *Kanban: Successful Evolutionary Change for Your
> Technology Business* (2010). OHNO, T. *Toyota Production System* (origem do
> Lean/Kanban). LADAS, C. *Scrumban: Essays on Kanban Systems for Lean Software
> Development* (2009). *Manifesto Ágil* (2001). Ajuste a formatação conforme a
> norma exigida (ABNT) na versão final.
