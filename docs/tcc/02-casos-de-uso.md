# Unravel — Modelagem de Casos de Uso

> Complemento da documentação acadêmica. Cobre casos de uso **implementados** e
> **planejados** (próximas implementações), conforme o backlog e os documentos
> de visão (pasta `arquivos_unravel`).
>
> **Legenda de situação:** `[I]` Implementado · `[P]` Parcial (núcleo pronto,
> em evolução) · `[F]` Futuro/planejado.

---

## 3.1 Atores

| Ator | Tipo | Descrição |
|---|---|---|
| **Aluno** | Primário (humano) | Estudante que consome trilhas, estuda capítulos, responde quizzes e progride na jornada. Principal usuário do sistema. |
| **Moderador** | Primário (humano) | Cria e cura trilhas, conteúdos e questões; gera questões por IA, escreve/edita/remove questões e publica trilhas. |
| **Professor** | Primário (humano) — **especialização de Moderador** | *(Planejado)* É um Moderador que, além de curar conteúdo, conduz atividades ao vivo (Modo Aula) e acompanha o desempenho de uma turma. **Pode ser a mesma pessoa que o Moderador.** |
| **Administrador** | Primário (humano) — **especialização de Moderador** | É um Moderador com permissões operacionais sobre a plataforma: dispara o replanejamento manualmente, importa trilhas do repositório de conhecimento e monitora a geração de questões globalmente. **Pode ser a mesma pessoa que o Moderador.** |

> **Generalização de ator (UML):** `Professor ──▷ Moderador` e
> `Administrador ──▷ Moderador`. Ambos **herdam** os casos de uso do Moderador
> (UC20–UC28): o Professor acrescenta os do Modo Aula (UC29–UC30) e o
> Administrador acrescenta os de operação da plataforma (UC36–UC38). Na prática
> — e no modelo de papéis implementado, que possui apenas os perfis *Aluno* e
> *Moderador* — **Professor, Administrador e Moderador são o mesmo perfil**; os
> dois primeiros descrevem conjuntos adicionais de capacidades exercidos por um
> Moderador. Não é necessário criar perfis de acesso separados.
| **Sistema / Agente automático** | Secundário (não-humano) | Processos autônomos: replanejamento da jornada, *worker* de geração de questões, desativação de questões ruins, recargas periódicas. |
| **Serviço de IA (OpenAI)** | Secundário (externo) | Provedor externo de LLM usado pela geração de questões fundamentadas. |

---

## 3.2 Diagrama de Casos de Uso

O diagrama completo está disponível em duas formas:

- **Imagem renderizada:** `docs/tcc/use-case-diagram.svg` (visualização rápida).
- **Fonte editável (PlantUML):** `docs/tcc/use-case-diagram.puml` — pode ser
  renderizada em <https://www.plantuml.com/plantuml> ou na extensão PlantUML do
  VS Code para gerar a imagem na resolução/formatação desejada.

O diagrama está particionado em quatro subsistemas para legibilidade:
**Aprendizagem** (Aluno), **Curadoria de Conteúdo** (Moderador),
**Gamificação e Social** (Aluno) e **Automação** (Sistema), além do subsistema
**Sala de Aula** (Professor, planejado).

---

## 3.3 Lista de Casos de Uso

### Aluno
| ID | Caso de uso | Situação |
|---|---|---|
| UC35 | Cadastrar-se (criar conta) | `[I]` |
| UC01 | Autenticar-se | `[I]` |
| UC02 | Realizar nivelamento inicial | `[I]` |
| UC03 | Visualizar painel da jornada do dia | `[I]` |
| UC04 | Descobrir e explorar trilhas | `[I]` |
| UC05 | Iniciar jornada em uma trilha | `[I]` |
| UC06 | Estudar conteúdo em capítulos | `[I]` |
| UC07 | Responder questões do quiz | `[I]` |
| UC08 | Realizar quiz adaptativo | `[I]` |
| UC09 | Enfrentar Boss Fight | `[I]` |
| UC10 | Realizar quiz de reforço | `[I]` |
| UC11 | Visualizar mapa de progresso da trilha | `[I]` |
| UC12 | Consultar radar de fraquezas | `[I]` |
| UC13 | Visualizar perfil e progresso | `[P]` |
| UC14 | Registrar login diário e ofensiva | `[P]` |
| UC15 | Comprar e equipar cosméticos do NAVI | `[F]` |
| UC16 | Conquistar badges/títulos e ver ranking | `[F]` |
| UC17 | Formar parceria colaborativa | `[F]` |
| UC18 | Participar de grupo (Caixinha de Gatos) | `[F]` |
| UC19 | Disputar partida na Arena (PvP) | `[F]` |

### Moderador
| ID | Caso de uso | Situação |
|---|---|---|
| UC20 | Criar e editar trilha personalizada | `[I]` |
| UC21 | Criar e editar conteúdo | `[I]` |
| UC22 | Gerar questões por IA | `[I]` |
| UC23 | Escrever questão autoral | `[I]` |
| UC24 | Editar e remover questões | `[I]` |
| UC25 | Curar gabarito de avaliação | `[I]` |
| UC26 | Acompanhar fila de geração | `[I]` |
| UC27 | Gerenciar saldo de tokens (Novelo de Lã) | `[I]` |
| UC28 | Publicar trilha | `[I]` |

