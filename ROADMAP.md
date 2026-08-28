# NekoLib Roadmap

**Document ID:** GLOBAL-ROADMAP

**Schema version:** 1

**Kind:** roadmap/status

**Lifecycle:** current

**Subject:** product direction, strategic intentions, guardrails, freezes, and planning horizons

**Surface:** roadmap

**Boundary:** global

**Authority role:** roadmap

**Mutation:** authored

**Indexing:** include

**Direction decision date:** 2026-08-27

## Purpose

This document owns NekoLib's current direction and intentions. It explains
where the framework is heading, which boundaries must be preserved, and which
horizons are worth investigating. It does not authorize implementation by
itself.

Use:

- [`TODO.md`](TODO.md) for formally promoted work, execution order, gates, and
  completion criteria;
- [`docs/proposals/`](docs/proposals/README.md) for concise unpromoted ideas that
  still require investigation or disposition;
- module findings and issues for uncertain observations and confirmed defects;
- [`docs/audit/`](docs/audit/README.md) for dated evidence and decision records;
  and
- [`docs/history/`](docs/history/README.md) for completed or superseded roadmap
  state.

An intention recorded here is not a task. Promotion requires a formalized and
accepted decision in `TODO.md`, regardless of whether the idea originated in a
proposal, finding, issue, audit, external-consumer record, or direct owner
decision.

## Current direction

NekoLib remains a Windows-oriented application framework for small and medium
PDV/DM applications, including unattended, touch-first, single-window shells
that may remain on `net481` or run on `net9.0`.

The current direction is to:

- preserve the validated Phase E behavior and evidence boundaries;
- prefer evidence over new abstractions and narrow fixes over speculative
  modules;
- keep project references shallow and preserve opt-in and NO-OP behavior;
- preserve Logging, Telemetry, Inspection, Diagnostics, and
  Diagnostics.Windows as distinct capabilities;
- keep Windows-specific crash behavior isolated in Diagnostics.Windows;
- keep Data, Devices, Mvvm, and Pipes independent of Core unless a separately
  accepted module decision changes that graph;
- keep Watchdog a local process supervisor across a process/IPC boundary;
- distinguish automated, build-only, manual, interactive, package, provider,
  hardware, short-window, and duration evidence truthfully;
- avoid generalizing application-specific infrastructure into the framework;
  and
- require real use cases before accepting scale, fleet, portability, remote,
  or platform abstractions.

The current module map, targets, dependency graph, public entry points, and
package overview remain owned by [`README.md`](README.md), project files, and
source. Package versions remain immutable, validation remains manually
triggered, and Windows is required for full dual-target validation.

## Planning horizons

### Promoted horizon

Two bounded records from the NekoMarketplace external-consumer intake are now
formally promoted in [`TODO.md`](TODO.md):

- `NEKOMKT-F009` ships generated XML API documentation with packageable managed
  library assets; and
- `NEKOMKT-F026` removes the legacy SQL-inverted
  `QueryBuilder.Join(string, string, string)` overload only after its accepted
  compatibility window and next-major gate are satisfied.

Both entries retain explicit implementation gates. Their presence here records
the promoted horizon; `TODO.md` remains the only work scheduler and admission
gate.

The Data type-adaptation and QueryBuilder API normalization work completed on
2026-08-27. Its accepted decisions and closure evidence are preserved in the
historical
[`Data review`](docs/audit/data-type-adaptation-querybuilder-api-review-2026-08-26.md).

### Decision horizon

`NEKOMKT-F022` requires an explicit product/security decision about minidump
confidentiality. External-consumer evidence demonstrated that an exception
secret redacted from `crash.txt` remained in a `MiniDumpNormal` file. Current
documentation warns that dumps can contain sensitive process memory, but the
`Redact` and `WindowsCrash.UseMiniDump()` contracts do not state one combined
confidentiality boundary.

The decision must determine whether the current opt-in dump behavior plus an
explicit contract is sufficient, or whether NekoLib should change the dump,
storage, or access-control boundary. This roadmap entry does not choose an
implementation and does not authorize Diagnostics or Diagnostics.Windows
changes. Any accepted work must be admitted separately to `TODO.md`.

