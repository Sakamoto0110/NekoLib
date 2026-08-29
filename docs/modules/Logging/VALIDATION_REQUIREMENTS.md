# NekoLib.Logging Validation Requirements

**Document ID:** LOG-VALIDATION-REQUIREMENTS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** evidence contract for qualifying the NekoLib.Logging boundary

**Surface:** validation-requirements

**Boundary:** logging

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

The [module manifest](MANIFEST.md) owns the inherited profile list. The
requirements below specialize those profiles for a synchronous ordered
pipeline, caller-owned sink composition, a bounded flush that never cancels,
terminal disposal, and filesystem persistence with rotation and retention.

Evidence produced by the shared Observability scenario qualifies a Logging
requirement only through that scenario's Logging checks. Its Telemetry and
Inspection checks are not Logging evidence.

## LOG-VALREQ-001

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to source, project, target, or package settings

**Category:** build

**Boundary:** in-process

**Targets:** `net481` and `net9.0`

**Acceptance criteria:** Both target assemblies build with zero errors and no new normalized warning identity.

**Required evidence level:** build-only

**Rationale:** The project carries no conditional compilation, so a target-specific break would come from the SDK, the `DefineTrace` property, or Core rather than from a visible branch in Logging source.

## LOG-VALREQ-002

**Classification:** REQUIRED

**Trigger:** every release candidate and every public declaration, target, nullable, or default-value change

**Category:** api-compatibility

**Boundary:** in-process

**Targets:** both accepted `NekoLib.Logging` manifests

**Acceptance criteria:** Release assemblies match both accepted manifests exactly; any delta carries an explicit compatibility disposition and a migration entry before acceptance.

**Required evidence level:** build-only

**Rationale:** The accepted `net481` and `net9.0` manifests are identical, so a divergence introduced on one target would be invisible to source review.

## LOG-VALREQ-003

**Classification:** REQUIRED

**Trigger:** every implementation or contract change and every release candidate

**Category:** focused-regression

**Boundary:** in-process

**Targets:** focused suite on `net481` and `net9.0`

**Acceptance criteria:** The complete Logging suite passes with zero failed or skipped tests and preserves severity admission below the minimum level, registration-order fan-out, exactly-once delivery, write-failure isolation, construction-time options and sink-array capture, null-element and null-array tolerance, and the documented option defaults.

**Required evidence level:** automated-runtime

**Rationale:** Ordering, isolation, and construction-time capture are the core delivery guarantees, and none of them is observable from compilation or from the compiled API surface.

## LOG-VALREQ-004

**Classification:** REQUIRED

**Trigger:** every change to flush admission, budget accounting, abandonment, or the disposal gate

**Category:** focused-regression

**Boundary:** in-process

**Targets:** both focused test targets

**Acceptance criteria:** Automated tests prove that a thrown sink failure does not stop later admission, that budget exhaustion does, that a negative budget including `Timeout.InfiniteTimeSpan` throws, that `Flush` returns true after completed disposal without touching sinks, that a flush racing an in-progress disposal returns false when its budget expires, and that an abandoned sink fault is observed rather than surfacing through `TaskScheduler.UnobservedTaskException`.

**Required evidence level:** automated-runtime

**Rationale:** This is the pipeline's only bounded operation and its only interaction with the crash reporter; a sub-millisecond admission error already produced `LOG-ISSUE-001` here once.

## LOG-VALREQ-005

**Classification:** REQUIRED

**Trigger:** every change to disposal, sink ownership, or the terminal flush

**Category:** focused-regression

**Boundary:** in-process

**Targets:** both focused test targets

**Acceptance criteria:** Automated tests prove that disposal flushes then disposes when sink disposal is enabled, that borrowed sinks are still flushed but never disposed, that disposal is idempotent and never throws, that sink failures during the terminal flush and dispose are isolated, that emission is inert afterwards, and that the recent snapshot still returns retained entries.

**Required evidence level:** automated-runtime

**Rationale:** Sink disposal ownership transfers to the logger by default, so a regression here silently double-disposes or leaks a sink shared between two loggers.

## LOG-VALREQ-006

**Classification:** REQUIRED

**Trigger:** every change to rotation, retention, archive naming, byte accounting, encoding, durability, or path normalization

**Category:** runtime

**Boundary:** filesystem

**Targets:** both focused test targets

**Acceptance criteria:** Temporary-filesystem tests prove pre-write rotation against the encoded byte count, that an empty file is never rotated, exact archive naming and eviction for the default and minimum retained counts, construction-time path normalization and rejection of blank paths and sub-minimum bounds, and that a write to an unusable path surfaces as a sink failure the pipeline absorbs.

**Required evidence level:** automated-runtime

**Rationale:** Rotation is the only place where Logging deletes and renames consumer data, and neither bound can be switched off.

## LOG-VALREQ-007

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to the debug sink, the trace-definition property, or the build configuration used for validation

**Category:** focused-regression

**Boundary:** in-process

**Targets:** both focused test targets, executed in Release

**Acceptance criteria:** A Release-configuration test observes real output from `DebugLogSink` through a trace listener, and a null entry throws `ArgumentNullException`.

**Required evidence level:** automated-runtime

**Rationale:** The shipped defect this replaces was invisible in Debug: a conditional call is removed only from the Release assembly that actually ships, so a Debug-only suite would pass against a sink that discards everything.