### Professor (planejado)
| ID | Caso de uso | Situação |
|---|---|---|
| UC29 | Conduzir sessão de quiz ao vivo (Modo Aula) | `[F]` |
| UC30 | Consultar relatório pedagógico da turma | `[F]` |

### Administrador
| ID | Caso de uso | Situação |
|---|---|---|
| UC36 | Disparar replanejamento manualmente | `[I]` |
| UC37 | Importar trilhas do repositório de conhecimento | `[I]` |
| UC38 | Monitorar a geração de questões (visão global) | `[I]` |

### Sistema
| ID | Caso de uso | Situação |
|---|---|---|
| UC31 | Replanejar jornadas | `[I]` |
| UC32 | Processar geração de questões | `[I]` |
| UC33 | Desativar questões de baixa qualidade | `[P]` |
| UC34 | Recarregar recursos (refill) | `[F]` |

---

## 3.4 Especificações dos Casos de Uso

### Subsistema: Aprendizagem (Aluno)

---

#### UC35 — Cadastrar-se (criar conta) `[I]`
- **Descrição:** Permite a uma nova pessoa criar uma conta na plataforma,
  informando seus dados de acesso.
- **Atores:** Aluno (visitante); Moderador.
- **Pré-condição:** Não possuir conta com o mesmo e-mail.
- **Fluxo principal:**
  1. O visitante acessa a tela de cadastro.
  2. Informa nome, e-mail e senha.
  3. O sistema valida os dados e a unicidade do e-mail.
  4. O sistema cria a conta (com a senha armazenada de forma segura) e a
     autentica (UC01).
- **Pós-condição:** Conta criada e ativa; usuário autenticado.
- **Fluxo alternativo:**
  - **A1 — E-mail já cadastrado:** o sistema informa o conflito e mantém a tela.
  - **A2 — Dados inválidos:** o sistema indica os campos a corrigir.
- **Observações:** A senha é armazenada com *hash* (BCrypt); novos usuários
  recebem, por padrão, o perfil Aluno.

---

#### UC01 — Autenticar-se `[I]`
- **Descrição:** Permite ao usuário entrar na plataforma com e-mail e senha,
  obtendo acesso às funcionalidades conforme seu perfil.
- **Atores:** Aluno, Moderador.
- **Pré-condição:** Possuir conta cadastrada e ativa.
- **Fluxo principal:**
  1. O usuário acessa a tela de login.
  2. Informa e-mail e senha.
  3. O sistema valida as credenciais.
  4. O sistema emite um token de acesso (JWT) e direciona para o painel inicial.
- **Pós-condição:** Sessão autenticada; usuário com acesso às telas do seu perfil.
- **Fluxo alternativo:**
  - **A1 — Credenciais inválidas:** o sistema exibe mensagem de erro e mantém a
    tela de login.
  - **A2 — Sessão expirada:** ao detectar token inválido em uma requisição, o
    sistema tenta a renovação automática (*refresh*); falhando, encerra a sessão.
- **Observações:** Senhas são armazenadas com *hash* (BCrypt); o perfil
  (Aluno/Moderador) é codificado no token e define as telas acessíveis.

---

#### UC02 — Realizar nivelamento inicial `[I]`
- **Descrição:** No primeiro acesso, o sistema estima o nível de domínio do
  estudante para calibrar a dificuldade inicial das questões (*cold-start*).
- **Atores:** Aluno.
- **Pré-condição:** Usuário autenticado, sem histórico de domínio registrado.
- **Fluxo principal:**
  1. O sistema apresenta o fluxo de nivelamento (onboarding).
  2. O aluno responde a um conjunto inicial de questões.
  3. O sistema calcula uma estimativa de domínio por tópico.
  4. O sistema persiste o *mastery* inicial e personaliza a primeira jornada.
- **Pós-condição:** Perfil de domínio inicial registrado; jornada calibrada.
- **Fluxo alternativo:**
  - **A1 — Aluno pula o nivelamento:** o sistema assume um nível padrão
    conservador e ajusta ao longo das primeiras sessões.
- **Observações:** O domínio é continuamente refinado a cada resposta (ver UC07
  e UC31), aplicando o princípio de *mastery learning*.

---

#### UC03 — Visualizar painel da jornada do dia `[I]`
- **Descrição:** Exibe ao aluno o que estudar hoje, com base na jornada
  planejada e em suas estatísticas de progresso.
- **Atores:** Aluno.
- **Pré-condição:** Usuário autenticado com ao menos uma jornada ativa.
- **Fluxo principal:**
  1. O aluno acessa o painel inicial (dashboard).
  2. O sistema consulta a jornada planejada e o progresso do dia.
  3. O sistema apresenta as atividades recomendadas e os indicadores (XP,
     ofensiva, etc.).
- **Pós-condição:** Aluno informado das atividades do dia.
- **Fluxo alternativo:**
  - **A1 — Sem jornada ativa:** o sistema sugere descobrir/criar uma jornada
    (UC04/UC05).
- **Observações:** As recomendações vêm do planejador de jornada, atualizado
  diariamente pelo UC31.

---

#### UC04 — Descobrir e explorar trilhas `[I]`
- **Descrição:** Permite ao aluno navegar pelo catálogo de trilhas disponíveis
  e ver detalhes antes de se inscrever.
- **Atores:** Aluno.
- **Pré-condição:** Usuário autenticado.
- **Fluxo principal:**
  1. O aluno acessa a página de descoberta de trilhas.
  2. O sistema lista as trilhas publicadas.
  3. O aluno seleciona uma trilha para ver detalhes (descrição, nível, conteúdos).
