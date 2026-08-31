# NekoLib.Data Changelog

**Document ID:** DATA-CHANGELOG

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** consumer-visible evolution of the NekoLib.Data boundary

**Surface:** changelog

**Boundary:** data

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

The [coordinated family changelog](../../../CHANGELOG.md) remains the release
summary. This file records Data-specific consumer impact without duplicating
package hashes or treating local candidates as public releases.

## 1.1.0

**Packages:** `NekoLib.Data`

**Compatibility class:** additive

**Consumer impact:** Structured QueryBuilder APIs, explicit write/read type-adaptation policy, and complete XML member documentation are available in post-1.0.0 source. Four legacy builder overloads remain warning-only compatibility shims; no current runtime contract or target surface was removed.

**Migration:** `docs/modules/Data/migrations/querybuilder-structured-api.md`, `docs/modules/Data/migrations/data-type-adaptation.md`

- Canonical `Value`, `Set`, structured `Where`, and `JoinOn` APIs separate
  parameterized values from caller-trusted identifiers and SQL fragments.
- Builder reuse, subquery parameter isolation, empty-collection predicates, and
  unconstrained update/delete guards retain fail-closed behavior.
- Write promotion, provider decay, exact loss authorization, schema discovery,
  DTO temporal materialization, and value-free adaptation evidence are explicit
  policies rather than provider-call retries or implicit conversions.
- Documentation-enabled builds produce Data XML assets for both targets.
  Immutable candidate `1.1.0-local.9` is the qualifying package evidence for
  the stable release; earlier candidates remain historical evidence only.

## 1.0.0

**Packages:** `NekoLib.Data`

**Compatibility class:** mixed

**Consumer impact:** The pre-stable candidate gateway was corrected before the first stable contract. Consumers upgrading from an earlier candidate may need to choose an explicit result family, update session/cancellation argument order, and compile streaming use only for `net9.0`.

**Migration:** `docs/modules/Data/migrations/f1.md`

- `IDatabaseGateway` became a composition of narrower raw, DTO, dynamic, DML,
  transaction, and target-conditional streaming capability interfaces.
- Universal `Get<T>`/`Read<T>` calls were replaced by explicit raw, DTO, and
  dynamic methods; `RecordItem` remained an intentionally lossy compatibility
  result rather than the canonical DTO path.
- Async streaming became a real `net9.0` surface and is absent from the
  `net481` assembly. DTO reflection entry points gained modern-target trimming
  annotations without changing the legacy target.
- Context/factory ownership, session affinity and nested transactions, command
  timeout, cancellation, mapping failure, observer isolation, and bounded
  dynamic IL behavior were finalized before stable release.
