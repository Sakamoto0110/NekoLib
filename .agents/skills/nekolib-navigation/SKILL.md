---
name: nekolib-navigation
description: Implement, diagnose, review, document, or test the NekoLib Navigation family, including NavigationService, PageNavBootstrap, page lifecycle, history, guards, sessions, idle behavior, overlays, diagnostics, and WinForms or WPF adapters. Use for work under src/Navigation or tests/NekoLib.Navigation.Tests, and for application code whose behavior depends on NekoLib navigation APIs.
---

# Work on NekoLib Navigation

Preserve the deterministic desktop-navigation contract while making the
smallest change that satisfies the request.

## Establish current truth

1. Read `../../../AGENTS.md`.
2. Read `../../../src/Navigation/NekoLib.Navigation/README.md` completely before
   changing Navigation.
3. Inspect the affected source, tests, and every relevant `*.csproj`.
4. Treat source and project files as authoritative. Treat `TODO.md` and audit
   files as historical context that must be reverified.

## Classify the change

Locate the change in the existing architecture:

- `Contracts/`: framework-neutral contracts without platform assumptions
- `Metadata/`: attributes, enums, and descriptor data
- `Runtime/`: lifecycle, registry, factories, services, history, and session
- `Diagnostics/`: tracing plus optional Logging, Telemetry, and Inspection bridges
- `Toolkit/`: optional positioning helpers
- WinForms or WPF projects: native hosts, views, and platform adapters

Keep platform behavior in the adapter projects. Do not introduce a
repository-wide folder or layering pattern.

## Preserve Navigation invariants

- Preserve the public static `NavigationService` facade and
  `PageNavBootstrap.Start()` mounting model.
- Require `await NavigationService.Shutdown()` before mounting a fresh context.
- Treat the current README sections marked `DO NOT CHANGE`, frozen, or
  stability-sensitive as hard boundaries unless the user explicitly unfreezes
  them.
- Preserve UI-thread lifecycle execution, the navigation gate, guard timeout,
  redirect correlation, rollback, history, and overlay semantics documented by
  the current README.
- Do not add `ConfigureAwait(false)` inside the runtime UI path.
- Do not take the navigation gate for Dialog, Prompt, or Popover operations.
- Preserve the documented asymmetric overlay teardown behavior.
- Inspect the affected project file before changing target frameworks,
  compilation constants, nullable settings, implicit usings, or project
  references.
- Avoid `record` for types shared with `net481`.
- Do not broaden Inspection actions or module instrumentation while the
  relevant current freeze remains in force.

## Add or update tests

- Mirror the source area under
  `../../../tests/NekoLib.Navigation.Tests/Unit/`.
- Reuse the fakes in that project instead of introducing a second test
  harness.
- Name tests `MethodName_Condition_ExpectedResult`.
- Mark every test that mounts the static facade with
  `[Collection("NavigationServiceFacade")]`.
- Put `await NavigationService.Shutdown()` in a `finally` for every mounted
  facade test.
- Preserve exact lifecycle-order regressions when changing adjacent behavior.

## Verify proportionally

Start with the narrowest relevant test, then expand according to impact:

```powershell
dotnet test tests/NekoLib.Navigation.Tests/Unit/NekoLib.Navigation.Tests.Unit.csproj
dotnet build src/Navigation/NekoLib.Navigation/NekoLib.Navigation.csproj
dotnet test NekoLib.sln
```

Build the WinForms or WPF project directly when its adapter changes. Run
interactive applications under `runtime_tests/` as executables, never through
`dotnet test`. Report exactly what was run and do not claim a partial TFM or
platform validation as full coverage.