- **Pós-condição:** Aluno conhece as trilhas disponíveis.
- **Fluxo alternativo:**
  - **A1 — Nenhuma trilha publicada:** o sistema exibe estado vazio informativo.
- **Observações:** Apenas trilhas que atendem ao *gate* de publicação (UC28) são
  listadas.

---

#### UC05 — Iniciar jornada em uma trilha `[I]`
- **Descrição:** Permite ao aluno se inscrever em uma trilha, gerando uma
  jornada personalizada de estudo.
- **Atores:** Aluno.
- **Pré-condição:** Usuário autenticado; trilha publicada selecionada.
- **Fluxo principal:**
  1. O aluno escolhe "Criar jornada" a partir de uma trilha.
  2. O sistema monta o plano da jornada (ordem de conteúdos/tópicos) considerando
     o domínio atual do aluno.
  3. O sistema registra a inscrição e exibe a jornada criada.
- **Pós-condição:** Jornada ativa associada ao aluno.
- **Fluxo alternativo:**
  - **A1 — Já inscrito:** o sistema direciona para a jornada existente.
- **Observações:** A jornada é replanejada periodicamente (UC31).

---

#### UC06 — Estudar conteúdo em capítulos `[I]`
- **Descrição:** Conduz o aluno por um conteúdo dividido em capítulos (estudo
  guiado): ler o capítulo → praticar questões → recapitular → revisão final.
- **Atores:** Aluno.
- **Pré-condição:** Conteúdo disponível com capítulos e questões suficientes.
- **Fluxo principal:**
  1. O aluno inicia o estudo de um conteúdo.
  2. O sistema apresenta o primeiro capítulo (texto) para leitura.
  3. O aluno avança para a prática e responde às questões do capítulo (UC07).
  4. O sistema apresenta uma recapitulação do capítulo.
  5. Repetem-se os passos 2–4 para os demais capítulos.
  6. Ao final, o sistema apresenta uma revisão geral e conclui a sessão.
- **Pós-condição:** Progresso do conteúdo atualizado; domínio recalculado.
- **Fluxo alternativo:**
  - **A1 — Aluno interrompe a sessão:** o sistema preserva o progresso até o
    último passo concluído.
- **Observações:** O fatiamento usa os títulos de seção (H2) do material; a
  prática intercalada por capítulos é a principal estratégia anti-decoreba.

---

#### UC07 — Responder questões do quiz `[I]`
- **Descrição:** Permite ao aluno responder questões (múltipla escolha ou
  completar lacuna) e receber correção e explicação imediatas.
- **Atores:** Aluno.
- **Pré-condição:** Sessão de estudo/quiz em andamento com questões disponíveis.
- **Fluxo principal:**
  1. O sistema apresenta a questão (enunciado e alternativas).
  2. O aluno seleciona/preenche a resposta e confirma.
  3. O sistema corrige, indica acerto/erro e mostra a explicação.
  4. O sistema registra a resposta e atualiza o domínio do tópico.
- **Pós-condição:** Resposta registrada; *mastery* e gamificação atualizados.
- **Fluxo alternativo:**
  - **A1 — Resposta incorreta:** a questão pode ser marcada para reforço futuro
    (UC10).
- **Observações:** Suporta os formatos *múltipla escolha* e *completar lacuna*;
  o feedback imediato apoia o *testing effect*.

---

#### UC08 — Realizar quiz adaptativo `[I]`
- **Descrição:** Oferece uma sessão cuja dificuldade se ajusta dinamicamente ao
  desempenho do aluno (teste adaptativo), buscando a zona proximal de
  aprendizagem.
- **Atores:** Aluno.
- **Pré-condição:** Conteúdo com pool de questões suficiente.
- **Fluxo principal:**
  1. O aluno inicia o quiz adaptativo.
  2. O sistema seleciona a próxima questão com base no histórico curto da sessão
     (habilidade estimada online).
  3. O aluno responde (UC07).
  4. O sistema reavalia a habilidade e decide continuar ou encerrar (critério de
     parada).
  5. O sistema apresenta o resultado.
- **Pós-condição:** Estimativa de habilidade e domínio atualizados.
- **Fluxo alternativo:**
  - **A1 — Pool esgotado:** o sistema encerra a sessão informando o motivo.
- **Observações:** Baseado em lógica de *Computerized Adaptive Testing* (CAT)
  simplificada.

---

#### UC09 — Enfrentar Boss Fight `[I]`
- **Descrição:** Desafio especial que combina questões de múltiplos tópicos de
  uma trilha, como marco de consolidação.
- **Atores:** Aluno.
- **Pré-condição:** Progresso mínimo na trilha; questões disponíveis nos tópicos.
- **Fluxo principal:**
  1. O aluno inicia o Boss Fight da trilha.
  2. O sistema monta um conjunto combinatorial de questões dos tópicos cobertos.
  3. O aluno responde à sequência (UC07).
  4. O sistema apura o resultado e concede recompensas.
- **Pós-condição:** Resultado e recompensas registrados; progresso atualizado.
- **Fluxo alternativo:**
  - **A1 — Desempenho insuficiente:** o sistema sinaliza tópicos a reforçar
    (UC12) e libera nova tentativa.
- **Observações:** Atua como avaliação de transferência entre tópicos
  (*interleaving* em escala de trilha).

