# NekoLib.Data Validation Requirements

**Document ID:** DATA-VALIDATION-REQUIREMENTS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** evidence contract for the NekoLib.Data boundary

**Surface:** validation-requirements

**Boundary:** data

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

The [module manifest](MANIFEST.md) owns the inherited profile list. The
requirements below specialize those profiles for Data's target split, mutable
query construction, ADO.NET ownership, provider boundary, value-conversion
policy, and external database evidence.

## DATA-VALREQ-001

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to source, project, target, nullable, dependency, or package settings

**Category:** build

**Boundary:** in-process

**Targets:** `NekoLib.Data` on `net481` and `net9.0`

**Acceptance criteria:** Both target assemblies build with zero errors and no new normalized warning identity; the project continues to declare exactly `net481;net9.0` and no project or package dependency.

**Required evidence level:** build-only

**Rationale:** Conditional APIs, framework-owned ADO.NET references, nullable contracts, and the no-dependency boundary can diverge even when one target compiles.

## DATA-VALREQ-002

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to a public declaration, target guard, nullable annotation, default value, or package boundary

**Category:** api-compatibility

**Boundary:** in-process

**Targets:** accepted `NekoLib.Data` manifests for `net481` and `net9.0`

**Acceptance criteria:** Compiled assemblies match both accepted manifests without an automatic baseline update; the only intentional target-family difference remains the `net9.0` streaming capability and modern DTO trimming metadata.

**Required evidence level:** build-only

**Rationale:** The legacy and modern compiled surfaces are separate compatibility contracts, and source review cannot prove emitted metadata parity.

## DATA-VALREQ-003

**Classification:** REQUIRED

**Trigger:** every Data implementation or contract change and every release candidate

**Category:** focused-regression

**Boundary:** in-process

**Targets:** the complete `NekoLib.Data.Tests.Unit` suite on `net481` and `net9.0`

**Acceptance criteria:** Every focused test passes with zero failures and zero unexpected skips, preserving gateway, builder, translator, binder, mapping, dynamic, session, event, cancellation, and adaptation behavior on both targets.

**Required evidence level:** automated-runtime

**Rationale:** Data coordinates mutable builders, disposable ADO.NET objects, callbacks, asynchronous fallbacks, reflection, generated types, and policy-driven conversion that compilation alone cannot qualify.

## DATA-VALREQ-004

**Classification:** REQUIRED

**Trigger:** every change to QueryBuilder, translators, raw command surfaces, parameter specifications, or binding selection

**Category:** security

**Boundary:** in-process

**Targets:** both focused test targets and every affected translator/binder path

**Acceptance criteria:** Tests prove value parameterization, trusted identifier/fragment boundaries, placeholder tokenization outside literals/comments, empty-collection and unconstrained-DML guards, reusable and idempotent statement builds, subquery isolation, named binding, and OleDb positional binding by SQL occurrence with missing and unused values rejected before dispatch.

**Required evidence level:** automated-runtime

**Rationale:** SQL injection and parameter misbinding are boundary defects even when generated SQL appears syntactically valid.

## DATA-VALREQ-005

**Classification:** REQUIRED

**Trigger:** every change to connection factories, contexts, gateways, sessions, transactions, disposal, or ownership configuration

**Category:** focused-regression

**Boundary:** database

**Targets:** both focused test targets with deterministic fake providers

**Acceptance criteria:** Tests prove new closed connections per factory call, configured factory ownership, gateway non-ownership of the context, session affinity, one session-owned connection, nested commit depth, whole-transaction rollback, reuse after a terminal transaction, idempotent disposal, and cleanup on every terminal path.

**Required evidence level:** automated-runtime

**Rationale:** Connection or transaction ownership mistakes can leak resources, cross contexts, or report a transaction outcome different from the provider outcome.

## DATA-VALREQ-006

**Classification:** REQUIRED

**Trigger:** every change to raw, DTO, dynamic, callback, buffered, or streaming result paths; mapping; conversion; or generated-type controls

**Category:** focused-regression

**Boundary:** in-process

**Targets:** `net481` raw/DTO/dynamic buffered and callback paths; `net9.0` parity plus streaming

**Acceptance criteria:** Tests preserve `RecordItem`'s intentional textual loss, strict and lenient DTO failure behavior, direct provider-value mapping, callback resource lifetime, lazy-stream cleanup, Expando defaults, the process-wide bounded IL cache, schema-cap rejection/fallback, and the absence of streaming from `net481`.

**Required evidence level:** automated-runtime

**Rationale:** The result families make deliberately different fidelity, allocation, lifetime, and target promises that must not collapse into one implicit conversion path.

## DATA-VALREQ-007

**Classification:** REQUIRED

**Trigger:** every change to promotion, decay, loss, schema discovery, write binding, read materialization, adaptation rules, or adaptation evidence

**Category:** security

**Boundary:** database

**Targets:** both focused test targets plus each affected real-provider profile

**Acceptance criteria:** Tests distinguish promotion, provider adaptation/decay, loss authorization, schema discovery, write rules, read materialization, and value-free evidence; custom rules cannot bypass configured promotion, decay, or loss policy; a mutation is never retried with a different representation; SQL, values, credentials, and raw provider errors remain absent from adaptation evidence.

**Required evidence level:** automated-runtime

**Rationale:** Silent conversion or lossy fallback can corrupt persisted values, while diagnostic projection can expose application data unless each policy stage stays explicit.

