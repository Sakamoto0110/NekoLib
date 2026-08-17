# Data Public API Review — 2026-08-17

**Kind:** audit

**Lifecycle:** historical

**Subject:** F1-DATA compiled public surface, gateway contract, target-specific
streaming, ownership, extensibility, mapping, and migration boundary

**Status:** all seven dispositions accepted and implemented

**Reference date:** 2026-08-17

**Reference commit:** `87b34b061f5db6cf50a28d3187070940b851e1be`

**Last reconciliation:** 2026-08-17

**Current state:** the [Data technical reference](../../src/Data/NekoLib.Data/README.md),
the [F1-DATA migration guide](../migrations/f1-data.md), and
[`TODO.md`](../../TODO.md) F1-DATA

## Baseline and authority

This review covers the committed source at the reference commit on branch
`phase-e/sqlserver-and-orchestration`. The worktree and index were clean before
the review artifact, and the branch was two commits ahead of its matching
remote branch. No F1 commit had been pushed.

The review changed no product code or accepted API baseline. Its only repository
changes are this audit and its documentation-index entries. Current source,
project files, the assembly-derived manifests under `eng/public-api/`, unit
tests, and the versioned Data runtime scenarios are authoritative. Historical
Data audits supplied leads only; every disposition below was rechecked against
the current tree.

## Scope

Included:

- the compiled `NekoLib.Data` surface for `net481` and `net9.0`;
- the public gateway implementation and capability interfaces;
- connection-factory, execution-context, session, query, mapping, dynamic-row,
  event, cancellation, disposal, and extension boundaries;
- repository consumers under `tests/` and `runtime_tests/Data/`;
- the accepted pre-stable correction, migration, and API-baseline rules.

Excluded:

- product-code or baseline changes;
- new providers, new query features, async transaction APIs, or behavior
  changes unrelated to the public contract;
- runtime execution of Access, SQLite, or SQL Server scenarios;
- a new cross-module dependency, facade pattern, global registry, or Core
  reference.

## Observed module boundary

`NekoLib.Data` targets `net481;net9.0`, has no project references, and keeps the
same package ownership boundary on both targets. The supported composition is:

1. a consumer supplies an `IDbConnectionFactory`, provider translator, options,
   and connection-factory ownership to `QueryExecutionContext`;
2. the context owns execution policy, translator selection, event subscribers,
   observer-failure retention, and optionally the factory lifetime;
3. `DatabaseGateway` executes through that context and creates context-affine
   `DbSession` instances;
4. consumers select raw, DTO, dynamic, callback, buffered, and, on `net9.0`,
   streaming query shapes.

`QueryExecutionContext` owns the meaningful lifecycle. `DatabaseGateway` is the
primary instance entry point, but it neither owns nor disposes the context. A
process-wide static facade would obscure this lifetime and is not justified.

The capability interfaces are real consumer contracts. Both versioned Data
scenarios construct one concrete gateway and expose it as `IDatabaseGateway`;
provider-specific schema and workload code accepts that interface. The
interfaces should therefore remain first-class rather than being removed merely
because many unit tests use the concrete type directly.

## Compiled target comparison

The accepted manifests contain 411 lines for `net481` and 421 for `net9.0`.
Most differences are intentional:

- `IDatabaseGateway` composes streaming only on `net9.0`;
- concrete streaming methods exist only on `net9.0`;
- DTO methods on the `net9.0` concrete class carry
  `DynamicallyAccessedMembers` requirements needed by reflection-based mapping;
- the target-framework assembly attribute differs.

Two differences are not acceptable final contracts:

1. `IDqlStreamingGateway` is still present in the `net481` assembly, marked
   `[Obsolete(error: true)]`, despite having no implementation and not being
   composed by `IDatabaseGateway`. This is an unusable ghost capability, not a
   supported target-specific API.
2. The `net9.0` DTO requirements appear on concrete methods but not on the
   matching interface generic parameters. A consumer following the recommended
   `IDatabaseGateway` path therefore loses the trimming/AOT contract that the
   implementation already declares.

## Findings and recommended dispositions

### F1-DATA-01 — The primary public type is internally named

**Observed:** the public `DatabaseGateway` is declared in
`NekoLib.Data.Internal.Gateway`; its public interfaces are in
`NekoLib.Data.Gateway`. Every tracked consumer must import an `Internal`
namespace to construct the documented primary entry point.

**Recommended decision:** move `DatabaseGateway` to
`NekoLib.Data.Gateway`. Do not retain a public compatibility shim in the old
namespace. The package is still pre-stable, every tracked consumer can migrate
in the same block, and preserving two public gateway identities would make the
candidate surface permanently ambiguous.

### F1-DATA-02 — Universal reads duplicate explicit families and break translator ownership

**Observed:** `IUniversalQueryGateway` and its implementation add four kinds of
ambiguity:

- `Get<TTranslator,T>` selects a new translator per call even though the
  execution context already owns the provider translator;
- `Read<T>` duplicates `ReadDto<T>` and dynamically special-cases `object` and
  `DynamicRow`;
- `Read(Delegate)` adds reflection and runtime signature validation to a path
  already represented by typed callbacks;