---

#### UC10 — Realizar quiz de reforço `[I]`
- **Descrição:** Sessão focada em revisar questões/tópicos nos quais o aluno
  apresentou dificuldade, aplicando repetição espaçada.
- **Atores:** Aluno.
- **Pré-condição:** Existirem itens marcados para reforço.
- **Fluxo principal:**
  1. O aluno inicia o quiz de reforço.
  2. O sistema seleciona itens prioritários (erros recentes e revisões devidas).
  3. O aluno responde (UC07).
  4. O sistema atualiza os agendamentos de revisão.
- **Pós-condição:** Agenda de repetição espaçada atualizada.
- **Fluxo alternativo:**
  - **A1 — Nada a reforçar:** o sistema informa que não há itens pendentes.
- **Observações:** Operacionaliza o *spaced repetition* (SRS).

---

#### UC11 — Visualizar mapa de progresso da trilha `[I]`
- **Descrição:** Exibe a trilha como um mapa visual de etapas, indicando o que
  foi concluído, o que está liberado e o próximo passo.
- **Atores:** Aluno.
- **Pré-condição:** Jornada ativa na trilha.
- **Fluxo principal:**
  1. O aluno acessa o mapa da trilha.
  2. O sistema apresenta as etapas com seus estados (concluída/atual/bloqueada).
  3. O aluno seleciona uma etapa liberada para estudar (UC06).
- **Pós-condição:** Aluno orientado quanto ao próximo passo.
- **Fluxo alternativo:**
  - **A1 — Etapa bloqueada:** o sistema informa o pré-requisito necessário.
- **Observações:** Metáfora de progressão em mapa (inspiração de jogos);
  evolução planejada para progresso por **capítulo** (não só por conteúdo).
- **Observações (planejado):** Granularidade por capítulo prevista no backlog.

---

#### UC12 — Consultar radar de fraquezas `[I]`
- **Descrição:** Mostra ao aluno um panorama de domínio por tópico na trilha,
  destacando pontos fortes e fracos.
- **Atores:** Aluno.
- **Pré-condição:** Histórico de respostas suficiente para estimar domínio.
- **Fluxo principal:**
  1. O aluno acessa o radar de fraquezas.
  2. O sistema agrega o *mastery* por tópico e apresenta a visualização.
  3. O aluno identifica tópicos a priorizar.
- **Pós-condição:** Aluno informado sobre lacunas de domínio.
- **Fluxo alternativo:**
  - **A1 — Dados insuficientes:** o sistema sugere estudar mais para gerar
    estimativas confiáveis.
- **Observações:** Apoia decisões de estudo alinhadas a *mastery learning*.

---

#### UC13 — Visualizar perfil e progresso `[P]`
- **Descrição:** Exibe dados do aluno e indicadores acumulados (XP, ofensiva,
  vidas, moedas, conquistas).
- **Atores:** Aluno.
- **Pré-condição:** Usuário autenticado.
- **Fluxo principal:**
  1. O aluno acessa o perfil.
  2. O sistema apresenta os indicadores e o histórico resumido.
- **Pós-condição:** Aluno informado de seu progresso.
- **Fluxo alternativo:** —
- **Observações:** Indicadores básicos implementados; exibição completa de
  conquistas/títulos depende de UC16.

---

#### UC14 — Registrar login diário e ofensiva `[P]`
- **Descrição:** Reconhece o acesso diário do aluno, avança o ciclo semanal de
  recompensas e contabiliza a ofensiva (dias consecutivos).
- **Atores:** Aluno; Sistema.
- **Pré-condição:** Usuário autenticado; primeiro acesso do dia.
- **Fluxo principal:**
  1. No primeiro acesso do dia, o sistema registra a atividade.
  2. Incrementa o dia do ciclo semanal e concede a recompensa correspondente
     (vidas, moedas ou bônus de XP).
  3. Atualiza a ofensiva e verifica marcos (7, 14, 30, 60, 100 dias).
- **Pós-condição:** Recompensa do dia concedida; ofensiva atualizada.
- **Fluxo alternativo:**
  - **A1 — Quebra de ofensiva:** ausência além do permitido zera o contador de
    dias consecutivos.
- **Observações:** Entidades e parte da lógica existem; o ciclo completo de
  recompensas integra-se a UC15/UC16. Principal motor de retenção.

---

### Subsistema: Gamificação e Social (Aluno) — planejado

---

#### UC15 — Comprar e equipar cosméticos do NAVI `[F]`
- **Descrição:** Permite ao aluno gastar moedas em itens cosméticos (chapéus,
  expressões, poses, etc.) para personalizar o mascote NAVI.
- **Atores:** Aluno.
- **Pré-condição:** Usuário autenticado com saldo de moedas.
- **Fluxo principal:**
  1. O aluno acessa a loja cosmética.
  2. Seleciona um item e confirma a compra.
  3. O sistema debita as moedas e adiciona o item ao inventário.
  4. O aluno equipa o item no NAVI.
- **Pós-condição:** Item adquirido e/ou equipado; saldo atualizado.
- **Fluxo alternativo:**
  - **A1 — Saldo insuficiente:** o sistema bloqueia a compra e informa o valor
    faltante.
- **Observações:** Cosméticos não conferem vantagem competitiva (apenas
  personalização). Itens possuem raridade (comum→lendário).

---

