# NekoLib.Telemetry Validation Requirements

**Document ID:** TEL-VALIDATION-REQUIREMENTS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** evidence contract for qualifying the NekoLib.Telemetry boundary

**Surface:** validation-requirements

**Boundary:** telemetry

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

The [module manifest](MANIFEST.md) owns the inherited profile list. The
requirements below specialize those profiles for caller-owned operation
lifetimes with exactly one terminal, mixed wall-clock and monotonic time,
caller-owned evidence values, bounded in-memory retention, and synchronous
inline sink dispatch with no flush and no disposal.

Evidence produced by the shared Observability scenario qualifies a Telemetry
requirement only through that scenario's Telemetry checks. Its Logging and
Inspection checks are not Telemetry evidence.

## TEL-VALREQ-001

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to source, project, target, or package settings

**Category:** build

**Boundary:** in-process

**Targets:** `net481` and `net9.0`

**Acceptance criteria:** Both target assemblies build with zero errors and no new normalized warning identity.

**Required evidence level:** build-only

**Rationale:** The project carries no conditional compilation, so a target-specific break would come from the SDK or Core rather than from a visible branch in Telemetry source.

## TEL-VALREQ-002

**Classification:** REQUIRED

**Trigger:** every release candidate and every public declaration, target, nullable, or default-value change

**Category:** api-compatibility

**Boundary:** in-process

**Targets:** both accepted `NekoLib.Telemetry` manifests

**Acceptance criteria:** Release assemblies match both accepted manifests exactly; any delta carries an explicit compatibility disposition and a migration entry before acceptance.

**Required evidence level:** build-only

**Rationale:** The surface is two sealed types and five members, so a single accidental addition is a disproportionately large fraction of it and must be caught by comparison rather than review.

## TEL-VALREQ-003

**Classification:** REQUIRED

**Trigger:** every change to operation creation, identity, or terminal handling, and every release candidate

**Category:** focused-regression

**Boundary:** in-process

**Targets:** focused suite on `net481` and `net9.0`

**Acceptance criteria:** The suite proves that blank module or name throws, that a blank operation identifier is replaced by a generated 32-character value and an explicit one is preserved, that a blank parent is normalized to null while an explicit one is preserved, that first completion wins and later completions are ignored, that a checkpoint after completion is ignored and returns the final duration, and that an operation which is never completed produces no record, no sink write, and no error.

**Required evidence level:** automated-runtime

**Rationale:** The caller owns the single terminal and abandonment is invisible by design, so every one of these is a silent failure mode rather than an exception a consumer would notice.

## TEL-VALREQ-004

**Classification:** REQUIRED

**Trigger:** every change to dimension or measurement copying, merging, or validation

**Category:** focused-regression

**Boundary:** in-process

**Targets:** both focused test targets

**Acceptance criteria:** The suite proves ordinal case-sensitive keys, initial dimensions copied at start, terminal dimensions merged at completion with the terminal value winning a collision and initial-only keys surviving, a duplicate-enumerating dictionary keeping its last value without throwing, permissive numeric recording of `NaN`, infinities and negatives, and that a malformed dimension or measurement payload surfaces its exception while leaving the operation completable so a corrected retry still records.

**Required evidence level:** automated-runtime

**Rationale:** The malformed-payload path is the one place where a caller error previously destroyed the record it was trying to annotate; the merge and comparison rules are otherwise invisible until a consumer reads a wrong value.

## TEL-VALREQ-005

**Classification:** REQUIRED

**Trigger:** every change to retention, snapshot bounds, or the retention-versus-dispatch ordering

**Category:** focused-regression

**Boundary:** in-process

**Targets:** both focused test targets

**Acceptance criteria:** The suite proves that only completed operations are retained, that the newest window is returned in completion order bounded by both the request and the configured capacity, that non-positive and oversized limits stay bounded, that the result is a detached collection whose models do not change afterwards, and that retention completes before sink dispatch under a separate lock so a snapshot is not blocked by a slow sink.

**Required evidence level:** automated-runtime

**Rationale:** The separate retention lock is what lets the crash reporter collect telemetry under a contributor budget; if retention ever moved behind the dispatch gate, a slow sink would silently starve incident evidence.

## TEL-VALREQ-006

**Classification:** REQUIRED

**Trigger:** every change to locking, dispatch order, or failure isolation

**Category:** focused-regression

**Boundary:** in-process

**Targets:** both focused test targets

**Acceptance criteria:** The suite proves that concurrent terminals on one operation record exactly one, that concurrent writers give every sink one identical order which is also the retained order, that a throwing sink is isolated and later sinks still receive the operation, and that a caller mutating its own sink array after construction cannot re-target dispatch.

**Required evidence level:** automated-runtime

**Rationale:** Ordering and exactly-once delivery are the pipeline's core guarantees and are only observable under real concurrency.

## TEL-VALREQ-007

**Classification:** REQUIRED

**Trigger:** every change to time capture, duration measurement, or the retained model

**Category:** focused-regression

**Boundary:** in-process

**Targets:** both focused test targets

**Acceptance criteria:** Evidence proves that checkpoint elapsed values are monotonically non-decreasing within an operation, that the start timestamp is a single wall-clock reading, and that the documentation states plainly that the two clocks are not interchangeable and that no completion timestamp is retained.

**Required evidence level:** automated-runtime

**Rationale:** Mixing a wall-clock start with a monotonic duration is a correctness trap for any consumer computing a completion time, and a clock adjustment makes the error invisible in normal operation.

