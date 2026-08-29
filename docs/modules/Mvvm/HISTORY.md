# NekoLib.Mvvm History

**Document ID:** MVVM-HISTORY

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** factual chronological history of the NekoLib.Mvvm boundary

**Surface:** history

**Boundary:** mvvm

**Authority role:** evidence

**Mutation:** append-only

**Indexing:** include

## Empty state — 2026-08-25

No module-first history entries were curated during the structural pilot at
commit `806f0bdf06fc406c2d942b1e74e77793818de906`. This is not evidence that the
module has no history. Later entries must be factual, chronological, append-only,
and linked to preserved audits, migrations, release records, or validations.

The block above is the pilot's own dated record and is preserved unchanged. The
entries below were curated on 2026-08-29 from source history, the F1 audit, the
migration guide, and release evidence.

## 2026-06-03 — MVVM-HISTORY-001 — Module created as a Navigation sibling

**Release:** none

- `NekoLib.Mvvm` was added alongside a Navigation change as a deliberately
  separate package: `ViewModelBase`, `RelayCommand`, and `RelayCommand<T>` with
  typed parameter coercion. It was a sibling of Navigation from the first commit,
  never a part of it, and took no project reference then or since.

**Evidence:** commit `1c473f6`, [`REFERENCE.md`](REFERENCE.md)

## 2026-06-09 — MVVM-HISTORY-002 — Library retargeted and nullable enabled

**Release:** none

- Phase A retargeted the library from `net9.0-windows` to plain `net9.0`,
  confirming that nothing in it needs the Windows desktop framework, and then
  enabled nullable annotations across the project. The focused test project kept
  `net9.0-windows`, which is why the shipped `net9.0` assembly is still exercised
  through a Windows-flavoured host rather than its own target framework.

**Evidence:** commits `ce69e9f` and `1480cc7`, [`VALIDATIONS.md`](VALIDATIONS.md)

## 2026-08-17 — MVVM-HISTORY-003 — Stable public API dispositions completed

**Release:** pre-1.0.0

- The F1-MVVM review accepted every disposition, including the optional one. No
  type, member, signature, default value, namespace, target, or dependency was
  added or removed. `OnPropertyChanged` became `virtual`, making one override a
  funnel for every notification `SetProperty` raises, and the nullability
  contract was corrected to match `ICommand`, `INotifyPropertyChanged`, and the
  module's own behavior. The module's own warning count went from 20 to 0 and the
  focused suite grew to 34 cases per target.
- One review claim was measured and corrected rather than carried forward: the
  annotation fix does not leave every consumer warning-free. `RelayCommand` now
  takes `Action<object?>`, so a lambda that dereferences the parameter without
  checking gains `CS8602` — a true positive that the previous annotation denied.

**Evidence:** [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md), [`migrations/f1.md`](migrations/f1.md)

## 2026-08-18 — MVVM-HISTORY-004 — Package candidate qualified

**Release:** 1.0.0-local.20

- The coordinated clean package campaign qualified the F1 implementation. The
  candidate carried both target assemblies and empty dependency groups on both,
  which is the packaged form of the module's no-dependency claim.

**Evidence:** [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md), [`VALIDATIONS.md`](VALIDATIONS.md)

## 2026-08-21 — MVVM-HISTORY-005 — Stable 1.0.0 released

**Release:** 1.0.0

- `NekoLib.Mvvm` joined the first coordinated stable NekoLib release with a
  materialized package hash and qualifying `1.0.0-local.22` evidence.

**Evidence:** [`../../stable-release-1.0.0.md`](../../stable-release-1.0.0.md), [`../../history/phase-f1-public-api-release-stability-2026-08-21.md`](../../history/phase-f1-public-api-release-stability-2026-08-21.md)

## 2026-08-26 — MVVM-HISTORY-006 — Chosen as the module-first structural pilot

**Release:** 1.0.0

- Mvvm was the boundary used to establish the module-first documentation
  infrastructure. Its manifest, introduction, technical reference, audit, and
  migration guide were placed at their canonical paths and the source-adjacent
  README became a pointer-only portal. The remaining registers were created as
  explicit empty-state scaffolds, and the pilot said so rather than implying the
  module had no history, issues, or evidence.

**Evidence:** commit `0fa1a32`, [`MANIFEST.md`](MANIFEST.md)

## 2026-08-28 — MVVM-HISTORY-007 — Public XML documentation delivery qualified

**Release:** 1.1.0-local.8

- The family documentation campaign closed the remaining public-member
  documentation gaps and the corrected package flow proved package-owned XML
  files and PackageReference delivery for both target assets.

**Evidence:** [`../../audit/public-api-documentation-extensibility-review-2026-08-27.md`](../../audit/public-api-documentation-extensibility-review-2026-08-27.md), [`VALIDATIONS.md`](VALIDATIONS.md)

## 2026-08-29 — MVVM-HISTORY-008 — Module-first documentation semantically populated

**Release:** unreleased documentation

- The pilot scaffolds were replaced with reviewed content: a populated module
  changelog, this chronology, a bounded issues conclusion, a source-first
  findings review, twelve risk-derived validation requirements, and an evidence
  register that separates source, build, focused-test, API, cross-boundary
  consumer, interactive, package, release, and documentation layers. Two XML
  comments were corrected. No structural path moved and no runtime behavior or
  public API changed.

**Evidence:** [`MANIFEST.md`](MANIFEST.md), [`../../governance/agent-documentation-contract.md`](../../governance/agent-documentation-contract.md)