- `StreamData` duplicates `StreamDto` and `StreamDynamic`.

The FarmDatabase scenario records the concrete consequence: provider-agnostic
code must branch on its provider merely to name the translator generic type.
The SQL Server scenario exercises universal reads as matrix breadth, not as a
distinct required behavior.

**Recommended decision:** remove `IUniversalQueryGateway` and all `Get<
TTranslator,T>`, `Read`, and `StreamData` universal members. Preserve the
explicit raw, DTO, and dynamic families. Migration is direct:

| Removed shape | Replacement |
|---|---|
| `Get<TTranslator,T>` | configure the translator once in the context, then use `GetDto<T>` |
| `Read<T>` for DTOs | `ReadDto<T>` |
| `Read<DynamicRow>` or `Read<object>` | `ReadDynamic` |
| `Read(Delegate)` | a typed `ReadDto<T>` or `ReadDynamic` callback |
| `StreamData<T>` | `StreamDto<T>` |
| dynamic `StreamData` | `StreamDynamic` |

No replacement API is needed.

### F1-DATA-03 — Concrete and interface call shapes are inconsistent

**Observed:** session-aware string DML, `Delete`, string DTO reads, and the
non-session raw stream are available only through explicit interface
implementations. Other operations are public concrete methods. String DML also
places an optional session after the cancellation token, unlike the explicit
session overloads used by builder, read, and stream families. `ContainsData`
cannot bind parameters, which forces the tracked FarmDatabase scenario to
inline an otherwise parameterized constant.

**Recommended decision:** make the supported capability members equally
discoverable through `IDatabaseGateway` and the sealed concrete gateway. Use
separate non-session and session overloads, with the session immediately before
the cancellation token, instead of an optional session after it. Add parameter
support to `ContainsData`. Do not create every theoretical SQL/builder/result
combination; retain the currently supported explicit raw, DTO, dynamic,
callback, buffered, and streaming families.

This is a signature normalization inside the pre-stable candidate boundary.
The implementation block must record the exact manifest diff and named-argument
migration.

### F1-DATA-04 — net481 publishes an unusable streaming interface

**Observed:** `IDqlStreamingGateway` is compiled for `net481` only to be marked
as an obsolete error. No concrete streaming members exist there. The
`Microsoft.Bcl.AsyncInterfaces` package is consequently part of the `net481`
dependency graph for a capability the package cannot implement.

**Recommended decision:** expose `IDqlStreamingGateway` only on `net9.0`, where
the behavior exists. Remove the `net481` BCL async-interface dependency if the
post-change build confirms there is no remaining use. Keep callback reads on
both targets as the bounded-memory non-streaming alternative for `net481`.

### F1-DATA-05 — Interface-based DTO use loses the net9 trimming contract

**Observed:** concrete DTO methods carry public-constructor/public-properties
requirements, but `IDtoQueryGateway` and the DTO members of
`IDqlStreamingGateway` do not. `DataMapper` also reflects over caller-supplied
DTO types without declaring the same requirement on its generic or `Type`
entry points.

**Recommended decision:** mirror the existing `net9.0`
`DynamicallyAccessedMembers` annotations on every public interface or mapping
entry point that reflects over DTO construction and writable public properties.
Do not add the attribute to `net481`, where it is unavailable and unnecessary.

This corrects metadata; it does not claim that the entire package is trim-safe
or NativeAOT-compatible.

### F1-DATA-06 — One implementation helper is accidentally public

**Observed:** `DbDataReaderExtensions.HasColumn` is public under
`NekoLib.Data.Internal.Gateway`, has no repository consumer, and is not part of
gateway composition, provider extension, or a documented result contract.

**Recommended decision:** make it internal. Its namespace does not make it an
implementation detail while the compiled type remains public.

### F1-DATA-07 — Concrete inheritance is not the extension model

**Observed:** `DatabaseGateway`, `QueryExecutionContext`, `QueryBuilder`,
`RecordItem`, and `DbConnectionAbstractFactory<T>` are inheritable. Only the
context exposes a protected virtual member, and that member is the standard
dispose hook; no gateway execution stage is overridable. The real extension
seams are `IDbConnectionFactory`, `IDbQueryTranslator`, capability interfaces,
options, and event subscriptions.

**Recommended decision:** seal those five concrete types and remove the
protected gateway `Upsert` member and context dispose hook from the supported
surface. Preserve interface and translator/factory extensibility. Do not seal
the event-argument hierarchy, whose base/derived relationship is part of the
current event model.

## Candidate surface to retain

The following areas have current behavior, executable contracts, and a clear
consumer role. They should remain supported unless the implementation review
finds an unavoidable signature conflict:

