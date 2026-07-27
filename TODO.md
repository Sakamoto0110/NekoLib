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

### B3 — Hooks em `NekoLib.Navigation` ✅ (piloto + telemetria estendida)

> **Decisão-chave**: `NavigationContext` é FROZEN (CLAUDE.md + README §5). Em vez de
> instrumentar o ciclo de vida por dentro, o hook é um **subscriber puro** no
> `NavigationEventHub` público — zero alteração no core congelado.

- [x] `DebugUtilsNavigationObserver` (em `Navigation/Diagnostics/`): assina
  `NavigationLogged`/`GuardDenied` do hub e encaminha pra `IDebugUtils.Record`
  (`Navigation/Navigated`, `Navigation/NavigationFailed`, `Navigation/GuardDenied`).
  Mantém o último evento como snapshot pull-based via `RegisterStateProvider`
  (`Navigation::current`). `IDisposable` pra desanexar; no-op quando o sink está
  desabilitado (`NullDebugUtils`) — retorna `Disposable.Empty`, sem assinar nada.
- [x] `PageNavBootstrap.UseDebugUtils(IDebugUtils)` — espelha `UseDiagnostics`;
  anexa o observer no slot "Diagnostics bridge (optional)" após criar o contexto.
- [x] Teste `DebugUtilsNavigationObserverTests` (6 casos) usando o `DebugUtilsRuntime`
  real (end-to-end): operações Navigated/Failed/GuardDenied, state pull, dispose
  desanexa, sink desabilitado é no-op. Refs Core + DebugUtils adicionadas ao csproj.
- [x] Build/validação: 358/358 verdes na época (net481 + net9.0)

> **Mental model**: a Navigation só conhece `IDebugUtils` (contrato no Core). Quem
> hospeda o `DebugUtilsRuntime` consome via `GetOperations`/`CaptureState`. Nenhum
> acoplamento ao runtime concreto; nenhuma dependência cíclica.

#### Hooks adicionais de telemetria (2026-07-26)

O observer passou a ter **dois níveis de fidelidade**, porque o hub público só tem
2 eventos e ambos falam depois que a navegação resolveu:

- [x] `Attach(NavigationEventHub, IDebugUtils)` — só o hub: `Navigated` /
  `NavigationFailed` / `GuardDenied` + estado `Navigation::current` e
  `Navigation::stats`. **Não** toca a facade estática, então é o caminho dos
  testes paralelos.
- [x] `Attach(NavigationContext, IDebugUtils)` — o caminho do bootstrap. Assina
  também os eventos estáticos do `NavigationService`, único seam público que
  carrega:
  - `NavigationStarted` — a **intenção**, antes do resultado. Se a navegação
    travar (guard que não retorna, `OnNavigatedToAsync` em deadlock), o hub fica
    calado e o ring buffer não mostra nada; `NavigationStarted` sem desfecho é a
    impressão digital desse freeze.
  - `FirstPageAttached` / `NoPageAttached` / `NoPageVisible` — transições de
    attach/visibilidade, sintoma clássico de leak de página ou shell em branco.
  Registra também `Navigation::history` (pilhas back/forward) e
  `Navigation::session` (auth/roles/permissions como os guards veem).
- [x] `Navigation::stats` — contadores agregados
  (started/navigated/failed/guardDenied/timeouts/backNavigations/blankShellEvents
  + lastStarted). **Sobrevivem à rotação do ring buffer**: quando o buffer dá a
  volta, os totais são a única evidência que resta. `started > navigated + failed`
  ⇒ navegação entrou e nunca resolveu.
- [x] 13 testes (6 originais + 7 novos). Os 3 que montam a facade estática vivem
  em `DebugUtilsNavigationObserverFacadeTests` com
  `[Collection("NavigationServiceFacade")]` e `Shutdown()` no `finally` — qualquer
  teste futuro que monte o `NavigationService` entra nessa collection.
- [x] Validação: **478/478 verdes** (net481 + net9.0), 0 erros, 0 warnings novos.

> `NavigationHistory` é afim à UI thread e não tem sincronização interna, então o
> snapshot de `Navigation::history` é best-effort: capturar de outra thread durante
> uma navegação pode lançar. O `DebugUtilsRuntime` isola por provider e devolve um
> placeholder em vez de falhar a captura toda.

---

## ❄ Congelamento temporário da observabilidade (2026-07-26)

`NekoLib.Core.Observability` (`IDebugUtils`, `NullDebugUtils`), `NekoLib.DebugUtils`
e o `DebugUtilsNavigationObserver` estão **congelados**. Não estender sem decisão
explícita.

O que fica **declaradamente incompleto** — dívida conhecida, não esquecimento:

1. **B4 não foi feito.** Só a Navigation emite. `Data`, `Pipes`, `Watchdog`,
   `Devices` e `Diagnostics` não conhecem `IDebugUtils`. Cuidado com a pegadinha:
   o `IntegrationDemo_481` mostra operações `Data/*` e `Pipes/*` no ring buffer,
   mas é **o app chamando `Record` à mão** (`PodRepository`, `PipeDemoService`) —
   a lib não emite nada. Troque de app e a instrumentação vai embora.
2. **Canal de comando morto.** `RegisterCommand` / `TryInvokeCommand` não têm um
   único registro, invocação ou teste em todo o repo. Um terço da interface nunca
   foi exercitado.
3. **Sem superfície de consumo reutilizável.** Nenhum viewer, nenhum bridge para
   `ILogSink`/arquivo, nada no crash bundle; o `NekoLib` (hosting) não conhece o
   módulo. Cada app monta na mão (no demo é uma `ListBox` na `AdminPage`).
4. **Sem projeto de testes próprio do `NekoLib.DebugUtils`.** A evicção do ring
   buffer é coberta de raspão via observer; `ClearOperations`, `CommandKeys`, o
   canal de comando e concorrência não são testados.
5. `NoPageAttached` / `NoPageVisible` estão fiados mas sem teste — disparar de
   forma determinística exige host real, não os fakes.

Ao descongelar, a ordem recomendada: **bridge de consumo** (dump do ring buffer +
`CaptureState()` dentro do crash bundle do `CrashHandler` — é o que transforma o
módulo de "buffer que ninguém lê" em ferramenta de post-mortem) → **um caso real
de comando** (valida o terço morto antes de replicar em 5 módulos) → **B4** por
módulo, começando por Data (eventos do `QueryExecutionContext` já são o seam) e
Pipes (`IPipeMetrics` já é o ponto de extensão).

### B4 — Hooks nos demais módulos ⏸ **congelado** (ver acima)

### B5 — Validação ⏸ **congelado** (ver acima)
