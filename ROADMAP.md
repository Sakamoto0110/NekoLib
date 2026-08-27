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

**Direction decision date:** 2026-08-26

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

No implementation is currently promoted. [`TODO.md`](TODO.md) remains the only
work scheduler and admission gate.

The Data type-adaptation and QueryBuilder API normalization work completed on
2026-08-27. Its accepted decisions and closure evidence are preserved in the
historical
[`Data review`](docs/audit/data-type-adaptation-querybuilder-api-review-2026-08-26.md).

### Decision horizon

Payments/Pix has a completed design review but no accepted implementation
decision. Its concise current proposal is
[`payments-pix.md`](docs/proposals/payments-pix.md); the detailed review remains
dated decision input rather than implementation authorization.

### Investigation horizon

The following ideas remain unpromoted:

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