| Area | Retained contract |
|---|---|
| Composition | `QueryExecutionContext`, `DatabaseGatewayOptions`, factory ownership, `IDbConnectionFactory`, and the simple generic factory |
| Query construction | `QueryBuilder`, `QueryModel`, `DatabaseQuery`, `IDbQueryTranslator`, and the Access, SQLite, and SQL Server translators |
| Execution | `IDatabaseGateway`, explicit raw/DTO/dynamic query capabilities, DML, sessions, cancellation, and `DbCommandPolicy` |
| Results and mapping | `RecordItem` as an explicitly lossy display/export cell, `DynamicRow`, `DataMapper`, strict/lenient mapping policy, and `DataMappingException` |
| Observation | generated/dispatched/success/error/stream-terminal events, bounded observer failures, SQL redaction defaults, and stream terminal outcomes |
| Dynamic IL | explicit opt-in options, bounded process-wide schema emission, Expando fallback policy, and passive `DynamicIlMetrics` |

`RecordItem` remains intentionally lossy under the previously accepted
DATA-017 disposition. Renaming it or changing null/binary/provider fidelity in
place would be a behavioral break unrelated to DATA-016. A future lossless row
model would need a separately accepted API.

No reviewed Data API needs an experimental marker. The retained surface is
either a stable-release candidate or an implementation detail after the
accepted removals.

## Rejected alternatives

- **Static process-wide facade:** rejected because the context has explicit
  caller-owned lifetime, provider, policy, and event state.
- **Separate internal executor refactor:** rejected for this block. Private
  execution methods already hide implementation; moving them would add churn
  without changing a consumer boundary.
- **Old-namespace compatibility shim:** rejected because no stable release or
  declared support window justifies two gateway identities.
- **Universal APIs marked experimental:** rejected because explicit families
  already replace every behavior and the translator override contradicts the
  context contract.
- **Streaming on `net481` through an async-interface package:** rejected because
  the current implementation has no such behavior. Publishing a type alone is
  not capability evidence.
- **Removing callback reads:** rejected because callbacks provide the
  non-buffered row-processing path common to both target families.
- **Wholesale `Async` suffix, namespace, or SQL-acronym renaming:** rejected for
  this block. It creates migration volume without resolving ownership,
  capability, or behavioral ambiguity.
- **Removing capability interfaces because tracked callers favor the composite:**
  rejected. Absence of a repository caller does not disprove external
  interface-based composition or testing.

## Proposed implementation block after acceptance

One narrow F1-DATA implementation should:

1. apply the accepted namespace, interface, overload, streaming, annotation,
   helper-visibility, and sealing changes;
2. migrate unit tests and versioned Data scenarios to the explicit families;
3. add interface/concrete parity and API-shape regressions on both targets;
4. add a current `NekoLib.Data` technical reference and exact migration guide;
5. update `CHANGELOG.md`, only the two Data API manifests, and `TODO.md` with
   the accepted decision and final evidence;
6. build and test both targets, build the affected runtime scenarios without
   executing external providers, run the Data API comparison, documentation
   verification, and full solution build;
7. create a new immutable local package version and exercise the public Data
   entry point from PackageReference-only consumers for `net481` and `net9.0`.

External Access, SQLite, or SQL Server execution is not automatically required
for a compile-time-only API reorganization. If implementation changes execution
behavior, that claim changes and the relevant independent runtime oracle must
be rerun.

## Review validation

The review baseline was revalidated without changing product code:

- `dotnet test tests/NekoLib.Data.Tests/Unit/NekoLib.Data.Tests.Unit.csproj
  -c Release --no-restore`: 111/111 passed on `net481` and 120/120 passed on
  `net9.0`;
- `eng/verify-public-api.ps1 -PackageId NekoLib.Data`: both accepted manifests
  matched the compiled assemblies, with zero build warnings and zero errors;
- `eng/verify-docs.ps1`: passed after this artifact was registered;
- `git diff --check`: passed for the review change.

These results establish the current candidate baseline. They do not validate a
future accepted API change or constitute external provider runtime evidence.

## Decision gate at review time

Nothing in this review authorizes product edits. F1-DATA can proceed only after
the user explicitly accepts, modifies, or rejects the recommended dispositions
for F1-DATA-01 through F1-DATA-07.

## Reconciliation — 2026-08-17

The user accepted all seven recommended dispositions. The implementation landed
in `59d1faf`, followed by package-consumer and package-manifest refinements in
`bced326` and `3e58df2`. The resulting current contract is maintained in the
Data technical reference and its exact candidate migration is maintained in the
migration guide; this review now remains historical decision evidence.

Validation of the accepted implementation established:

- 111/111 Data tests passed on `net481` and 119/119 on `net9.0`;
- 1280/1280 solution tests passed in the final full run;
- both compiled Data API manifests matched, with the intended reviewed changes
  only;
- Data, Farm WinForms, and the SQL Server scenario built on both target
  families with zero warnings and zero errors;
- documentation verification passed;
- `eng/pack-local.ps1 -PackageVersion 1.0.0-local.14` built from `3e58df2`, ran
  the serial solution suite, and passed all PackageReference-only consumers on
  both targets with zero consumer warnings;
- `NekoLib.Data.1.0.0-local.14.nupkg` has SHA-256
  `B5B03AD0CA92C8F7EB5BF413EB2242945E1222011FC1667684821E106173ECDC`.

No external provider scenario was executed for this API-shape block. The Farm
and SQL Server scenarios were compile-validated after migration; their earlier
runtime evidence remains separately attributed to its recorded package/source.
