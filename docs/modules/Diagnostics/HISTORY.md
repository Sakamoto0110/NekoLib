# NekoLib.Diagnostics History

**Document ID:** DIAG-HISTORY

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** factual chronological history of the NekoLib.Diagnostics family boundary

**Surface:** history

**Boundary:** diagnostics

**Authority role:** evidence

**Mutation:** append-only

**Indexing:** include

## 2026-06-09 — DIAG-HISTORY-001 — Platform boundary established

**Release:** none

- Crash orchestration remained in the cross-platform Diagnostics package while
  native WinForms hooks, Windows error-mode control, and `dbghelp.dll` minidump
  writing moved into the separate Diagnostics.Windows adapter.

**Evidence:** [`../../history/phase-f1-public-api-release-stability-2026-08-21.md`](../../history/phase-f1-public-api-release-stability-2026-08-21.md), [`audits/windows-public-api-review-2026-08-17.md`](audits/windows-public-api-review-2026-08-17.md)

## 2026-08-01 — DIAG-HISTORY-002 — Phase D incident-evidence composition completed

**Release:** none

- Diagnostics became the incident-evidence owner over optional Core logging,
  telemetry, and read-only inspection sources, with budgets, redaction, partial
  bundles, and no dependency on their concrete feature packages.

**Evidence:** [`../../audit/diagnostics-boundaries-review-2026-07-30.md`](../../audit/diagnostics-boundaries-review-2026-07-30.md), [`REFERENCE.md`](REFERENCE.md)

## 2026-08-08 — DIAG-HISTORY-003 — Watchdog-shaped notification removed

**Release:** none

- External notification became an application-owned callback independent of
  Watchdog environment state, and the explicit WinForms hook became
  process-idempotent.

**Evidence:** [`../../history/phase-e-confidence-stabilization-2026-08-12.md`](../../history/phase-e-confidence-stabilization-2026-08-12.md), [`migrations/f1.md`](migrations/f1.md)

## 2026-08-17 — DIAG-HISTORY-004 — Stable public API dispositions completed

**Release:** pre-1.0.0

- The core and Windows public surfaces were finalized: options became captured,
  disposal became terminal, registry hooks became releasable, contributor and
  local evidence bounds were hardened, bundle failure became observable, and
  the native adapter retained exact non-cumulative dump-level semantics.

**Evidence:** [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md), [`audits/windows-public-api-review-2026-08-17.md`](audits/windows-public-api-review-2026-08-17.md), [`migrations/f1.md`](migrations/f1.md)

## 2026-08-18 — DIAG-HISTORY-005 — Lifecycle race and package candidate qualified

**Release:** 1.0.0-local.20

- The final install/dispose race was serialized with terminal disposal winning;
  the dual-target focused suite, accepted APIs, immutable packages, and isolated
  PackageReference probes qualified the candidate.

**Evidence:** [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md), [`audits/windows-public-api-review-2026-08-17.md`](audits/windows-public-api-review-2026-08-17.md)

## 2026-08-21 — DIAG-HISTORY-006 — Stable 1.0.0 family released

**Release:** 1.0.0

- Both packages joined the first coordinated stable NekoLib release with
  materialized package hashes and qualifying `1.0.0-local.22` evidence.

**Evidence:** [`../../stable-release-1.0.0.md`](../../stable-release-1.0.0.md), [`../../history/phase-f1-public-api-release-stability-2026-08-21.md`](../../history/phase-f1-public-api-release-stability-2026-08-21.md)

## 2026-08-27 — DIAG-HISTORY-007 — External security-policy horizon recorded

**Release:** 1.0.0

- The external NekoMarketplace review confirmed Diagnostics as the crash-bundle
  mechanism while leaving encryption, retention, upload, and consumer-specific
  secrecy policy outside this package and outside current promoted work.

**Evidence:** [`../../audit/nekomarketplace-external-consumer-evidence-intake-2026-08-26.md`](../../audit/nekomarketplace-external-consumer-evidence-intake-2026-08-26.md), [`FINDINGS.md`](FINDINGS.md)

## 2026-08-28 — DIAG-HISTORY-008 — Public XML documentation delivery qualified

**Release:** 1.1.0-local.8

- Accepted public and protected members received effective XML documentation
  without API changes, and the corrected package flow proved package-owned XML
  files and PackageReference delivery for both Diagnostics packages.

**Evidence:** [`../../audit/public-api-documentation-extensibility-review-2026-08-27.md`](../../audit/public-api-documentation-extensibility-review-2026-08-27.md)

## 2026-08-29 — DIAG-HISTORY-009 — Module-first documentation established

**Release:** unreleased documentation

- The Diagnostics family received one canonical module boundary with a manifest,
  concise introduction, normative reference, separate history/changelog,
  issues/findings split, validation contract, curated evidence, colocated F1
  audits and migration, and a pointer-only source portal.

**Evidence:** [`MANIFEST.md`](MANIFEST.md), [`../../governance/agent-documentation-contract.md`](../../governance/agent-documentation-contract.md)