## DATA-VALREQ-008

**Classification:** REQUIRED

**Trigger:** every change to command timeout, cancellation, synchronous fallback, callbacks, streaming disposal, lifecycle events, or observer-failure retention

**Category:** focused-regression

**Boundary:** in-process

**Targets:** both focused test targets and the `net9.0` streaming paths

**Acceptance criteria:** Tests prove command-policy timeout precedence, cancellation before synchronous fallback, provider limitations after blocking work begins, cleanup before terminal notification, exactly one authoritative terminal event, synchronous ordered observation, subscriber isolation, bounded observer-failure retention, and default SQL/result redaction.

**Required evidence level:** automated-runtime

**Rationale:** A callback or observer must not change an already completed database outcome, and cancellation claims must remain bounded by the provider's actual async support.

## DATA-VALREQ-009

**Classification:** CONDITIONAL

**Trigger:** every release or change affecting SQLite, Access/OleDb, translators, positional binding, schema discovery, sessions, transactions, or provider adaptation

**Category:** runtime

**Boundary:** database

**Targets:** FarmDatabase on Windows x64 for `net481` and `net9.0-windows`, using SQLite and Access/OleDb as applicable

**Acceptance criteria:** The versioned builder procedure passes on both targets and providers with matching semantic results, SQLite named binding, Access positional binding, provider-specific translation, transaction cleanup, schema discovery, and any affected adaptation probes; Access evidence records the installed x64 ACE provider and exact executed subset.

**Required evidence level:** automated-runtime

**Rationale:** Fake providers and translator strings do not prove real SQLite or OleDb behavior, while the Access engine is a machine and bitness prerequisite outside the package.

## DATA-VALREQ-010

**Classification:** CONDITIONAL

**Trigger:** every release or change affecting SQL Server composition, pooling, cancellation, streaming, transactions, schema discovery, adaptation, or failure recovery

**Category:** recovery-soak

**Boundary:** database

**Targets:** the versioned SQL Server scenario on Windows x64 for `net481` and `net9.0`

**Acceptance criteria:** The adopted pinned container procedure records exact engine/provider versions and passes the affected smoke matrix; recovery-sensitive changes also pass the specified-window `net9.0` rehearsal, while `net481` records the intentional streaming absence and its applicable fault subset. Cleanup drops run-owned databases and restores the adopted container's prior state.

**Required evidence level:** automated-runtime

**Rationale:** Real server cancellation, dead transports, stale pools, and recovery timing cannot be established by mocks, but Data must not silently launch or own external infrastructure.

## DATA-VALREQ-011

**Classification:** REQUIRED

**Trigger:** every publishable release candidate and every cross-cutting Data change

**Category:** full-regression

**Boundary:** in-process

**Targets:** the complete coordinated solution on all declared test targets

**Acceptance criteria:** The full solution suite passes with zero failures and zero unexpected skips, and a Release rebuild introduces no new normalized warning identity.

**Required evidence level:** automated-runtime

**Rationale:** Data is dependency-independent but participates in shared consumers, scenarios, documentation, packaging, and compatibility rules that focused tests cannot qualify alone.

## DATA-VALREQ-012

**Classification:** REQUIRED

**Trigger:** every publishable release candidate and every change to package identity, targets, dependencies, repository metadata, or XML delivery

**Category:** package

**Boundary:** package-feed

**Targets:** the `NekoLib.Data` package and both `lib/net481` and `lib/net9.0` assets

**Acceptance criteria:** An immutable package from a clean recorded commit contains the expected assembly, matching XML documentation, repository metadata, and no undeclared provider or NekoLib dependency for both targets; artifact hashes and the exact package version are retained.

**Required evidence level:** build-only

**Rationale:** Project builds cannot prove the NuGet asset graph, documentation content, dependency metadata, or immutable provenance.

## DATA-VALREQ-013

**Classification:** REQUIRED

**Trigger:** every publishable release candidate and every package, dependency, target, or XML-delivery change

**Category:** package-consumer

**Boundary:** package-feed

**Targets:** isolated PackageReference-only consumers for `net481` and `net9.0`

**Acceptance criteria:** Consumers restore only from the candidate feed, compile representative Data composition on both targets without repository project references, resolve the correct target surface, and receive the package-owned XML file beside the selected assembly.

**Required evidence level:** build-only

**Rationale:** Repository project references can hide packaging, dependency, target selection, and XML-delivery failures.

## DATA-VALREQ-014

**Classification:** REQUIRED

**Trigger:** every publishable release candidate and every change to public XML comments or documentation-generation settings

**Category:** build

**Boundary:** in-process

**Targets:** both Data target assemblies

**Acceptance criteria:** Documentation-enabled Release builds generate the matching XML asset with zero missing-member, malformed, unresolved, or ambiguous XML-comment diagnostic; every accepted public member has an effective contract and intentional inheritance resolves to documented source.

**Required evidence level:** build-only

**Rationale:** Accepted API manifests prove compiled shape but do not prove IDE guidance, target-specific documentation, or valid generated XML.

### Evidence exclusions

The tracked `tests/NekoLib.Data.Tests/Shared/Pods.db` and `PodsDB` fixtures do not
satisfy any database-runtime requirement: current tests do not reference either
fixture. They become evidence only after an explicit test or versioned scenario
wires, executes, and verifies them.