Payments/Pix has a completed design review but no accepted implementation
decision. Its concise current proposal is
[`payments-pix.md`](docs/proposals/payments-pix.md); the detailed review remains
dated decision input rather than implementation authorization.

### Investigation horizon

Three Navigation directions are admitted to this roadmap for further design.
They are intentionally not implementation-ready: each still needs a concrete
consumer workflow, an accepted public boundary, and separate admission to
[`TODO.md`](TODO.md).

#### `NAV-DESIGN-PREFABS` — adapter-owned design-time prefabs

Extend the WinForms and WPF adapter toolkits with optional, reusable prefabs
that reduce the amount of application-owned host, page, and surface setup while
remaining useful in the native Visual Studio designers. The intended outcome is
faster composition of a conventional Navigation shell and common surfaces, not
a cross-platform visual framework or a replacement for framework-native pages.

`Prefab` is planning vocabulary, not an accepted type or packaging model. The
design still needs to determine:

- which recurring structures deserve a prefab, such as the layered host,
  loading mask, and common toast, dialog, prompt, or popover arrangements;
- whether each platform should ship editable base controls, composed controls,
  item templates, designer metadata, or another native mechanism;
- how consumer customization, accessibility, localization, theming, DPI/scale,
  and design-time sample data remain application-owned;
- which differences between WinForms and WPF should remain intentional; and
- whether the assets belong in the existing adapter packages, beside their
  `Hosting`/`Toolkit` areas, or in separate design-time packages.

This direction does not imply expanding the runtime `INavigationToolkit`
contract merely because it shares the toolkit name. It must remain additive and
optional, keep custom adapters compiling, and avoid changes to the frozen
Navigation core unless a later accepted design proves a narrow change is
necessary. Promotion requires one designer-first prototype per shipped adapter,
a concrete authoring walkthrough, and an explicit public API and package review.

#### `NAV-MULTI-HOST-2` — independently addressable Navigation hosts

Explore more than one active Navigation host in the same application process.
This direction is reserved exclusively for the `2.0.0` architecture window. It
must not weaken or emulate around the current 1.x single-mounted-context
contract, and it does not turn overlays or visual regions into nested pages.

The design must define:

- host identity, discovery, and the application-facing replacement or evolution
  of the process-wide static `NavigationService` facade;
- ownership and isolation of context, registry, page caches, history, session,
  idle behavior, surfaces, services, and observation state per host;
- whether any resources may be shared deliberately and how cross-host
  navigation or coordination is requested without implicit global routing;
- UI-thread and dispatcher rules when hosts belong to one or multiple windows
  or UI threads;
- reset, shutdown, failure containment, concurrent-operation admission, and
  page-instance ownership across host lifetimes; and
- a migration path that keeps the current single-host use case concise.

Promotion requires representative multi-window or multi-display consumer
scenarios, a complete lifecycle/concurrency model, a reviewed 1.x-to-2.0 API
migration, and an explicit unfreeze of every affected stability-sensitive type.
No multi-host implementation or compatibility shim is admitted for 1.x.

#### `NAV-REGISTRY-GENERATORS` — compile-time registry generation

Explore an optional Roslyn source-generator path for deterministic page
discovery and registry setup at compile time. The desired consumer outcome is a
strongly typed, discoverable registration artifact with useful build diagnostics
and less reliance on runtime assembly scanning, while retaining an explicit
bootstrap choice and a supported non-generated path.

The generator design still needs to determine:

- the generated contract consumed by `PageNavBootstrap` and whether generation
  covers discovery only or also validated descriptor construction;
- exact parity with the current defaults -> attributes -> manual DSL precedence,
  including explicit registrations, custom factories, idle selection, default
  loading-mask discovery, duplicate names/types, and invalid metadata;
- which conditions should become compiler diagnostics and which must remain
  bootstrap-time validation;
- incremental-build behavior, generated-code inspectability, package/MSBuild
  delivery, and consumer compatibility across `net481` and `net9.0`; and
- how applications that load page assemblies dynamically or cannot run the
  generator retain a clear reflection/manual-registration fallback.