## LOG-VALREQ-008

**Classification:** REQUIRED

**Trigger:** every change to dispatch, locking, retention, or sink failure handling

**Category:** soak

**Boundary:** filesystem

**Targets:** a sustained scenario run on each supported target

**Acceptance criteria:** A sustained run under concurrent writers holds one identical delivery order across sinks, keeps the recent window exactly at capacity, isolates an injected throwing sink so every entry offered to it still reaches the healthy one, absorbs a locked file without affecting other sinks, returns false from a blocked flush inside its bound and true once released, and ends with threads and handles inside a bounded allowance and a non-monotonic managed heap. Duration, workload, concurrency, faults, expected and actual recovery, resource measurements, acceptance criteria, and cleanup are recorded, and no unmeasured threshold is invented.

**Required evidence level:** automated-runtime

**Rationale:** Synchronous dispatch under a process-wide lock is exactly the design where ordering, backpressure, and handle retention drift only over time.

## LOG-VALREQ-009

**Classification:** REQUIRED

**Trigger:** every change to entry formatting, retention, or persistence

**Category:** security

**Boundary:** filesystem

**Targets:** both focused test targets and the current technical reference

**Acceptance criteria:** Evidence and documentation state that the pipeline applies no redaction or truncation, that the message, category, and retained exception reach sinks and the recent window verbatim, and that a persisting or transmitting sink owns redaction, truncation, access control, and retention.

**Required evidence level:** build-only

**Rationale:** Logging is the easiest place in the family to mistake a formatting boundary for a confidentiality boundary; the crash reporter's redactor does not apply to it.

## LOG-VALREQ-010

**Classification:** REQUIRED

**Trigger:** every publishable release candidate and every package, dependency, target, or XML-delivery change

**Category:** package

**Boundary:** package-feed

**Targets:** the `NekoLib.Logging` package and both target assets

**Acceptance criteria:** The immutable package contains both target assemblies, a package-owned matching XML file per assembly, correct target groups, the `NekoLib.Core` dependency at the aligned version, provenance metadata, and a recorded hash, with no unexpected repository assets.

**Required evidence level:** build-only

**Rationale:** Project output proves neither package contents nor dependency groups; a managed package once shipped without its XML files and only direct inspection caught it.

## LOG-VALREQ-011

**Classification:** REQUIRED

**Trigger:** every publishable release candidate and every package, dependency, target, or XML-delivery change

**Category:** package-consumer

**Boundary:** package-feed

**Targets:** isolated package-reference-only consumers on both supported targets

**Acceptance criteria:** Clean isolated consumers restore and build without repository project references and extract the expected assembly and XML pairs from the package cache.

**Required evidence level:** build-only

**Rationale:** A project-reference build cannot prove target asset selection or real package consumption.

## LOG-VALREQ-012

**Classification:** REQUIRED

**Trigger:** every publishable release candidate and every public XML comment or documentation-generation change

**Category:** build

**Boundary:** in-process

**Targets:** both target assemblies

**Acceptance criteria:** Documentation-enabled Release builds generate matching XML assets with zero missing-member, malformed, unresolved, or ambiguous XML-comment diagnostics.

**Required evidence level:** build-only

**Rationale:** The accepted manifests prove compiled shape but say nothing about IDE guidance or cross-target documentation parity.

## LOG-VALREQ-013

**Classification:** CONDITIONAL

**Trigger:** a change to the file share mode, rotation, or path gating, or an accepted consumer requirement for more than one process writing one log path

**Category:** runtime

**Boundary:** filesystem

**Targets:** two real processes on each supported target family

**Acceptance criteria:** Two processes targeting one normalized path demonstrate which writer succeeds, that the losing writer's failure is absorbed with no error surface, and that rotation performed by the owner neither corrupts nor truncates the archives; the observed behavior is compared against the documented single-writer rule.

**Required evidence level:** automated-runtime

**Rationale:** The single-writer rule is currently derived from the share mode in source only, and its failure mode is silent total log loss for the losing process. See [`LOG-FINDING-003`](FINDINGS.md).

## LOG-VALREQ-014

**Classification:** RECOMMENDED

**Trigger:** a change to disposal locking, terminal-flush behavior, or the snapshot read path

**Category:** focused-regression

**Boundary:** in-process

**Targets:** both focused test targets

**Acceptance criteria:** A test characterizes how long the recent snapshot blocks while a slow sink holds the unbounded terminal flush, so the crash-time consequence is measured rather than reasoned about.

**Required evidence level:** automated-runtime

**Rationale:** [`LOG-FINDING-001`](FINDINGS.md) identifies a real interaction with the incident collector's per-contributor budget whose width has never been measured. It stays below REQUIRED because no defect has been reproduced.

## LOG-VALREQ-015

**Classification:** REQUIRED

**Trigger:** every coordinated family release candidate

**Category:** full-regression

**Boundary:** in-process

**Targets:** `NekoLib.sln`

**Acceptance criteria:** The canonical full build, test, and package campaign passes and preserves the repository warning baseline, dependency graph, public APIs, packages, consumers, and unrelated module regressions.

**Required evidence level:** automated-runtime

**Rationale:** Focused Logging evidence cannot establish compatibility with the coordinated family graph, and Diagnostics consumes Logging through Core contracts.
