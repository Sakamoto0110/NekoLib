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
- [ ] ⏳ Build/validação: pendente (máquina Windows do usuário)

### A2 — Criar `NekoLib.Logger` (net481; net9.0) ✅

- [x] Criar `src/Diagnostics/NekoLib.Logger/NekoLib.Logger.csproj` (nullable enable, ref Core)
- [x] Mover de `NekoLib.Diagnostics` (via `git mv`, namespace → `NekoLib.Logger` / `NekoLib.Logger.Sinks`, nullable):
  - `Logger` (implementação concreta de `ILogger`)
  - `Diagnostics` (implementação de `IDiagnosticsContext`, `Diagnostics.Null`)
  - `DebugLogSink`, `MemoryTelemetrySink`
- [x] Adicionar referência a `NekoLib.Core`
- [x] Registrar no `NekoLib.sln` (sob solution folder Diagnostics)
- [x] Atualizar CLAUDE.md Module Map
- [ ] ⏳ Build/validação: pendente (Windows)
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
- [ ] ⏳ Build/validação: pendente (Windows)

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
- [ ] ⏳ Build/validação: pendente (Windows)

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
- [ ] ⏳ Build/validação: pendente (Windows)

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
- [ ] ⏳ Confirmar que build não quebra (warnings são aceitáveis; validação Windows)
- [ ] Anotar APIs públicas críticas nos módulos mais usados (Navigation, Diagnostics, Data) — incremental

### A8 — Validação ⏳ (pendente máquina Windows)

- [ ] `dotnet build NekoLib.sln` — 0 erros (net9.0 pode gerar nullable warnings; net481 requer Windows)
- [ ] `dotnet test` — todos os testes passando
- [ ] Confirmar targets finais de cada projeto (ver tabela abaixo)

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

### B1 — Completar `IDebugUtils` no Core

Interface já criada como esqueleto na Fase A. Nesta etapa:

- [ ] Definir `IDebugUtils` completo:
  ```csharp
  public interface IDebugUtils
  {
      bool IsEnabled { get; }
      void Record(string module, string operation, Func<object>? payload = null);
      IDisposable RegisterStateProvider(string module, string key, Func<object> snapshot);
      IDisposable RegisterCommand(string module, string name, Func<object?, object?> command);
  }
  ```
- [ ] Implementar `NullDebugUtils` (singleton, `IsEnabled = false`, todos os métodos são no-ops)
- [ ] Definir `Disposable.Empty` utilitário no Core (reusável pelo `NullDebugUtils`)

### B2 — Criar `NekoLib.DebugUtils` (net481; net9.0)

> **⏸ Pausa após esta etapa** — avaliar os pontos de hook em cada módulo antes de prosseguir para B3+.

- [ ] Criar `src/DebugUtils/NekoLib.DebugUtils/NekoLib.DebugUtils.csproj`
- [ ] Referencia apenas `NekoLib.Core`
- [ ] Implementar `DebugUtilsRuntime : IDebugUtils`:
  - Ring buffer de operações com capacidade configurável
  - Dicionário de state providers por módulo/chave
  - Dicionário de commands por módulo/nome
  - `IsEnabled = true`
- [ ] Registrar no `NekoLib.sln`
- [ ] Atualizar README / CLAUDE.md com novo projeto

---

> As etapas B3–B5 serão detalhadas após avaliação dos pontos de hook na pausa de B2.

### B3 — Hooks em `NekoLib.Navigation` *(pendente avaliação)*

### B4 — Hooks nos demais módulos *(pendente avaliação)*

### B5 — Validação *(pendente avaliação)*