## TEL-VALREQ-008

**Classification:** REQUIRED

**Trigger:** every change to dispatch, locking, retention, or sink failure handling

**Category:** soak

**Boundary:** in-process

**Targets:** a sustained scenario run on each supported target

**Acceptance criteria:** A sustained run holds bounded retention exactly at capacity under continuous completion, isolates an injected throwing sink so the pipeline keeps recording and the next operation records normally, confirms that abandoned operations accumulate no retained state, and ends with threads and handles inside a bounded allowance and a non-monotonic managed heap. Duration, workload, concurrency, operations, faults, expected and actual recovery, resource measurements, acceptance criteria, and cleanup are recorded, and no unmeasured threshold is invented.

**Required evidence level:** automated-runtime

**Rationale:** The pipeline retains caller objects by shallow reference in a bounded queue and has no disposal step, so retention and abandonment behavior over time cannot be inferred from short tests.

## TEL-VALREQ-009

**Classification:** REQUIRED

**Trigger:** every change to dimension or measurement retention, copying, or exposure

**Category:** security

**Boundary:** in-process

**Targets:** both focused test targets and the current technical reference

**Acceptance criteria:** Evidence and documentation state that dimension values are retained as shallow references, that the pipeline performs no deep clone, serialization, validation, or redaction, that a mutable value can therefore change after recording, and that whoever persists or transmits a snapshot owns redaction and access control.

**Required evidence level:** build-only

**Rationale:** Telemetry accepts arbitrary `object` values from application code and hands them to sinks and snapshots unchanged; treating the copy boundary as a confidentiality boundary would be a false assurance.

## TEL-VALREQ-010

**Classification:** REQUIRED

**Trigger:** every publishable release candidate and every package, dependency, target, or XML-delivery change

**Category:** package

**Boundary:** package-feed

**Targets:** the `NekoLib.Telemetry` package and both target assets

**Acceptance criteria:** The immutable package contains both target assemblies, a package-owned matching XML file per assembly, correct target groups, the `NekoLib.Core` dependency at the aligned version, provenance metadata, and a recorded hash, with no unexpected repository assets.

**Required evidence level:** build-only

**Rationale:** Project output proves neither package contents nor dependency groups; a managed package once shipped without its XML files and only direct inspection caught it.

## TEL-VALREQ-011

**Classification:** REQUIRED

**Trigger:** every publishable release candidate and every package, dependency, target, or XML-delivery change

**Category:** package-consumer

**Boundary:** package-feed

**Targets:** isolated package-reference-only consumers on both supported targets

**Acceptance criteria:** Clean isolated consumers restore and build without repository project references and extract the expected assembly and XML pairs from the package cache.

**Required evidence level:** build-only

**Rationale:** A project-reference build cannot prove target asset selection or real package consumption.

## TEL-VALREQ-012

**Classification:** REQUIRED

**Trigger:** every publishable release candidate and every public XML comment or documentation-generation change

**Category:** build

**Boundary:** in-process

**Targets:** both target assemblies

**Acceptance criteria:** Documentation-enabled Release builds generate matching XML assets with zero missing-member, malformed, unresolved, or ambiguous XML-comment diagnostics. Because `CS1591` is suppressed on `net481` by the project's `NoWarn`, a qualifying run either measures the `net9.0` target where it is live or unsuppresses it explicitly.

**Required evidence level:** build-only

**Rationale:** The accepted manifests prove compiled shape but say nothing about IDE guidance, and an ordinary `net481` build cannot detect a missing member at all.

## TEL-VALREQ-013

**Classification:** CONDITIONAL

**Trigger:** a producer that records checkpoints inside a loop or on an unbounded path, or any change to checkpoint retention

**Category:** performance

**Boundary:** in-process

**Targets:** a representative long-running consumer on each affected target

**Acceptance criteria:** Measured memory for a bounded window holding operations with large checkpoint counts, so the relationship between `RecentOperationCapacity` and actual retained memory is recorded rather than assumed, and any accepted cap is derived from that measurement.

**Required evidence level:** automated-runtime

**Rationale:** Checkpoints are unbounded per operation, so the capacity bound describes a count of operations and not the memory the window holds. See [`TEL-FINDING-001`](FINDINGS.md).

## TEL-VALREQ-014

**Classification:** RECOMMENDED

**Trigger:** any change to the dispatch lock, reentrancy behavior, or sink invocation

**Category:** focused-regression

**Boundary:** in-process

**Targets:** both focused test targets

**Acceptance criteria:** A bounded characterization of what happens when a sink produces telemetry on the pipeline dispatching to it, recorded without asserting an uncatchable process termination.

**Required evidence level:** automated-runtime

**Rationale:** The reentrant gate turns a misbehaving sink into unbounded recursion rather than an isolated fault, and that consequence currently rests only on reading the code. It stays below REQUIRED because a stack overflow cannot be safely asserted. See [`TEL-FINDING-003`](FINDINGS.md).

## TEL-VALREQ-015

**Classification:** REQUIRED

**Trigger:** every coordinated family release candidate

**Category:** full-regression

**Boundary:** in-process

**Targets:** `NekoLib.sln`

**Acceptance criteria:** The canonical full build, test, and package campaign passes and preserves the repository warning baseline, dependency graph, public APIs, packages, consumers, and unrelated module regressions.

**Required evidence level:** automated-runtime

**Rationale:** Navigation produces telemetry on its lifecycle path and Diagnostics consumes telemetry snapshots through Core contracts, so a Telemetry change can fail outside its own suite.
