# NekoLib — Plano de Ação

---

## Notas gerais de implementação

- **Keyword `record`**: não usar como keyword em tipos compartilhados entre targets. `record` (C# 9) requer `System.Runtime.CompilerServices.IsExternalInit` que não existe no net481 sem shim explícito. Usar classes normais para tipos de dados em projetos multi-target.
- **README**: atualizar `README.md` / `CLAUDE.md` ao concluir cada etapa, refletindo a nova estrutura de projetos e o grafo de dependências.
- **`NullDebugUtils`**: segue o padrão Null Object, consistente com `NullLogger` e `NullTelemetrySink` já existentes. A implementação satisfaz a interface sem fazer nada — o caller nunca precisa checar `if (debug != null)`.
- **Pausa entre etapas**: ao final de cada etapa (após commit, push e atualização do TODO/CLAUDE.md), fazer uma pausa para o usuário verificar contexto/tokens antes de continuar.

---

## Fase A — Reestruturação de base e desbloqueio multi-target

> Objetivo: separar contratos de implementação, destravar `net9.0` nos módulos que não precisam de Windows, e criar o projeto `NekoLib.Core` como base da pirâmide de dependências.

### A1 — Criar `NekoLib.Core` (net481; net9.0)

- [ ] Criar `src/Core/NekoLib.Core/NekoLib.Core.csproj`
- [ ] Mover de `NekoLib.Diagnostics`:
  - `ILogger`, `ILogSink`, `ITelemetrySink`, `IDiagnosticsContext`
  - `LogEntry`, `LogLevel`, `TelemetryEvent`
  - `NullLogger`, `NullTelemetrySink`
- [ ] Adicionar esqueletos de `IDebugUtils` e `NullDebugUtils` (contrato apenas — implementação na Fase B)
- [ ] Registrar no `NekoLib.sln`
- [ ] Atualizar README / CLAUDE.md com novo projeto

### A2 — Criar `NekoLib.Logger` (net481; net9.0)

- [ ] Criar `src/Diagnostics/NekoLib.Logger/NekoLib.Logger.csproj`
- [ ] Mover de `NekoLib.Diagnostics`:
  - `Logger` (implementação concreta de `ILogger`)
  - `Diagnostics` (implementação de `IDiagnosticsContext`)
  - `DebugLogSink`, `MemoryTelemetrySink`
- [ ] Adicionar referência a `NekoLib.Core`
- [ ] Registrar no `NekoLib.sln`
- [ ] Atualizar README / CLAUDE.md com novo projeto

### A3 — Refatorar `NekoLib.Diagnostics` (net481; net9.0)

- [ ] Remover contratos movidos para Core
- [ ] Remover implementações movidas para Logger
- [ ] Adicionar referência a `NekoLib.Logger`
- [ ] Manter `CrashHandler` (partes cross-platform: `AppDomain`, `TaskScheduler`)
- [ ] Alterar `TargetFrameworks` de `net481;net9.0-windows` → `net481;net9.0`

### A4 — Criar `NekoLib.Diagnostics.Windows` (net481; net9.0-windows)

- [ ] Criar `src/Diagnostics/NekoLib.Diagnostics.Windows/NekoLib.Diagnostics.Windows.csproj`
- [ ] Mover de `NekoLib.Diagnostics`:
  - `CrashHandler` — partes WinForms (`Application.SetUnhandledExceptionMode`, `Application.ThreadException`)
  - `MiniDumpWriter` (`dbghelp.dll` PInvoke)
  - `CrashSuppressor` (`kernel32.dll` PInvoke)
  - `CrashDumpLevel`
- [ ] Adicionar referência a `NekoLib.Diagnostics`
- [ ] Registrar no `NekoLib.sln`
- [ ] Atualizar README / CLAUDE.md com novo projeto

### A5 — Atualizar `NekoLib.Navigation` (net481; net9.0)

- [ ] Substituir referência `NekoLib.Diagnostics` → `NekoLib.Core`
- [ ] Alterar `TargetFrameworks` de `net481;net9.0-windows` → `net481;net9.0`
- [ ] Verificar `DiagnosticsNavigationSink` — usa apenas contratos de Core; ajustar se necessário

### A6 — Projetos com mudança só no target

- [ ] `NekoLib.Mvvm`: `net9.0-windows` → `net9.0` (sem mudança de código)
- [ ] `NekoLib.Pipes`: já `net9.0`, confirmar que não há dependência Windows oculta

### A7 — Nullable unificado

> Decisão: **habilitar** em todos os projetos.
> Novos projetos (Core, Logger, DebugUtils) nascem com nullable enable — custo zero.
> Projetos existentes: habilitar com warnings (não errors); usar `#nullable disable` localizado em arquivos ainda não migrados; migrar incrementalmente.

- [ ] Habilitar `<Nullable>enable</Nullable>` nos projetos ainda em `disable`:
  - `NekoLib.Diagnostics`
  - `NekoLib.Devices`
  - `NekoLib.Navigation`
  - `NekoLib.Navigation.WinForms`
  - `NekoLib.Navigation.Wpf`
  - `NekoLib.Mvvm`
  - `NekoLib.Watchdog`
  - `NekoLib.Watchdog.Host`
- [ ] Confirmar que build não quebra (warnings são aceitáveis nesta etapa)
- [ ] Anotar APIs públicas críticas nos módulos mais usados (Navigation, Diagnostics, Data)

### A8 — Validação

- [ ] `dotnet build NekoLib.sln` — 0 erros
- [ ] `dotnet test` — todos os testes passando
- [ ] Confirmar targets finais de cada projeto

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