#### UC16 — Conquistar badges/títulos e ver ranking `[F]`
- **Descrição:** Concede conquistas (badges) e títulos por marcos de desempenho
  e exibe rankings.
- **Atores:** Aluno; Sistema.
- **Pré-condição:** Usuário autenticado.
- **Fluxo principal:**
  1. Ao atingir um critério (ofensiva, taxa de acerto, etc.), o sistema concede a
     badge/título correspondente.
  2. O aluno visualiza suas conquistas no perfil.
  3. O aluno consulta o ranking (global e/ou por trilha).
- **Pós-condição:** Conquista registrada; ranking atualizado.
- **Fluxo alternativo:** —
- **Observações:** Títulos combinam raças de gato + jargão de TI (ex.:
  "CSSiamês Profissional"). Categorias: ofensiva, velocidade, conhecimento,
  social, evento, etc.

---

#### UC17 — Formar parceria colaborativa `[F]`
- **Descrição:** Mecânica social em que dois alunos firmam uma parceria e
  mantêm um compromisso mútuo de estudo (passagem de "novelo" entre si).
- **Atores:** Aluno.
- **Pré-condição:** Usuário autenticado; convite aceito por outro aluno.
- **Fluxo principal:**
  1. O aluno convida outro para parceria.
  2. O parceiro aceita.
  3. Os parceiros cumprem metas alternadas, "passando o novelo" a cada ciclo.
  4. O sistema concede pontos de parceria a cada troca bem-sucedida.
- **Pós-condição:** Parceria ativa; pontos de parceria atualizados.
- **Fluxo alternativo:**
  - **A1 — Parceiro inativo:** o sistema sinaliza risco de quebra do compromisso.
- **Observações (CONFLITO DE NOMENCLATURA):** Nos documentos de visão essa
  mecânica chama-se **"Novelo de Lã"**. Porém, **no que foi implementado** o
  termo "Novelo de Lã" designa a **moeda do moderador** para custear geração de
  questões por IA (ver UC27). Recomenda-se, na versão final do TCC, **renomear**
  uma das duas para evitar ambiguidade (p. ex. manter "Novelo de Lã" como a moeda
  e chamar esta mecânica de **"Parceria"** ou **"Dupla"**).

---

#### UC18 — Participar de grupo (Caixinha de Gatos) `[F]`
- **Descrição:** Permite ao aluno integrar um grupo/clã com metas coletivas e
  competições entre grupos.
- **Atores:** Aluno (membro ou líder).
- **Pré-condição:** Usuário autenticado.
- **Fluxo principal:**
  1. O aluno cria ou entra em uma "Caixinha".
  2. Os membros contribuem com pontos por meio de seu estudo individual.
  3. O sistema agrega a pontuação do grupo e o classifica em eventos coletivos.
- **Pós-condição:** Pontuação do grupo atualizada; posição em ranking coletivo.
- **Fluxo alternativo:**
  - **A1 — Grupo cheio:** o sistema impede novas entradas além do limite.
- **Observações:** Cria responsabilidade social positiva (o sucesso do grupo
  depende do esforço individual). Papéis de **líder** e **membro**.

---

#### UC19 — Disputar partida na Arena (PvP) `[F]`
- **Descrição:** Modo competitivo em tempo real no qual dois alunos respondem
  questões em confronto direto.
- **Atores:** Aluno.
- **Pré-condição:** Usuário autenticado; adversário disponível (matchmaking).
- **Fluxo principal:**
  1. O aluno entra na fila da Arena.
  2. O sistema pareia dois jogadores e inicia a partida em tempo real.
  3. Ambos respondem às mesmas questões; o sistema pontua por acerto e rapidez.
  4. Ao final, declara o vencedor e concede pontos de Arena.
- **Pós-condição:** Resultado e pontos de Arena registrados.
- **Fluxo alternativo:**
  - **A1 — Adversário desconecta:** o sistema concede vitória por desistência.
- **Observações:** Requer comunicação em tempo real (WebSocket). Ranking de
  Arena separado do ranking global.

---

### Subsistema: Curadoria de Conteúdo (Moderador)

---

#### UC20 — Criar e editar trilha personalizada `[I]`
- **Descrição:** Permite ao moderador criar trilhas próprias e editar seus
  metadados (nome, descrição, nível, ícone, cor).
- **Atores:** Moderador.
- **Pré-condição:** Usuário autenticado com perfil Moderador.
- **Fluxo principal:**
  1. O moderador acessa a gestão de trilhas.
  2. Cria uma trilha informando nome e nível (gera um identificador único).
  3. Edita os metadados quando necessário.
- **Pós-condição:** Trilha criada/atualizada (inicialmente não publicada).
- **Fluxo alternativo:**
  - **A1 — Identificador em uso:** o sistema solicita outro nome/identificador.
- **Observações:** Trilhas criadas por moderador são distintas das importadas via
  repositório de conhecimento.

---

#### UC21 — Criar e editar conteúdo `[I]`
- **Descrição:** Permite ao moderador escrever o material de um conteúdo em
  Markdown, com pré-visualização.
- **Atores:** Moderador.
- **Pré-condição:** Trilha existente do moderador.
- **Fluxo principal:**
  1. O moderador cria um conteúdo na trilha.
  2. Escreve o material em Markdown, usando títulos de seção (H2) para demarcar
     capítulos.
  3. Salva as alterações.
