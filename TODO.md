# NekoLib — Plano de Ação

---

## Notas gerais de implementação

- **Keyword `record`**: não usar como keyword em tipos compartilhados entre targets. `record` (C# 9) requer `System.Runtime.CompilerServices.IsExternalInit` que não existe no net481 sem shim explícito. Usar classes normais para tipos de dados em projetos multi-target.
- **README**: atualizar `README.md` / `CLAUDE.md` ao concluir cada etapa, refletindo a nova estrutura de projetos e o grafo de dependências.
- **`NullDebugUtils`**: segue o padrão Null Object, consistente com `NullLogger` e `NullTelemetrySink` já existentes. A implementação satisfaz a interface sem fazer nada — o caller nunca precisa checar `if (debug != null)`.
- **Pausa entre etapas**: ao final de cada etapa (após commit e atualização do TODO/CLAUDE.md), fazer uma pausa para o usuário verificar contexto/tokens antes de continuar. **Push só ao final de cada fase** (ou quando solicitado), não a cada etapa.
- **⚠ Build não disponível no ambiente remoto**: o SDK .NET não está instalado e os CDNs (aka.ms / builds.dotnet) estão bloqueados (403). Além disso, `net481` e os targets `-windows` só compilam no Windows. **Toda validação de compilação (A8, B5) é feita pelo usuário na máquina Windows / VS2022.** As etapas aqui são revisadas por inspeção manual.

---

## Fase A — Reestruturação de base e desbloqueio multi-target

> Objetivo: separar contratos de implementação, destravar `net9.0` nos módulos que não precisam de Windows, e criar o projeto `NekoLib.Core` como base da pirâmide de dependências.

### A1 — Criar `NekoLib.Core` (net481; net9.0) ✅

- [x] Criar `src/Core/NekoLib.Core/NekoLib.Core.csproj` (nullable enable, net481;net9.0, sem Windows)
- [x] Mover de `NekoLib.Diagnostics` (via `git mv`, namespace → `NekoLib.Core.Diagnostics`, anotações nullable):
  - `ILogger`, `ILogSink`, `ITelemetrySink`, `IDiagnosticsContext`
  - `LogEntry`, `LogLevel`, `TelemetryEvent`
  - `NullLogger`, `NullTelemetrySink`
- [x] Adicionar `IDebugUtils` + `NullDebugUtils` em `NekoLib.Core.Observability` + utilitário `Disposable.Empty` (contrato — pode ser refinado em B1)
- [x] Registrar no `NekoLib.sln` (edição manual — sem dotnet CLI)
- [x] Atualizar CLAUDE.md Module Map com `NekoLib.Core`
- [x] ⏳ Build/validação: pendente (máquina Windows do usuário)

### A2 — Criar `NekoLib.Logger` (net481; net9.0) ✅

- [x] Criar `src/Diagnostics/NekoLib.Logger/NekoLib.Logger.csproj` (nullable enable, ref Core)
- [x] Mover de `NekoLib.Diagnostics` (via `git mv`, namespace → `NekoLib.Logger` / `NekoLib.Logger.Sinks`, nullable):
  - `Logger` (implementação concreta de `ILogger`)
  - `Diagnostics` (implementação de `IDiagnosticsContext`, `Diagnostics.Null`)
  - `DebugLogSink`, `MemoryTelemetrySink`
- [x] Adicionar referência a `NekoLib.Core`
- [x] Registrar no `NekoLib.sln` (sob solution folder Diagnostics)
- [x] Atualizar CLAUDE.md Module Map
- [x] ⏳ Build/validação: pendente (Windows)
- [ ] 🔻 Consumidores a corrigir em etapas seguintes: `DiagnosticsNullTests` (`Diagnostics.Null` → `NekoLib.Logger.Diagnostics.Null`); Watchdog usa `LogEntry` (Core)

### A3 — Refatorar `NekoLib.Diagnostics` (net481; net9.0) ✅

- [x] Remover contratos movidos para Core (feito em A1)
- [x] Remover implementações movidas para Logger (feito em A2)
- [x] **Inversão do CrashHandler** (necessária p/ evitar ciclo Diagnostics ↔ Diagnostics.Windows):
  - `CrashHandlerOptions.DumpWriter` (delegate `CrashDumpWriter`) substitui a chamada direta a `MiniDumpWriter.TryWrite`
  - `CrashHandler.ReportExternalCrash(...)` estático: ponto de entrada para o hook WinForms (que vai pro Windows no A4)
  - Removido o `#if WINFORMS` (`Application.SetUnhandledExceptionMode` / `ThreadException`) — vai pro A4
- [x] `CrashHandler` cross-platform mantido (`AppDomain`, `TaskScheduler`, crash bundle/txt/tails)
- [x] Alterar `TargetFrameworks` → `net481;net9.0`, remover `UseWindowsForms`/`WINFORMS`
- [x] ⏳ Build/validação: pendente (Windows)

> **Desvios do plano literal** (decisões a confirmar):
> 1. **Referência a Logger NÃO adicionada**: o código de crash usa só `System.*`; uma referência seria não-usada (YAGNI). Adicionar Core/Logger quando o crash emitir telemetria via contratos.
> 2. **`CrashDumpLevel` permanece em Diagnostics** (não vai pro Windows como dizia A4): é parte da API de `CrashHandlerOptions`; mover criaria ciclo. Só o *impl* (`MiniDumpWriter`) é Windows.
> 3. `MiniDumpWriter` + `CrashSuppressor` ainda fisicamente em Diagnostics (compilam em net9.0 como PInvoke); movidos no A4. Pode haver warning CA1416 transitório em net9.0.

### A4 — Criar `NekoLib.Diagnostics.Windows` (net481; net9.0-windows) ✅

- [x] Criar `src/Diagnostics/NekoLib.Diagnostics.Windows/NekoLib.Diagnostics.Windows.csproj` (UseWindowsForms, ref Diagnostics)
- [x] Mover de `NekoLib.Diagnostics` (via `git mv`, namespace → `NekoLib.Diagnostics.Windows`):
  - `MiniDumpWriter` (`dbghelp.dll` PInvoke)
  - `CrashSuppressor` (`kernel32.dll` PInvoke)
- [x] Novo facade `WindowsCrash` (substitui o hook auto que vivia no CrashHandler):
  - `UseMiniDump(this CrashHandlerOptions)` → liga `MiniDumpWriter.TryWrite` no `DumpWriter`
  - `HookWinForms()` → `Application.SetUnhandledExceptionMode` + `ThreadException` → `CrashHandler.ReportExternalCrash`
- [x] `CrashDumpLevel` **permanece em Diagnostics** (parte da API de options — ver desvio #2 do A3)
- [x] Adicionar referência a `NekoLib.Diagnostics`
- [x] Registrar no `NekoLib.sln` + atualizar CLAUDE.md
- [x] ⏳ Build/validação: pendente (Windows)

> **Mudança de comportamento p/ apps WinForms**: o hook `Application.ThreadException` deixou de ser automático (era `#if WINFORMS` dentro do CrashHandler). Agora a app deve chamar `WindowsCrash.HookWinForms()` no startup (a ser fiado pela camada de hosting / `NekoLib`).

### A5 — Atualizar `NekoLib.Navigation` (net481; net9.0) ✅

- [x] Substituir referência `NekoLib.Diagnostics` → `NekoLib.Core`
- [x] Alterar `TargetFrameworks` de `net481;net9.0-windows` → `net481;net9.0`
- [x] Atualizar `DiagnosticsNavigationSink`, `PageLogEntry`, `TelemetryEventData`, `PageNavBootstrap`, `NavigationContext` — `using NekoLib.Diagnostics.Contracts` → `using NekoLib.Core.Diagnostics`
- [x] Corrigir consumidores do namespace antigo:
  - `WatchdogRuntime`, `WatchdogController`, `WatchdogOptions`, `WatchdogPipeLogSink` → `using NekoLib.Core.Diagnostics`
  - `NekoLib.Watchdog.csproj`: ref `NekoLib.Diagnostics` → `NekoLib.Core`
  - `DiagnosticsNullTests.cs`: `Diagnostics.Null` → `NekoLib.Logger.Diagnostics.Null`; csproj: adicionar refs Core + Logger
  - `WatchdogLogForwardingTests.cs` + csproj: ref `NekoLib.Diagnostics` → `NekoLib.Core`
- [x] ⏳ Build/validação: pendente (Windows)

### A6 — Projetos com mudança só no target ✅

- [x] `NekoLib.Mvvm`: `net9.0-windows` → `net9.0` (`System.Windows.Input` é cross-platform desde .NET Core)
- [x] `NekoLib.Pipes`: `net9.0-windows` → `net9.0` (sem deps Windows no código-fonte confirmado)
- `NekoLib.Watchdog`: permanece `net9.0-windows` (Win32 PInvoke: RegisterHotKey, CreateMessageOnlyWindow, etc.)
- CLAUDE.md Module Map corrigido para refletir targets reais de todos os módulos

### A7 — Nullable unificado

> Decisão: **habilitar** em todos os projetos.
> Novos projetos (Core, Logger, DebugUtils) nascem com nullable enable — custo zero.
> Projetos existentes: habilitar com warnings (não errors); usar `#nullable disable` localizado em arquivos ainda não migrados; migrar incrementalmente.

- [x] Habilitar `<Nullable>enable</Nullable>` nos projetos ainda em `disable`:
  - `NekoLib.Diagnostics`
  - `NekoLib.Devices`
  - `NekoLib.Navigation`
  - `NekoLib.Navigation.WinForms`
  - `NekoLib.Navigation.Wpf`
  - `NekoLib.Mvvm`
  - `NekoLib.Watchdog`
  - `NekoLib.Watchdog.Host`
- [x] ⏳ Confirmar que build não quebra (warnings são aceitáveis; validação Windows)
- [ ] Anotar APIs públicas críticas nos módulos mais usados (Navigation, Diagnostics, Data) — incremental

### A8 — Validação ✅ (Windows / VS26, SDK net9 + net10)

- [x] `dotnet build NekoLib.sln` — 0 erros nos dois targets (net481 + net9.0/-windows)
- [x] `dotnet test` — **358/358 verdes** em net481 e net9.0-windows
- [x] Confirmar targets finais de cada projeto (ver tabela abaixo)

> **Bug pré-existente encontrado na validação** (não causado pela Fase A):
> guard de instância única do Watchdog usava `Mutex` (thread-afim) liberado de
> uma thread do ThreadPool via RPC `stop` → `ReleaseMutex` lançava, era engolido,
> e o mutex nunca era liberado → próximos testes "already running". Corrigido em
> 2 frentes: (1) `Mutex` → `Semaphore` nomeado (libera de qualquer thread —
> correção de produto); (2) isolamento de teste via **target único** (cada teste
> copia cmd.exe pra um path próprio; a identidade do pipe/semaphore é hash do
> target, então target único ⇒ kernel object único — evita colisão entre os
> processos paralelos net481 / net9.0). `WatchdogOptions.Normalize()` mantido
> intacto (deriva o pipe do hash do target — contrato `WatchdogOptionsTests`).

| Projeto | Target esperado |
|---|---|
| NekoLib.Core | net481; net9.0 |
| NekoLib.Logger | net481; net9.0 |
| NekoLib.Diagnostics | net481; net9.0 |
| NekoLib.Diagnostics.Windows | net481; net9.0-windows |
| NekoLib.Navigation | net481; net9.0 |
| NekoLib.Navigation.WinForms | net481; net9.0-windows |
| NekoLib.Navigation.Wpf | net481; net9.0-windows |
| NekoLib.Mvvm | net481; net9.0 |
| NekoLib.Pipes | net481; net9.0 |
| NekoLib.Watchdog | net481; net9.0-windows |
| NekoLib.Watchdog.Host | net481; net9.0-windows |
| NekoLib.Data | net481; net9.0 |
| NekoLib.Devices | net481; net9.0 |

---

## Fase B — `IDebugUtils` / Observabilidade global

> Objetivo: sistema de observabilidade opt-in, custo-zero em produção, sem criar dependências cíclicas entre módulos.
> **Não utilizar em builds finais** — hooks são no-ops quando `IDebugUtils` não está registrado.

### B1 — Completar `IDebugUtils` no Core ✅

Interface criada como esqueleto na Fase A — revisada e confirmada idêntica ao design final:

- [x] `IDebugUtils` completo (`IsEnabled`, `Record`, `RegisterStateProvider`, `RegisterCommand`)
- [x] `NullDebugUtils` (singleton, `IsEnabled = false`, no-ops via `Disposable.Empty`)
- [x] `Disposable.Empty` utilitário no Core
- [x] ⏳ Build/validação: pendente (Windows)

### B2 — Criar `NekoLib.DebugUtils` (net481; net9.0) ✅

> **⏸ Pausa após esta etapa** — avaliar os pontos de hook em cada módulo antes de prosseguir para B3+.

- [x] Criar `src/DebugUtils/NekoLib.DebugUtils/NekoLib.DebugUtils.csproj` (net481;net9.0, nullable enable, ref só Core)
- [x] Implementar `DebugUtilsRuntime : IDebugUtils`:
  - Ring buffer de operações (`Queue` + lock, capacidade configurável via `DebugUtilsOptions`)
  - Dicionário de state providers por `module::key`
  - Dicionário de commands por `module::name`
  - `IsEnabled = true`; thread-safe; sem keyword `record`
  - Lado de consumo (concrete-only): `GetOperations`, `CaptureState`, `TryInvokeCommand`, `StateKeys`, `CommandKeys`
  - `DebugOperation` (entrada imutável) + `DebugUtilsOptions` (Capacity, default 1024)
- [x] Registrar no `NekoLib.sln` (solution folder `DebugUtils` sob `src`)
- [x] Atualizar CLAUDE.md Module Map
- [x] ⏳ Build/validação: pendente (Windows / smoke-test single project)

> **Decisão de design**: `IDebugUtils` é o contrato *push/register* (lado do módulo observado, sem ciclo).
> O lado *pull/consume* (ler operações, capturar estado, invocar comando) vive como API pública só na
> classe concreta `DebugUtilsRuntime` — quem hospeda o runtime consome; módulos só conhecem a interface no Core.

---

> As etapas B3–B5 serão detalhadas após avaliação dos pontos de hook na pausa de B2.

### B3 — Hooks em `NekoLib.Navigation` ✅ (trace correlacionado + lifecycle)

> **Decisão explícita de 2026-07-27:** a auditoria de lifecycle autorizou um
> descongelamento limitado do `NavigationRuntime`. `NavigationContext` continua
> sendo state bag; não foi criado host genérico, DI, message bus nem dependência
> da implementação concreta de DebugUtils. Encerrada esta correção, os componentes
> voltam a ser stability-sensitive.

- [x] Início real da requisição antes de UI dispatch/gate; `Navigating` após
  resolver o descriptor e imediatamente antes da guard.
- [x] Trace interno somente com escalares:
  `RuntimeId`/`RequestId`/`AttemptId`/`ParentAttemptId`/
  `BackgroundOperationId`, stages e tempos monotônicos.
- [x] Um terminal por request; redirects viram attempts filhos; `NoHistory` é
  resultado normal; background load fecha independentemente sem gerar um segundo
  `PageLogEntry`.
- [x] `Navigating`, `Navigated` e lifecycle recebem `NavigationArgs` com
  `LoadMode` efetivo do descriptor. `AllowAnonymous` agora é consultado.
- [x] History transacional no back (deny/redirect/falha preservam as pilhas),
  commit por identidade contra mutação reentrante e nomes lógicos do descriptor
  nos entries.
- [x] Lifecycle de páginas corrigido/testado para `ShowImmediately`,
  `LoadBeforeShow`, `LoadInBackground`, transient, singleton e
  `KeepAttached`: hide → leave → detach; o transient anterior só é descartado
  após o enter do alvo, permitindo rollback de attach/show/load/restore/enter;
  reset/shutdown limpam todas as páginas tracked sem repetir exit hooks de
  páginas já ocultas.
- [x] Bases WinForms/WPF implementam `IPageVisibility`; hosts e runtime só fazem
  attach/detach em mudança real de membership.
- [x] Loading mask e troca de página têm rollback; falha pós-attach descarta
  background do alvo e restaura host/visibilidade/`Current` anteriores.
  Toast/Dialog/Prompt/Popover desfazem setup parcial, aceitam conclusão
  síncrona, propagam falhas de cleanup e emitem lifecycle correlacionado sem
  reter payload/result.
- [x] Blocker compartilhado ganhou reference counting e rastreamento page-aware
  de views adicionadas durante modais; idle timer/observer têm ownership
  explícito, invalidação por geração, rearm após falha/negação, teardown antes
  do runtime e telemetria de configured/unavailable/interaction/elapsed/failure/
  disposed.
- [x] `DebugUtilsNavigationObserver` mantém mirrors thread-safe. Hub-only registra
  13 providers; bootstrap/context registra 16:
  runtime, in-flight, attempts, queue, current, last terminal, registry, pages,
  cache, background, overlays, idle, history, session e stats.
  `CaptureState()` não toca UI, caches ou history ao vivo.
- [x] Session expõe somente authenticated + contagens de roles/permissions e
  atualiza imediatamente via evento interno `Changed`.
- [x] Subscribers do hub, runtime e facade são isolados individualmente.
- [x] `NavigationService.Shutdown()` é uma operação única para callers
  concorrentes, impede remount durante teardown e preserva o observer até o
  último evento de teardown. Operações aceitas mantêm lease até a admissão real
  na UI; superfícies não podem surgir órfãs depois do dispose.
- [x] Rollback de `LoadInBackground` cancela somente a operação do alvo que
  falhou, sem descartar o load ainda válido da página anterior restaurada.
  Falha ao reanexar/trazer/mostrar a página anterior não publica `Current` ou
  `visible` falsos.
- [x] Blocker modal e grupos Dialog/Prompt são seguros contra callbacks
  síncronos/reentrantes; callbacks de conclusão contêm falhas de cleanup e
  entregam a falha pela `Task` aguardada.

O contrato público de outcomes foi preservado e enriquecido com correlação/
duração. A Navigation continua conhecendo somente `IDebugUtils` no Core; sem
hub, request e surface trace scopes não são alocados.

---

## Fundação process-wide descongelada (2026-07-27)

Decisão explícita: descongelar somente a instalação e o lifecycle mínimos do hub,
sem ampliar B4 e sem criar host de módulos, DI, service registry ou message bus.

- [x] `DebugUtilsProvider.Current` no Core, thread-safe e com
  `NullDebugUtils.Instance` como default não-nulo.
- [x] `DebugUtilsRuntime.EnableGlobal(...)`: uma instalação por processo;
  segunda ativação falha deterministicamente; `Dispose()` restaura o NO-OP e
  limpa operações, providers e commands. O slot também revalida o hub depois
  da publicação atômica e desfaz a instalação se ele for desabilitado durante
  a corrida.
- [x] `PageNavBootstrap.UseDebugUtils()` resolve o slot global no momento de
  `Start()`; o overload `UseDebugUtils(IDebugUtils)` foi preservado.
- [x] `NavigationService.Shutdown()` agora descarta o observer criado pelo
  bootstrap e remove os providers que capturam o contexto.
- [x] Projeto `NekoLib.DebugUtils.Tests.Unit` criado, cobrindo NO-OP, evicção,
  clear, providers, commands, isolamento de payload/provider, concorrência e
  ativação global.
- [x] Ring buffer numerado e introspectável (`GetDiagnostics()`): retenção,
  total, evicções, clears, sequência e contagem de providers/commands; registros
  duplicados são rejeitados e handles antigos não removem replacements.

---

## Remarks — deferred Inspection rollout (B4/B5) ❄

- Broad feature-module Inspection instrumentation remains frozen. Navigation is
  the only feature module that records Inspection operations. Phase D completed
  the read-only Diagnostics consumer bridge; it did not authorize broad module
  recording or new Core dependencies.
- No feature module registers a real Inspection action. Navigation remains
  read-only until an explicit async, cancellation, timeout, and UI-marshalling
  action contract is accepted.
- Existing seams for a future module-scoped rollout are
  `QueryExecutionContext` in Data, `IPipeMetrics` in Pipes, and the serialized
  `HardwareEngine.SendAsync` transaction in Devices. Watchdog and Diagnostics
  require separate review because crash notification crosses IPC and
  Diagnostics owns incident capture.
- Resume only through an explicit module-scoped unfreeze. Validate the smallest
  real producer first, preserve disabled/NO-OP behavior, cover both target
  frameworks, and restore the broad freeze after the authorized scope.
- These are architectural remarks, not active Phase C tasks. The superseded
  DebugUtils-era implementation log may move to `docs/history/` during C4.

---

## Fase C — Governança documental e organização do repositório

> Objetivo: tornar a documentação precisa, navegável e verificável; separar
> claramente testes automatizados, cenários manuais, código-fonte de ferramentas,
> executáveis locais e artefatos gerados; eliminar fontes de verdade concorrentes.
>
> Esta fase é estrutural. Ela não autoriza mudanças de comportamento nos módulos,
> no grafo de dependências ou no congelamento da instrumentação de Inspection.
>
> **Preparation status (2026-08-01): ready to start.** The evidence, corrected
> premises, and recommended order are recorded in the
> [Phase C readiness review](docs/audit/repository-hygiene-phase-c-readiness-2026-08-01.md).
> No C1-C9 checklist item is complete yet. Start with C1 + C3, then reconcile
> current documentation and the live roadmap through C2 + C4.

### C1 — Definir autoridade e ciclo de vida da documentação

- [ ] Criar `docs/README.md` como índice e classificar cada documento em dois
  eixos:
  - **kind** — `reference`, `guide`, `roadmap/status` ou `audit`;
  - **lifecycle** — `current`, `frozen` ou `historical`.
  Um guide também pode ser current; `frozen` é contexto vivo, não histórico.
- [ ] Definir metadados mínimos em formato estável e verificável pelo C9:
  kind, lifecycle, assunto/dono, commit ou data de referência e, quando
  aplicável, link para o estado atual.
- [ ] Registrar autoridade por tipo de fato, não como uma precedência linear
  universal:
  - targets, referências e propriedades de build vêm dos `*.csproj` e dos
    arquivos `Directory.Build.*`;
  - participação na solution vem de `NekoLib.sln`;
  - comportamento e superfície pública vêm do código; testes executáveis são
    evidência verificável desse comportamento;
  - trabalho aberto, decisões em vigor e congelamentos vêm do roadmap/status;
  - auditorias descrevem somente o estado no commit auditado.
- [ ] Declarar que `AGENTS.md` e arquivos locais de orientação de assistentes
  não substituem documentação pública ou técnica.
- [ ] Cada fato mutável deve ter um único dono. Outros documentos podem resumir
  ou apontar para esse fato, mas não manter uma segunda lista independente.
- [ ] Não manter contagens correntes de testes, warnings ou projetos em vários
  documentos. Quando o número for histórico, registrar data, comando e commit.

### C2 — Reconciliar a documentação atual contra o repositório

- [ ] Revisar cada afirmação factual, comando e link do `README.md` contra os
  `*.csproj`, `NekoLib.sln`, empacotamento e APIs públicas.
- [ ] Corrigir afirmações amplas que escondem dependências ou TFMs específicos;
  o mapa de módulos deve ser a descrição curta autoritativa do grafo atual.
- [ ] Reconciliar fatos operacionais em orientações versionadas como
  `AGENTS.md`; quando possível, substituir cópias de fatos públicos por links.
  In particular, reconcile `tests/NekoLib.Data.Tests/Shared/`: its fixtures are
  tracked even though the path also matches `.gitignore`, while current
  versioned tests do not reference those fixtures by name.
- [ ] Preservar
  `src/Navigation/NekoLib.Navigation/README.md` como referência técnica
  próxima ao módulo e indexá-la em `docs/README.md`.
- [ ] Definir, para cada módulo sem referência técnica própria, o mínimo
  necessário: propósito, targets, dependências, superfície pública principal,
  limitações conhecidas e comandos de validação. Não criar documentação extensa
  por simetria quando uma página curta resolver.
- [ ] Separar instrução durável de registros de execução. Resultados como
  “N/N testes” e “X warnings” pertencem a um snapshot de validação, não à
  descrição permanente do produto.
- [ ] Verificar todos os links e remover referências a arquivos ausentes em um
  clone limpo.

### C3 — Transformar auditorias em snapshots históricos

- [ ] Criar um índice em `docs/audit/` com módulo, data, commit auditado,
  escopo, última reconciliação e link para o estado atual.
- [ ] Remover dos títulos e cabeçalhos termos ambíguos como `Latest`,
  `Current Status` e `Status (current)`; substituir por
  `Historical snapshot at <commit>`.
- [ ] Não reescrever o corpo histórico para simular que a descoberta original
  já conhecia correções posteriores. Manter a reconciliação no cabeçalho/índice
  ou em uma seção curta claramente posterior ao snapshot original.
- [ ] Mover `src/Data/NekoLib.Data/DataAudit.md` para `docs/audit/`, preservando
  histórico e links via `git mv`.
- [ ] Reconciliar as divergências já identificadas:
  - Devices ainda lista como pendentes itens encerrados por `d352fa8`;
  - Pipes declara `_handlers` corrigido e aberto no mesmo arquivo, além de
    registrar o TFM antigo `net9.0-windows`;
  - Navigation mantém NEW-13 aberto embora `Register<T>` já encaminhe para
    `RegisterType`;
  - Watchdog chama de `uncommitted` trabalho presente em `1727a1c`;
  - Data ainda carrega como abertas correções já implementadas/testadas.
- [ ] Itens realmente abertos devem existir em um único roadmap/status; a
  auditoria apenas aponta para ele.

### C4 — Reduzir o `TODO.md` ao trabalho vivo

> Regra: `frozen` não significa concluído ou histórico. Um bloco congelado é
> contexto vivo para a futura retomada e permanece íntegro no `TODO.md` enquanto
> o congelamento estiver em vigor.
>
> Um descongelamento temporário explicitamente autorizado suspende o bloqueio
> somente para o objetivo e o escopo declarados. Dentro desse limite, nenhum
> agente deve tratar o estado normalmente congelado como impedimento, hesitar
> por esse motivo ou pedir uma segunda autorização equivalente. Continuam
> valendo as regras normais de segurança, arquitetura, testes e preservação de
> mudanças alheias.
>
> Concluído o escopo autorizado, o estado congelado volta a valer
> automaticamente. Ampliar o escopo, prolongar o descongelamento ou alterar
> outra área congelada exige uma nova decisão explícita.

- [ ] Mover somente os relatos efetivamente concluídos das Fases A/B para um
  documento sob `docs/history/`, preservando decisões arquiteturais, commits e
  validações relevantes. Não mover junto seções que contenham trabalho
  congelado.
- [ ] Remover instruções específicas de um ambiente antigo, como SDK ausente,
  execução remota bloqueada e pausas ligadas ao fluxo de um assistente.
- [ ] Manter no `TODO.md` somente trabalho pendente/congelado, decisões em vigor,
  dependências entre etapas e critérios de conclusão.
- [ ] Todo bloco congelado deve preservar: motivação do congelamento, estado
  implementado, lacunas conhecidas, armadilhas já descobertas, seams existentes,
  ordem recomendada de retomada e condições para descongelar.
- [ ] Ao concluir uma fase, arquivar apenas o log detalhado que não seja
  necessário para retomar trabalho congelado e deixar um resumo com link; não
  acumular indefinidamente work logs concluídos no roadmap.
- [ ] Um bloco congelado só pode ser reduzido ou movido depois de uma decisão
  explícita de descongelamento e da transferência integral do contexto relevante
  para o novo plano ativo. O registro original do período congelado vai então
  para o histórico.
- [ ] When archiving the completed Phase A/B work log, keep a link to the
  concise B4/B5 remarks and preserve their current freeze, remaining gaps,
  seams, resume order, and validation conditions.

### C5 — Formalizar a taxonomia de testes

- [ ] Definir `tests/` como raiz de **verificações automatizadas**, não como
  sinônimo absoluto de unit tests.
- [ ] Classificar verificações em eixos independentes: execução
  (automatizada/manual), escopo (unit/integration/functional/package probe),
  pré-requisitos e entrypoint. A classificação semântica não obriga por si só
  uma mudança de pasta ou assembly.
- [ ] Manter unit tests em `tests/NekoLib.{Module}.Tests/Unit/`.
- [ ] Documentar que `tests/NekoLib.PackageConsumers/` contém probes de pacote,
  fica fora de `NekoLib.sln` e é executado pelo fluxo de packaging.
- [ ] Classificar como integration/functional qualquer teste automatizado que
  use processos, IPC, banco real ou recursos do SO. Separar fisicamente quando
  pré-requisitos, comandos, isolamento ou custo de execução justificarem uma
  suíte distinta.
- [ ] Documentar os comandos canônicos para solution, projeto, TFM, teste
  individual e package-consumer probes.

### C6 — Dar um contrato operacional a `runtime_tests/`

> Decisão-alvo: um cenário usado como evidência compartilhada deve ser
> versionado. Experimentos exclusivamente locais pertencem a `.local/` e não
> podem ser citados como cobertura do repositório.

- [ ] Não tratar os cenários atuais, reconhecidamente outdated, como evidência
  de comportamento. Inventariar cada um e classificá-lo como reconstruir para
  evidência compartilhada, manter como experimento local, arquivar ou remover.
- [ ] Decidir a classe de cada cenário antes de alterar `.gitignore` ou
  documentação. A decisão pode manter cenários compartilhados e locais em
  paralelo, mas não pode citar os locais como cobertura.
- [ ] Para cenários compartilhados, versionar `runtime_tests/README.md`, um
  template mínimo, a fonte e as instruções necessárias; ignorar somente seus
  outputs e dados temporários.
- [ ] Cada cenário compartilhado ativo deve registrar: propósito, módulo, SO/TFM,
  pré-requisitos, build, executável a iniciar, passos manuais, resultado
  esperado, cleanup, data e commit da última verificação.
- [ ] Organizar novos cenários primeiro pelo módulo/capacidade validada; manter
  UI e TFM como metadados ou subnível, não como única identidade do cenário.
- [ ] Cenários permanecem fora de `NekoLib.sln` por padrão e nunca são
  executados via `dotnet test`; a operação é build + lançamento explícito do
  executável.
- [ ] Para experimentos locais, usar `.local/runtime-tests/`, manter a área
  ignorada e remover referências a ela da documentação compartilhada.
- [ ] Ajustar `.gitignore` depois da classificação: ignorar `bin/`, `obj/`,
  logs, bancos temporários e outros outputs, sem esconder fonte ou instruções de
  cenários compartilhados ativos.

### C7 — Separar ferramentas, automação e artefatos

- [ ] Adotar responsabilidades explícitas:
  - `src/Tools/` — código-fonte versionado de executáveis próprios;
  - `tools/` — payloads executáveis locais/restaurados, sem código-fonte;
  - `eng/` — scripts versionados de build, validação e manutenção do repo;
  - `artifacts/` — saídas geradas e descartáveis;
  - `.local/` — experimentos, configuração e scratch exclusivos da máquina.
- [ ] Nenhum teste pode depender de um `.exe` opaco copiado manualmente.
  Executáveis mantidos pelo repositório devem ter build ou restore reproduzível,
  versão e hash; binários do sistema operacional devem ser pré-requisitos
  declarados e isolados, não payloads vendorizados.
- [ ] Tornar `src/Tools/BundlerTool/` a fonte canônica do BundlerTool e definir
  um build reproduzível cuja saída vá para `artifacts/`. Uma cópia ignorada em
  `tools/BundlerTool.exe` é cache local, nunca autoridade.
- [ ] Não criar uma pasta genérica `internal_tools/` inteira e invisível ao Git.
  Versionar a fonte útil; ignorar somente outputs, caches, credenciais e scratch.

O catalogador de código voltado a LLMs **não faz parte do critério de conclusão
da Fase C**. Se for autorizado separadamente, deve:

- reutilizar/extrair o scanner e o parsing Roslyn já presentes no
  `BundlerTool`, evitando um segundo leitor independente da árvore;
- preferir uma CLI determinística em `src/Tools/` ou orquestração em `eng/`;
- gerar o catálogo sob `artifacts/`, com commit, hash da fonte, símbolo,
  assinatura, arquivo/linha e documentação existente;
- marcar intenção gerada por LLM como inferência, com evidência/confiança;
- nunca inserir comentários inferidos automaticamente no código-fonte.

### C8 — Eliminar duplicação física e divergência lógica

- [ ] Remover, após verificar referências e packaging, as cópias idênticas
  `src/Navigation/NekoLib.Navigation/LICENSE.txt` e
  `src/Navigation/NekoLib.Navigation/.gitattributes` se continuarem redundantes
  em relação aos arquivos raiz.
- [ ] Manter duplicações necessárias por assembly, como os `AssemblyInfo.cs`
  de Watchdog, mesmo quando o conteúdo coincidir.
- [ ] Substituir repetição de explicações por links para a fonte autoritativa;
  resumos podem existir, mas não carregar uma segunda lista independente de
  estado ou itens abertos.
- [ ] Executar uma busca por arquivos idênticos e por fatos divergentes antes de
  concluir a fase. Examinar primeiro os arquivos retornados por `git ls-files`,
  classificar ignorados separadamente e distinguir boilerplate legítimo de
  cópia abandonada.

### C9 — Automatizar a verificação documental

- [ ] Criar uma verificação em `eng/` para links Markdown, caminhos ausentes,
  referências a arquivos ignorados e metadados obrigatórios de classificação e
  auditoria.
- [ ] Validar automaticamente, sempre que viável, o mapa de projetos contra
  `dotnet sln NekoLib.sln list` e os targets/referências contra os `*.csproj`.
- [ ] A verificação deve falhar quando um documento “current” citar caminho
  inexistente; documentos históricos podem citar caminhos removidos somente
  quando estiverem marcados explicitamente como históricos.
- [ ] Comparar warnings por identidade normalizada, não apenas por contagem,
  preservando o baseline sem introduzir identidades novas.
- [ ] Quando arquivos ou documentação de packaging forem afetados, executar o
  fluxo canônico com uma versão local descartável e inédita; nunca sobrescrever
  uma versão existente no feed.
- [ ] Executar ao final:

```powershell
.\eng\verify-docs.ps1
dotnet sln NekoLib.sln list
dotnet build NekoLib.sln -t:Rebuild
dotnet test NekoLib.sln
git diff --check
```

- [ ] Registrar a validação final em um snapshot datado com o commit; não copiar
  os números resultantes para múltiplos documentos duráveis.

### Critério de conclusão da Fase C

- [ ] Um clone limpo consegue descobrir, pelo `README.md` e `docs/README.md`,
  onde está a referência atual, como validar o projeto e quais documentos são
  apenas históricos.
- [ ] Nenhum item aberto possui duas listas autoritativas.
- [ ] Nenhum cenário ou ferramenta citado pela documentação depende de arquivos
  locais invisíveis sem procedimento reproduzível.
- [ ] Nenhum documento current depende de um caminho ignorado ou ausente em
  clone limpo sem declará-lo explicitamente como pré-requisito local.
- [ ] Build e testes permanecem verdes nos dois TFMs, sem nova identidade de
  warning.

---

## Phase D — Logging, Telemetry, Diagnostics, and Inspection boundaries ✅

> Promoted on 2026-08-01 from the accepted decisions in the
> [Diagnostics sector review](docs/audit/diagnostics-boundaries-review-2026-07-30.md).
> This section is the authoritative implementation roadmap for those decisions;
> the review preserves evidence and rationale only.
>
> Implementation was authorized on 2026-08-01. The limited unfreeze covered the
> D7 Diagnostics consumer bridge and Navigation timing producer; the broad B4
> Inspection instrumentation freeze remains active.

### Target capability boundaries

| Target project | Responsibility |
|---|---|
| `NekoLib.Core` | Small producer and consumer contracts plus null implementations; no concrete pipeline ownership |
| `NekoLib.Logging` | Severity-based logging pipeline, sink dispatch, recent entries, flush, and reusable bounded disk persistence |
| `NekoLib.Telemetry` | Correlated operation timings, checkpoints, outcomes, snapshots, and optional persistence |
| `NekoLib.Inspection` | Opt-in runtime operations, state snapshots, and explicitly registered intrusive actions |
| `NekoLib.Diagnostics` | Incident/crash capture and evidence-bundle orchestration that consumes the other capabilities through abstractions |
| `NekoLib.Diagnostics.Windows` | Windows-only crash hooks, WER behavior, and minidumps |

Feature modules consume only the smallest contracts they need. Concrete
packages are selected by the application composition root. Diagnostics must not
reference concrete Logging, Telemetry, or Inspection implementations. The
`.Windows` project remains the intentional platform-specific exception.

### D1 — Correct and separate the Core contracts

- [x] Make telemetry data independent from `LogEntry`; remove the invalid
  `TelemetryEvent : LogEntry` inheritance and add direct regression coverage.
- [x] Align `LogEntry`, `ILogger`, category support, constructors, and
  `ToString()` semantics with the minimum accepted logging model.
- [x] Retire the logger-plus-telemetry `IDiagnosticsContext` container. Migrate
  consumers to independent writer/read-side contracts rather than replacing it
  with another broadly named context.
- [x] Keep Core limited to stable contracts and null implementations. Do not
  move queues, file I/O, crash policy, Inspection storage, or composition into
  Core.
- [x] Preserve `net481` and `net9.0` compatibility and use ordinary classes,
  not records, for shared public data types.

### D2 — Build the Logging pipeline

- [x] Rename `NekoLib.Logger` to the accepted `NekoLib.Logging` capability name
  after deciding the public type and PackageId compatibility strategy.
- [x] Preserve the small feature-facing `ILogger` writer contract and define a
  separate operational surface for composition/Diagnostics to flush and read a
  bounded recent-entry snapshot.
- [x] Specify and implement ordered sink dispatch, bounded buffering or an
  explicitly synchronous policy, sink-failure isolation, shutdown, and a
  bounded flush operation.
- [x] Add a reusable rolling file sink. Define file location, encoding, maximum
  size, retained-file count, concurrent-writer behavior, failure behavior, and
  the persistence guarantee for `Error`/`Fatal` before implementation.
- [x] Keep debugger output as an optional sink; writing an ordinary `Info` entry
  must not require Diagnostics or Inspection.
- [x] Add direct dual-target tests for severity filtering, sink fan-out,
  ordering, failure isolation, recent snapshots, flush, rotation, retention,
  and write failures.

### D3 — Build the Telemetry pipeline

- [x] Create an independent `NekoLib.Telemetry` implementation over small Core
  contracts; do not route telemetry through the logging pipeline or inherit log
  models.
- [x] Define the minimum operation model: module, operation name, operation ID,
  optional parent ID, outcome, dimensions, one UTC chronology timestamp, and
  monotonic elapsed values for checkpoints and completion.
- [x] Treat operation timings as raw telemetry. Defer percentiles, counters, and
  other metric aggregation until a concrete consumer requires them.
- [x] Provide bounded recent-operation snapshots for Diagnostics. Decide
  separately whether v1 also persists raw telemetry to disk.
- [x] Add direct dual-target tests for correlation, checkpoints, monotonic
  durations, terminal outcomes, bounded retention, and subscriber/sink failure
  isolation.

#### Initial Navigation timing semantics

- [x] Use Navigation as the first producer without changing its canonical
  lifecycle order, redirect correlation, UI dispatch, navigation gate, or
  existing terminal semantics.
- [x] Capture three meaningful milestones for the initial page-transition use
  case: page switch started, authentication completed, and page ready.
- [x] Derive `page_switch.total_ms`,
  `page_switch.time_to_authenticated_ms`, and
  `page_switch.post_auth_to_ready_ms` from those milestones.
- [x] Do not label the latter two values as pure authentication or page-load
  duration unless their exact start/end boundaries are instrumented.
  `NavigationStarted` currently includes UI-dispatch and gate-wait time.
- [x] Keep authentication and catalog/API behavior outside Navigation. The
  application supplies the authentication checkpoint and correlation; POST/GET
  start/end timings are not required for the initial use case.
- [x] Define `page ready` precisely. Do not claim first paint or OS-level render
  completion if the adapter only proves completion of the synchronous
  Navigation lifecycle.

### D4 — Rename DebugUtils to Inspection without broadening it

- [x] Rename the package/project to `NekoLib.Inspection` and establish the
  public-type migration map before changing PackageIds.
- [x] Preserve the current opt-in, in-process, bounded, singleton-capable model.
  Inspection remains more intrusive than logging and must not become a second
  logger, a control bus, a DI container, or an exception-policy owner.
- [x] Separate the module-facing record/register capability from the read-only
  snapshot capability. Diagnostics may consume snapshots but must never invoke
  registered actions.
- [x] Preserve deterministic disable/dispose behavior, NO-OP defaults, bounded
  payload construction, provider isolation, and existing dual-target tests
  through the rename.
- [x] Decide whether `RegisterCommand` becomes `RegisterAction`; keep any action
  surface explicitly operational and constrained rather than general-purpose.

### D5 — Refocus Diagnostics on incident evidence

- [x] Keep `NekoLib.Diagnostics` focused on exception/incident capture and
  evidence-bundle orchestration, not ordinary log emission or telemetry
  production.
- [x] Define the incident sequence: record the fatal event, request a bounded
  logging flush, capture recent logs, optionally capture recent telemetry and a
  read-only Inspection snapshot, collect platform artifacts, write the bundle,
  then notify the configured supervisor.
- [x] Consume all optional sources through abstractions supplied by the
  composition root. Do not add concrete project references from Diagnostics to
  Logging, Telemetry, or Inspection.
- [x] Add only the Core contract dependency required by the target Diagnostics
  composition; do not use that dependency to pull unrelated contracts or policy
  into the crash package.
- [x] Define bounded collection, timeouts, redaction, contributor-failure
  isolation, and partial-bundle behavior so diagnostics cannot hang or replace
  the original failure.
- [x] Keep `NekoLib.Diagnostics.Windows` as the Windows-only adapter. The
  platform-neutral artifact contract, Watchdog notification policy, WinForms
  hook lifecycle, and filename cleanup remain pending review decisions rather
  than accepted Phase D work.

### D6 — Migration, verification, and documentation

- [x] Decide clean breaking rename versus compatibility packages/types before
  changing public namespaces, assembly names, or PackageIds.
- [x] Migrate Navigation and Watchdog composition away from
  `IDiagnosticsContext` while preserving their Core-only product dependencies.
- [x] Split or rename test projects to mirror the accepted package boundaries;
  add direct Windows adapter tests where automation is practical.
- [x] Update solution membership, package-consumer probes, packaging metadata,
  README module maps, examples, and current technical documentation only after
  the implementation becomes authoritative.
- [x] Validate both target frameworks on Windows, run package-consumer probes
  with a new disposable version, compare warning identities, and run the full
  solution tests before completing the phase.

### D7 — Module rollout and consumer bridge ✅ **limited scope complete**

- [x] After an explicit limited unfreeze, connect the read-only Inspection
  snapshot and bounded Telemetry/Logging snapshots to Diagnostics incident
  bundles.
- [x] After an explicit limited unfreeze, implement the initial Navigation
  operation timing without altering the frozen lifecycle-sensitive components.
- [x] Evaluate Data and Devices separately before adding telemetry contracts or
  new Core references. If accepted, emit their own correlated operations (for
  example query/transaction and command/round-trip) rather than Navigation-
  specific metrics.
- [x] Keep the existing B4 Inspection instrumentation freeze and its preserved
  context authoritative for broad module record/state/action hooks; telemetry
  rollout does not silently authorize those hooks.

Phase D used a clean breaking rename because these packages are coordinated
locally and compatibility shims would preserve the ambiguity the phase removes.
Logging v1 is synchronous and ordered; Telemetry v1 keeps bounded completed
operations in memory and does not persist them. `RegisterCommand` became the
explicitly operational `RegisterAction` surface.

The Data and Devices evaluation did not authorize instrumentation. Data already
has `QueryExecutionContext` events that can later delimit query/transaction
operations without importing Navigation semantics. Devices has a natural
`HardwareEngine.SendAsync` command/round-trip boundary, but its transport work
was concurrently dirty during Phase D. Both modules retain their zero-project-
reference topology; a later accepted rollout can use a caller-supplied parent
operation ID through Core telemetry contracts. The broader B4 Inspection freeze
remains active.

### Phase D completion criteria

- [x] Ordinary logging, operation telemetry, runtime Inspection, and incident
  Diagnostics have distinct public names and non-overlapping ownership.
- [x] An application can write `Info` through Logging and persist it to bounded
  disk storage without enabling Diagnostics or Inspection.
- [x] Telemetry can represent the accepted Navigation timing scenario and later
  correlate independent Data or Devices operations without module-to-module
  dependencies.
- [x] Diagnostics can produce a bounded partial bundle from the sources supplied
  by composition without referencing their concrete implementations.
- [x] Windows-specific behavior remains isolated, and both supported TFMs plus
  package-consumer probes pass without new warning identities.

---

## Active architecture reviews

- [ ] Complete the remaining Diagnostics sector boundary and naming decisions.
  - Review artifact:
    [`docs/audit/diagnostics-boundaries-review-2026-07-30.md`](docs/audit/diagnostics-boundaries-review-2026-07-30.md)
  - Baseline: `master` / `1727a1cac3f66666b2df02bc618ad6ab45807a49`.
  - Promoted to Phase D: DGN-01, CORE-01, BND-01, LOG-01, CORE-02,
    TEST-01, and the frozen target direction of DBG-01.
  - Remaining review-only decisions: CRASH-01, CRASH-02, and WIN-01.
