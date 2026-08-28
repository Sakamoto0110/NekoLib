# NekoLib.Navigation History

**Document ID:** NAV-HISTORY

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** factual chronological history of the NekoLib.Navigation family boundary

**Surface:** history

**Boundary:** navigation

**Authority role:** evidence

**Mutation:** append-only

**Indexing:** include

## 2026-06-03 — NAV-HISTORY-001 — Initial lifecycle audit and regression harness

**Release:** none

- The first multi-pass Navigation audit reconciled lifecycle, history, state,
  loading, surfaces, and the runnable WinForms consumer; its fixes established
  the initial dual-target automated regression harness.

**Evidence:** [`audits/initial-audit.md`](audits/initial-audit.md)

## 2026-07-28 — NAV-HISTORY-002 — Lifecycle and observation hardening

**Release:** none

- The bounded lifecycle/trace correction established request/attempt
  correlation before UI dispatch, rollback and terminal behavior, safe
  concurrent shutdown admission, background-load terminals, and passive
  Navigation observation. The four stability-sensitive core types were frozen
  again after completion.

**Evidence:** [`../../history/phase-e-confidence-stabilization-2026-08-12.md`](../../history/phase-e-confidence-stabilization-2026-08-12.md), [`REFERENCE.md`](REFERENCE.md)

## 2026-08-04 — NAV-HISTORY-003 — Native adapter hardening completed

**Release:** none

- The WinForms and WPF adapter review closed NAV-001 through NAV-011, covering
  truthful dispatch, focus and light dismissal, modal blocking, idle rearming,
  toolkit wiring, surface placement, DPI behavior, page naming, and native
  cleanup without changing the frozen runtime core.

**Evidence:** [`audits/adapter-review-2026-08-03.md`](audits/adapter-review-2026-08-03.md), [`../../../runtime_tests/Navigation/WinFormsSmoke/README.md`](../../../runtime_tests/Navigation/WinFormsSmoke/README.md), [`../../../runtime_tests/Navigation/WpfSmoke/README.md`](../../../runtime_tests/Navigation/WpfSmoke/README.md)

## 2026-08-06 — NAV-HISTORY-004 — Surface bases made designer-loadable

**Release:** none

- WinForms and WPF surface bases became non-abstract with protected
  constructors, and the WinForms parent-change path became safe before handle
  creation. Automated coverage and a real Visual Studio WinForms designer pass
  preserved the remaining generic-prompt shim as an explicit consumer cost.

**Evidence:** [`audits/design-time-2026-08-06.md`](audits/design-time-2026-08-06.md)

## 2026-08-11 — NAV-HISTORY-005 — Long-running and recovery gate completed

**Release:** none

- Native 20-minute smokes passed on WinForms `net481`, WinForms
  `net9.0-windows`, and WPF `net9.0-windows`; the WinForms `net481` recovery
  rehearsal exercised all fourteen planned faults for 70 minutes with clean
  awaited shutdown and no owned resource left behind.

**Evidence:** [`../../../runtime_tests/Navigation/LongRunningRecovery/README.md`](../../../runtime_tests/Navigation/LongRunningRecovery/README.md), [`../../history/phase-e-confidence-stabilization-2026-08-12.md`](../../history/phase-e-confidence-stabilization-2026-08-12.md)

## 2026-08-21 — NAV-HISTORY-006 — Stable 1.0.0 API family declared

**Release:** 1.0.0

- The core, WinForms, and WPF public surfaces were finalized against six
  assembly-derived API baselines, the F1 migration guide was published, and the
  three packages joined the first stable coordinated NekoLib release.

**Evidence:** [`audits/public-api-review-2026-08-20.md`](audits/public-api-review-2026-08-20.md), [`audits/winforms-public-api-review-2026-08-21.md`](audits/winforms-public-api-review-2026-08-21.md), [`audits/wpf-public-api-review-2026-08-21.md`](audits/wpf-public-api-review-2026-08-21.md), [`migrations/f1.md`](migrations/f1.md), [`../../stable-release-1.0.0.md`](../../stable-release-1.0.0.md)

## 2026-08-28 — NAV-HISTORY-007 — Public XML documentation and delivery qualified

**Release:** 1.1.0-local.8

- All accepted public and protected members across the six target assemblies
  received effective XML documentation without an API-baseline change. The
  corrected immutable package flow then proved package-owned XML files beside
  every Navigation assembly and PackageReference delivery to isolated
  WinForms and WPF consumers.

**Evidence:** [`../../audit/public-api-documentation-extensibility-review-2026-08-27.md`](../../audit/public-api-documentation-extensibility-review-2026-08-27.md)