- **Pós-condição:** Conteúdo salvo e fatiável em capítulos.
- **Fluxo alternativo:**
  - **A1 — Edição do corpo:** ao alterar o texto, as questões existentes são
    invalidadas (desativadas) para evitar desalinhamento com o novo conteúdo.
- **Observações:** Os títulos H2 definem os capítulos usados em UC06, UC22 e UC28.

---

#### UC22 — Gerar questões por IA `[I]`
- **Descrição:** Permite ao moderador disparar a geração automática de questões
  fundamentadas no conteúdo, por conteúdo inteiro, por trilha (em lote) ou por
  **capítulo específico**.
- **Atores:** Moderador; Sistema; Serviço de IA (OpenAI).
- **Pré-condição:** Conteúdo salvo; saldo de tokens (Novelo de Lã) suficiente.
- **Fluxo principal:**
  1. O moderador escolhe o escopo (conteúdo, trilha ou capítulo) e a quantidade.
  2. O sistema estima o custo em tokens e o exibe.
  3. O moderador confirma; o sistema debita os tokens e enfileira os trabalhos de
     geração.
  4. O *worker* processa os trabalhos (UC32) e as questões válidas passam a
     compor o pool do conteúdo.
- **Pós-condição:** Trabalhos enfileirados; questões válidas adicionadas ao pool.
- **Fluxo alternativo:**
  - **A1 — Saldo insuficiente:** o sistema bloqueia a operação e informa o valor
    faltante.
  - **A2 — Nenhuma afirmação extraível:** o sistema informa que não há base para
    gerar questões naquele escopo.
- **Observações:** A geração por capítulo resolve o caso de capítulos "fracos"
  que, na seleção global por relevância, dificilmente seriam cobertos. Custo:
  geração normal 1 token/trabalho; urgente 3 tokens/trabalho.

---

#### UC23 — Escrever questão autoral `[I]`
- **Descrição:** Permite ao moderador escrever manualmente uma questão (múltipla
  escolha) associada a um capítulo, que passa a valer para o aluno.
- **Atores:** Moderador.
- **Pré-condição:** Conteúdo com capítulos definidos.
- **Fluxo principal:**
  1. O moderador escolhe um capítulo e abre o formulário de questão.
  2. Informa enunciado, resposta correta, três distratores e (opcional)
     explicação.
  3. O sistema valida (enunciado mínimo; quatro opções distintas) e salva a
     questão como parte do pool do capítulo.
- **Pós-condição:** Questão autoral disponível ao aluno; prontidão do capítulo
  recalculada.
- **Fluxo alternativo:**
  - **A1 — Validação falha:** o sistema indica o problema (ex.: opções
    repetidas) e mantém o formulário.
- **Observações:** Não consome tokens (sem custo de IA). Diferente do gabarito
  (UC25), a questão autoral **vai para o aluno** e **conta na prontidão**.

---

#### UC24 — Editar e remover questões `[I]`
- **Descrição:** Permite ao moderador revisar o pool de um capítulo, editando ou
  removendo questões — tanto as **autorais** quanto as **geradas pela IA**.
- **Atores:** Moderador.
- **Pré-condição:** Existirem questões no conteúdo.
- **Fluxo principal:**
  1. O moderador localiza a questão no capítulo (lista expansível por capítulo).
  2. Edita o enunciado/opções/explicação **ou** remove a questão.
  3. O sistema salva a alteração e recalcula a prontidão do capítulo.
- **Pós-condição:** Questão atualizada/desativada; prontidão recalculada.
- **Fluxo alternativo:**
  - **A1 — Edição de questão da IA:** o sistema **preserva a procedência e o
    formato** originais (a questão continua identificada como "IA" e mantém seu
    tipo); apenas o conteúdo muda.
- **Observações:** A remoção é lógica (*soft-delete*), preservando histórico.

---

#### UC25 — Curar gabarito de avaliação `[I]`
- **Descrição:** Permite ao moderador cadastrar questões de referência (*gold*)
  usadas **somente** para medir a qualidade do gerador de IA.
- **Atores:** Moderador.
- **Pré-condição:** Conteúdo existente.
- **Fluxo principal:**
  1. O moderador cadastra uma questão de referência (manual ou promovendo uma
     gerada).
  2. O sistema armazena o item no conjunto de avaliação.
  3. O conjunto é consumido pela rotina de avaliação do pipeline de IA.
- **Pós-condição:** Item de gabarito registrado.
- **Fluxo alternativo:** —
- **Observações (IMPORTANTE):** O gabarito **não é exibido ao aluno** e **não
  conta** na prontidão do capítulo — serve apenas de *benchmark* da IA. As
  questões do aluno são as de UC22/UC23.

---

#### UC26 — Acompanhar fila de geração `[I]`
- **Descrição:** Permite ao moderador acompanhar o andamento dos lotes de
  geração que disparou (em fila, em processamento, concluídos, falhos).
- **Atores:** Moderador.
- **Pré-condição:** Ter disparado ao menos um lote de geração.
- **Fluxo principal:**
  1. O moderador abre o painel de atividade de geração.
  2. O sistema apresenta os lotes recentes e seus contadores de progresso.
  3. O moderador acompanha a conclusão.
- **Pós-condição:** Moderador informado do estado dos lotes.
- **Fluxo alternativo:** —
- **Observações:** Cada lote é identificável e associado ao moderador que o
  disparou.

---

