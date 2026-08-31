# NekoLib.Data History

**Document ID:** DATA-HISTORY

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** factual chronological history of the NekoLib.Data boundary

**Surface:** history

**Boundary:** data

**Authority role:** evidence

**Mutation:** append-only

**Indexing:** include

This timeline summarizes outcomes without replacing the dated audits, migration
guides, runtime procedures, or release records that own the detailed evidence.

## 2026-05-31 — DATA-HISTORY-001 — Initial source audit reconciled

**Release:** pre-1.0.0

- The first-pass review established the provider-neutral gateway, QueryBuilder,
  mapping, dynamic-row, session, and target boundaries. Its later reconciliation
  recorded corrected OleDb compilation, subquery isolation, idempotent DML
  building, event cleanup, and nullable/build findings while preserving the
  original snapshot.

**Evidence:** [`audits/initial-audit.md`](audits/initial-audit.md)

## 2026-08-02 — DATA-HISTORY-002 — Stabilization contract implemented

**Release:** pre-1.0.0

- The accepted E1 stabilization work made provider async fallback explicit,
  isolated observers, bounded dynamic IL generation, enforced session affinity
  and deterministic cleanup, added strict/lenient mapping policy, made command
  timeout and parameter binding configurable, and added transaction-safe
  gateway overloads.

**Evidence:** [`audits/stabilization-review-2026-08-01.md`](audits/stabilization-review-2026-08-01.md)

## 2026-08-12 — DATA-HISTORY-003 — Real-provider confidence recorded

**Release:** pre-1.0.0

- Versioned FarmDatabase and SQL Server scenarios established separate SQLite,
  Access/OleDb, and SQL Server evidence across the supported target families.
  The SQL Server suite also recorded pooling, cancellation, connection-loss,
  recovery, and bounded-dynamic-schema behavior without turning provider
  packages or infrastructure into library-owned dependencies.

**Evidence:** [`../../history/phase-e-confidence-stabilization-2026-08-12.md`](../../history/phase-e-confidence-stabilization-2026-08-12.md), [`../../../runtime_tests/Data/FarmDatabase/README.md`](../../../runtime_tests/Data/FarmDatabase/README.md), [`../../../runtime_tests/Data/SqlServer/README.md`](../../../runtime_tests/Data/SqlServer/README.md)

## 2026-08-17 — DATA-HISTORY-004 — F1 public API finalized

**Release:** `1.0.0-local.14`

- The F1 correction replaced the universal query family with explicit raw,
  DTO, and dynamic capabilities; made streaming a real `net9.0`-only surface;
  aligned cancellation and session overloads; and preserved `RecordItem` as an
  explicitly lossy compatibility result.

**Evidence:** [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md), [`migrations/f1.md`](migrations/f1.md)

## 2026-08-21 — DATA-HISTORY-005 — First stable package baseline declared

**Release:** `1.0.0`

- `NekoLib.Data` entered the coordinated stable family baseline with separate
  accepted manifests for `net481` and `net9.0`, immutable package provenance,
  PackageReference consumer validation, and no project or package dependency.

**Evidence:** [`../../stable-release-1.0.0.md`](../../stable-release-1.0.0.md)

## 2026-08-27 — DATA-HISTORY-006 — QueryBuilder and type-adaptation policies completed

**Release:** post-1.0.0 additive source; disposable candidates through `1.1.0-local.4`

- Structured predicates and joins, explicit trusted-fragment APIs, reusable
  statement state, and warning-only compatibility shims were finalized.
  Write promotion, provider decay, explicit loss authorization, schema
  discovery, DTO temporal materialization, sanitized evidence, and real-provider
  probes then closed the accepted Data work item.

**Evidence:** [`audits/type-adaptation-querybuilder-api-review-2026-08-26.md`](audits/type-adaptation-querybuilder-api-review-2026-08-26.md), [`migrations/querybuilder-structured-api.md`](migrations/querybuilder-structured-api.md), [`migrations/data-type-adaptation.md`](migrations/data-type-adaptation.md)

## 2026-08-28 — DATA-HISTORY-007 — Public XML documentation and delivery qualified

**Release:** `1.1.0-local.8`

- Every accepted Data public member received effective XML documentation without
  a compiled-API change. The corrected immutable package flow proved both
  target-specific XML files inside `NekoLib.Data` and delivery to isolated
  PackageReference consumers.

**Evidence:** [`../../audit/public-api-documentation-extensibility-review-2026-08-27.md`](../../audit/public-api-documentation-extensibility-review-2026-08-27.md)

## 2026-08-31 — DATA-HISTORY-008 — First compatible minor published

**Release:** `1.1.0`

- The structured QueryBuilder, explicit write/read type-adaptation, temporal DTO
  materialization, warning-only compatibility shims, and complete XML
  documentation shipped in the coordinated stable family. The clean local
  package set, repository-signed public downloads, and external consumers were
  verified without changing the accepted Data API manifests.

**Evidence:** [`../../stable-release-1.1.0.md`](../../stable-release-1.1.0.md), [`../../history/release-1.1.0-2026-08-31.md`](../../history/release-1.1.0-2026-08-31.md)
