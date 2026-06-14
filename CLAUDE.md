# Unravel — Contexto para AI

Plataforma educacional (TCC ULBRA Torres, mascote NAVI). **Backend** .NET 8
(Clean Arch: Domain / Application / Infrastructure / API) + **Frontend**
React + Vite + TanStack Query + Tailwind + shadcn. Branch: `development`.
Postgres em Docker (`unravel_postgres`, porta 5433).

## Subir local

```bash
docker start unravel_postgres
cd backend && ASPNETCORE_ENVIRONMENT=Development dotnet run \
  --project src/Unravel.API/Unravel.API.csproj --no-launch-profile -- --urls http://localhost:5000
```
- A API **trava as DLLs** enquanto roda → pra rebuild completo, pare o
  processo antes (`Stop-Process` no PID do `dotnet run`). Pra só typecheckar
  sem parar: `dotnet build ... -o /tmp/output-separado`.
- OpenAI key: env var `OPENAI_API_KEY` (Windows User env) — **nunca** no chat
  nem em appsettings. O env var tem precedência sobre configuration no DI.
- Migrations são do **usuário**: eu altero código C#, ele gera/aplica a migration.

## Geração de perguntas — modelo mental (IMPORTANTE)

Existem **duas coisas diferentes** que parecem "perguntas do moderador":

| | `GeneratedChallenge` | `ModeratorGoldItem` (gold) |
|---|---|---|
| **Vai pro aluno?** | ✅ sim (quiz) | ❌ não |
| **Conta no readiness?** | ✅ sim | ❌ não |
| **Pra que serve** | conteúdo servido | **gabarito de avaliação** (`forge:eval`) |
| **Strategies** | `LlmGrounded` (IA), `ModeratorAuthored` (autoral) | n/a |

- O quiz e o readiness **só leem `GeneratedChallenge` ativo**, agrupado por
  `BodyJson.sourceChunkIndex`.
- Gold (`ModeratorGoldItem`) é lido **só** pelo `ModeratorGoldReader` →
  `forge:eval` (benchmark da qualidade da IA). **Criar gold NÃO coloca
  pergunta pro aluno** nem fecha gap de capítulo.
- Pergunta escrita à mão pelo moderador (PR 60-f) = `GeneratedChallenge`
  com `Strategy=ModeratorAuthored` → serve ao aluno e conta no readiness.

## Capítulos e readiness (PR 60)

- `ChunkSegmenter` fatia o markdown por **H2 (`##`)** → capítulos.
- Cada capítulo precisa de **≥4 perguntas** (`minRequiredPerChapter`) pra
  publicar; quota adaptativa de **4–7** por dificuldade no quiz.
- Readiness: `GET /api/admin/contents/{id}/publication-readiness`.

### Perguntas por capítulo (PR 60-f / 60-f bis) — fluxo do moderador

No editor de conteúdo, o `ChapterQuestionsPanel` mostra cada capítulo com
`current/required` e duas ações **scoped àquele capítulo**:

1. **Gerar IA** → `POST /api/admin/forge/{id}?chunkIndex=N` (extrai claims
   só do chunk N; antes o top-N global podia nunca cobrir um capítulo fraco).
   Consome lã (tokens).
2. **Escrever** → `POST /api/admin/contents/{id}/questions` (autoral, MCQ).
   Não consome lã.

Edição/remoção (`PUT`/`DELETE /api/admin/contents/{id}/questions/{cid}`):
- **Qualquer** pergunta é editável/removível — autoral OU da IA.
- Editar uma da IA **preserva procedência e shape**: continua
  `LlmGrounded`/`FillInTheBlank`, badge segue "IA", só o conteúdo muda.

Arquivos-chave:
- BE: `AdminController` (endpoints `forge`, `contents/{id}/questions`),
  `ContentChaptersService` (readiness/chapters), `AuthoredQuestion`
  (factory pura: validação + posição determinística da correta),
  `QuestionForgeWorker` (persiste `sourceChunkIndex` no BodyJson).
- FE: `features/admin-custom/chapter-questions-panel.tsx`,
  `generate-questions-dialog.tsx` (prop `chunkIndex`),
  `content-questions-manager.tsx` (agora só o gold/benchmark),
  `api/chapters.ts`, `types/chapters.ts`.

## Pipeline LLM-grounded (qualidade ~90% em conteúdo avançado)

ClaimExtractor → ClaimShapeRouter → PromptBuilder → OpenAI (gpt-4o-mini,
escala pra gpt-4o na cauda) → validators (schema, leakage, groundedness,
distractor grammar/diversity, blank placement). Retry com **reflexion**
(feedback da falha no prompt) + **shape fallback** + **filtro de claims**.

- Yield estabilizou em ~90%. Os ~10% residuais são **claims de definição
  atômica** ("O Fiber é o motor de reconciliação") que só rendem pergunta de
  rótulo (decoreba) — o validador rejeitar isso é o **comportamento correto**,
  não um bug. Não empurrar via flashcard/recall (contraria a tese anti-decoreba).
- Config: `Llm:OpenAi:EscalationModel=gpt-4o`, `EscalateAfterPriorAttempts=2`.

## Testes

`cd backend && dotnet test Unravel.sln`. Nota: 1 teste de integração do
**Ollama** falha sem GPU/modelo servindo (CUDA OOM / timeout) — **ambiental**,
sem relação com código; produção usa OpenAI, não Ollama.