#### UC27 — Gerenciar saldo de tokens (Novelo de Lã) `[I]`
- **Descrição:** O moderador consulta seu saldo de tokens ("Novelo de Lã") e o
  consome ao gerar questões por IA, pois a geração tem custo computacional/
  financeiro (chamadas ao serviço externo).
- **Atores:** Moderador; Sistema.
- **Pré-condição:** Usuário autenticado com perfil Moderador.
- **Fluxo principal:**
  1. O moderador consulta o saldo atual.
  2. Ao gerar questões (UC22), o sistema debita o custo correspondente.
  3. O sistema registra a transação e atualiza o saldo.
- **Pós-condição:** Saldo atualizado; transação registrada.
- **Fluxo alternativo:**
  - **A1 — Saldo insuficiente:** a geração é bloqueada (ver UC22/A1).
- **Observações:** Recarga periódica prevista em UC34. **Atenção ao conflito de
  nomenclatura descrito em UC17.**

---

#### UC28 — Publicar trilha `[I]`
- **Descrição:** Permite ao moderador publicar uma trilha, tornando-a visível
  aos alunos, condicionado à prontidão de seus conteúdos.
- **Atores:** Moderador.
- **Pré-condição:** Trilha com conteúdos; cada capítulo com a quantidade mínima
  de questões.
- **Fluxo principal:**
  1. O moderador solicita a publicação da trilha.
  2. O sistema verifica a **prontidão**: cada capítulo de cada conteúdo possui ao
     menos o mínimo exigido de questões.
  3. Atendido o critério, o sistema publica a trilha.
- **Pós-condição:** Trilha publicada e disponível em UC04.
- **Fluxo alternativo:**
  - **A1 — Prontidão não atingida:** o sistema bloqueia a publicação e indica os
    capítulos incompletos.
- **Observações:** O *gate* de prontidão (mínimo de 4 questões por capítulo)
  garante qualidade pedagógica mínima antes da exposição ao aluno.

---

### Subsistema: Sala de Aula (Professor) — planejado

---

#### UC29 — Conduzir sessão de quiz ao vivo (Modo Aula) `[F]`
- **Descrição:** Permite ao professor conduzir um quiz síncrono com uma turma,
  controlando o ritmo e visualizando o desempenho em tempo real.
- **Atores:** Professor; Aluno (participante).
- **Pré-condição:** Professor autenticado; banco de questões selecionado.
- **Fluxo principal:**
  1. O professor cria uma sessão e obtém um código de acesso.
  2. Os alunos entram com o código.
  3. O professor libera as questões; os alunos respondem dentro do tempo.
  4. O sistema exibe o ranking/estatísticas a cada rodada.
  5. O professor encerra a sessão.
- **Pós-condição:** Sessão concluída; resultados registrados.
- **Fluxo alternativo:**
  - **A1 — Aluno entra após o início:** o sistema o inclui a partir da rodada
    corrente.
- **Observações:** Inspirado no Kahoot; requer tempo real (WebSocket). Ponte para
  uso em sala de aula sobre o núcleo autônomo da plataforma.

---

#### UC30 — Consultar relatório pedagógico da turma `[F]`
- **Descrição:** Disponibiliza ao professor um relatório com o desempenho da
  turma por questão/tópico após uma sessão de Modo Aula.
- **Atores:** Professor.
- **Pré-condição:** Existir ao menos uma sessão concluída.
- **Fluxo principal:**
  1. O professor seleciona uma sessão encerrada.
  2. O sistema apresenta acertos por questão, lacunas por tópico e desempenho
     individual/agregado.
- **Pós-condição:** Professor informado das lacunas da turma.
- **Fluxo alternativo:** —
- **Observações:** Apoia intervenção pedagógica direcionada.

---

### Subsistema: Automação (Sistema)

---

#### UC31 — Replanejar jornadas `[I]`
- **Descrição:** Rotina automática diária que recalcula as jornadas dos alunos
  conforme o domínio atualizado e a agenda de revisões.
- **Atores:** Sistema.
- **Pré-condição:** Existirem jornadas ativas.
- **Fluxo principal:**
  1. Em horário programado, a rotina é disparada.
  2. Para cada aluno, recalcula prioridades de estudo (novos tópicos + revisões
     devidas).
  3. Persiste o plano atualizado, refletido em UC03.
- **Pós-condição:** Jornadas atualizadas para o dia.
- **Fluxo alternativo:**
  - **A1 — Já executada no dia:** a rotina é idempotente (faz *upsert* sem
    duplicar).
- **Observações:** Pode também ser disparada manualmente por um administrador.

---

#### UC32 — Processar geração de questões `[I]`
- **Descrição:** Serviço em segundo plano que consome a fila de trabalhos de
  geração, invoca o pipeline de IA e persiste as questões válidas.
- **Atores:** Sistema; Serviço de IA (OpenAI).
- **Pré-condição:** Existirem trabalhos pendentes na fila (gerados por UC22).
- **Fluxo principal:**
  1. O *worker* retira o próximo trabalho da fila.
  2. Extrai a afirmação-alvo do conteúdo e invoca o gerador (LLM).
  3. Submete o resultado aos validadores de qualidade.
  4. Sendo válido, persiste a questão associada ao capítulo de origem; senão,
     reprocessa com autocorreção (até um limite de tentativas).
- **Pós-condição:** Questões válidas adicionadas ao pool; trabalhos marcados como
  concluídos ou falhos.