This direction does not authorize runtime compilation, a plugin system, or a
second registry with different semantics. It may be considered for 1.x only if
the final design is opt-in and additive and does not require a breaking public
surface or an unapproved change to frozen `PageRegistry`/`PageFactory` behavior;
otherwise it moves to the 2.0 architecture window. Promotion requires a small
consumer prototype, deterministic diagnostic and metadata-parity tests, startup
and build-cost evidence, and a reviewed package/public API boundary.

The design-time prefab and registry-generator investigations may proceed
independently. Multi-host remains a 2.0-only architecture investigation and is
not a prerequisite for either authoring improvement.

The following other ideas remain unpromoted:

- [automated release confidence](docs/proposals/automated-release-confidence.md);
- [measured performance and resource budgets](docs/proposals/performance-resource-budgets.md);
- [optional external evidence export](docs/proposals/external-evidence-export.md);
- [fleet-management assessment](docs/proposals/fleet-management-assessment.md);
- [intentional portability preparation](docs/proposals/portability-preparation.md);
  and
- [Navigation surface regions and toast orchestration](docs/proposals/navigation-surface-regions.md).

Their presence identifies a direction worth investigating. It does not reserve
an implementation slot or authorize product changes.

## Active guardrails and freezes

### Deferred Inspection module rollout

Broad Inspection instrumentation and state-changing actions remain frozen.
Core contracts, the global Inspection runtime, the Navigation producer, and the
Diagnostics read-only consumer exist, but a wider rollout has not demonstrated
enough operational value or a safe common action contract.

Current guardrails:

- Navigation is the only feature module that records Inspection operations;
- Diagnostics consumes only `IInspectionSnapshotSource` and cannot invoke
  actions;
- application code calling `Record(...)` manually is not proof that Data,
  Pipes, Devices, Watchdog, or Diagnostics emit module instrumentation;
- no feature module registers a real Inspection action;
- Watchdog crash notification crosses IPC and must not be treated as ordinary
  in-process recording; and
- no Instrumentation project family, TestControl project, plugin loader,
  privileged IPC host, or reflection activation system is accepted.

A future rollout must explicitly unfreeze one bounded module, define the
operational question its data answers, validate the smallest real producer,
preserve disabled/NO-OP behavior and both targets, and restore the broad freeze
after that scope closes. Data's `QueryExecutionContext` and Pipes'
`IPipeMetrics` remain the preferred first seams if this direction is promoted.

### Navigation stability-sensitive core

`NavigationContext`, `NavigationRuntime`, `PageRegistry`, and `PageFactory`
remain frozen after the accepted lifecycle and trace correction. No adapter or
planning intention authorizes changes to them.

A confirmed finding that requires one of these components must record evidence,
receive explicit promotion and a narrow module-scoped unfreeze, preserve the
canonical lifecycle invariants, and restore the freeze after the authorized
scope.

## Explicit non-goals

Without a real use case and an accepted promotion, do not create a generic
application host, Neko-specific DI container, Microsoft DI wrapper, global
service registry, message bus, event bus, universal exception policy, broader
HTTP client abstraction, API gateway, ORM expansion, repository/unit-of-work
framework, scheduler, job engine, distributed cache, configuration framework,
secret manager, plugin platform, Instrumentation project family, TestControl
project, generic remote debugger, cloud backend, dashboard, updater inside
Watchdog, or fleet-control plane.

## Completed direction milestones

- Phases A, B, and D: [architecture roadmap through Phase D](docs/history/architecture-roadmap-through-phase-d-2026-08-01.md).
- Phase C: [repository hygiene completion](docs/history/phase-c-repository-hygiene-2026-08-01.md).
- Phase E: [confidence stabilization completion](docs/history/phase-e-confidence-stabilization-2026-08-12.md).
- Phase G1: [typed HTTP integration completion](docs/history/phase-g1-http-integration-2026-08-16.md).
- Phase F1: [public API and release stability completion](docs/history/phase-f1-public-api-release-stability-2026-08-21.md).
- Data type adaptation and QueryBuilder normalization: [accepted decision and completion evidence](docs/audit/data-type-adaptation-querybuilder-api-review-2026-08-26.md).
- Stable family baseline: [`NekoLib 1.0.0`](docs/stable-release-1.0.0.md).

Historical test counts, hashes, package results, and implementation narratives
belong to those dated evidence records, not to this direction document.