- **Fluxo alternativo:**
  - **A1 — Falha persistente:** após as tentativas, o trabalho é marcado como
    falho e registrado para análise.
  - **A2 — Tentativa em cauda:** em re-tentativas, o sistema pode escalar para um
    modelo de IA mais capaz.
- **Observações:** Validadores incluem checagens de vazamento de resposta,
  fundamentação no texto, qualidade dos distratores e posicionamento de lacuna.
  Processamento sequencial (um por vez) por restrição de recurso.

---

#### UC33 — Desativar questões de baixa qualidade `[P]`
- **Descrição:** Mecanismo que desativa automaticamente questões com indícios de
  baixa qualidade (p. ex. todos acertam ou todos erram com volume relevante).
- **Atores:** Sistema.
- **Pré-condição:** Questões com volume de respostas suficiente.
- **Fluxo principal:**
  1. O sistema avalia estatísticas de acerto por questão.
  2. Identifica questões fora de faixas saudáveis.
  3. Marca essas questões como inativas.
- **Pós-condição:** Pool depurado; questões ruins fora de circulação.
- **Fluxo alternativo:** —
- **Observações:** Complementa os validadores de geração (UC32) com sinal de uso
  real.

---

#### UC34 — Recarregar recursos (refill) `[F]`
- **Descrição:** Rotina periódica que recompõe recursos consumíveis dos usuários
  (p. ex. tokens do moderador e/ou vidas do aluno).
- **Atores:** Sistema.
- **Pré-condição:** Política de recarga definida.
- **Fluxo principal:**
  1. Em intervalo programado, a rotina é disparada.
  2. Recompõe os recursos até os limites definidos por política.
  3. Registra as recargas.
- **Pós-condição:** Recursos recompostos.
- **Fluxo alternativo:** —
- **Observações:** Garante uso sustentável sem dependência exclusiva de eventos
  de recompensa.

---

### Subsistema: Administração (Administrador)

---

#### UC36 — Disparar replanejamento manualmente `[I]`
- **Descrição:** Permite ao administrador executar o replanejamento das jornadas
  sob demanda, sem esperar a rotina automática diária (UC31).
- **Atores:** Administrador.
- **Pré-condição:** Usuário autenticado com permissão administrativa.
- **Fluxo principal:**
  1. O administrador acessa o painel administrativo.
  2. Aciona a execução imediata do replanejamento.
  3. O sistema executa a rotina (mesma lógica do UC31) e exibe o relatório do
     lote.
- **Pós-condição:** Jornadas recalculadas; relatório disponível.
- **Fluxo alternativo:**
  - **A1 — Já executado no dia:** a operação é idempotente (faz *upsert*, não
    duplica).
- **Observações:** Útil para validar mudanças de conteúdo/algoritmo sem aguardar
  o agendamento; o progresso pode ser acompanhado em tempo real (eventos via
  WebSocket).

---

#### UC37 — Importar trilhas do repositório de conhecimento `[I]`
- **Descrição:** Permite ao administrador (re)importar as trilhas/conteúdos
  versionados no repositório (arquivos Markdown), sincronizando-os com o banco.
- **Atores:** Administrador.
- **Pré-condição:** Usuário autenticado com permissão administrativa; arquivos de
  conhecimento disponíveis.
- **Fluxo principal:**
  1. O administrador aciona a importação de conhecimento.
  2. O sistema lê os arquivos do repositório e faz *upsert* por identificador.
  3. O sistema retorna um resumo (trilhas/conteúdos criados e atualizados).
- **Pós-condição:** Catálogo sincronizado com o repositório.
- **Fluxo alternativo:**
  - **A1 — Falha de leitura:** a importação é abortada sem afetar o conteúdo já
    existente.
- **Observações:** Operação idempotente; complementar à criação de trilhas pela
  interface (UC20). Trilhas do repositório são distintas das criadas por
  moderador.

---

#### UC38 — Monitorar a geração de questões (visão global) `[I]`
- **Descrição:** Disponibiliza ao administrador um panorama agregado do pipeline
  de geração: estado da fila, métricas de aproveitamento e principais causas de
  falha.
- **Atores:** Administrador.
- **Pré-condição:** Usuário autenticado com permissão administrativa.
- **Fluxo principal:**
  1. O administrador acessa o painel de geração (forge).
  2. O sistema apresenta o estado da fila e as estatísticas agregadas
     (concluídos, falhos, taxa de aproveitamento, falhas mais comuns).
  3. O administrador interpreta os indicadores para calibrar o pipeline.
- **Pós-condição:** Administrador informado da saúde do pipeline.
- **Fluxo alternativo:** —
- **Observações:** Diferente do UC26 (moderador acompanha **os próprios** lotes);
  aqui a visão é **global** e voltada à operação/qualidade do sistema.

---

## 3.5 Observações gerais

1. **Conflito "Novelo de Lã":** termo usado em dois sentidos (moeda do moderador
   — implementado, UC27; e mecânica de parceria — planejada, UC17). Resolver a
   nomenclatura na versão final (ver UC17).
2. **Situação dos casos de uso:** os marcadores `[I]/[P]/[F]` refletem o estado
   na data desta documentação e devem ser revisados conforme o avanço das
   sprints.
3. **Relações de inclusão:** vários casos de uso de estudo (UC06, UC08, UC09,
   UC10) **incluem** UC07 (responder questões); UC22 **inclui** UC32 (o disparo
   da geração depende do processamento em segundo plano). Essas relações
   `<<include>>` estão refletidas no diagrama PlantUML.
