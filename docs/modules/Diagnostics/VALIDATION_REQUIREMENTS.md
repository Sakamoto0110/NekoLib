# NekoLib.Diagnostics Validation Requirements

**Document ID:** DIAG-VALIDATION-REQUIREMENTS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** evidence contract for qualifying the NekoLib.Diagnostics family boundary

**Surface:** validation-requirements

**Boundary:** diagnostics

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

The [module manifest](MANIFEST.md) owns the inherited profile list. The
requirements below specialize those profiles for process-wide exception hooks,
bounded contributors, filesystem evidence, and the Windows native adapter.

## DIAG-VALREQ-001

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to source, project, target, or package settings

**Category:** build

**Boundary:** in-process

**Targets:** Diagnostics on `net481` and `net9.0`; Diagnostics.Windows on `net481` and `net9.0-windows`

**Acceptance criteria:** All four target assemblies build with zero errors and no new normalized warning identity.

**Required evidence level:** build-only

**Rationale:** Conditional compilation, desktop references, nullable differences, and native interop can diverge independently across the family.

## DIAG-VALREQ-002

**Classification:** REQUIRED

**Trigger:** every release candidate and every public/protected declaration, target, nullable, default-value, or package-boundary change

**Category:** api-compatibility

**Boundary:** in-process

**Targets:** all four accepted Diagnostics API manifests

**Acceptance criteria:** Release assemblies match every accepted manifest; any delta has an explicit compatibility disposition and migration entry before acceptance.

**Required evidence level:** build-only

**Rationale:** Source review does not prove compiled metadata parity across both target families.

## DIAG-VALREQ-003

**Classification:** REQUIRED

**Trigger:** every implementation or contract change and every release candidate

**Category:** focused-regression

**Boundary:** in-process

**Targets:** focused suite on `net481` and `net9.0-windows`

**Acceptance criteria:** The complete Diagnostics suite passes with zero failed or skipped tests and preserves construction-time capture, lifecycle, registry ordering, exception isolation, contributor bounds, bundle outcomes, redaction, and Windows facade contracts.

**Required evidence level:** automated-runtime

**Rationale:** Process-global hooks, concurrency, callbacks, timeouts, and filesystem effects cannot be qualified by compilation alone.

## DIAG-VALREQ-004

**Classification:** REQUIRED

**Trigger:** every change to bundle composition, tail handling, crash-folder writing, failure reporting, or filesystem naming

**Category:** runtime

**Boundary:** filesystem

**Targets:** both focused test targets

**Acceptance criteria:** Temporary-filesystem tests prove mandatory `crash.txt`, conditional dump authority, tail disambiguation, partial-bundle survival, and exactly one written/failed terminal event when folder writing is enabled.

**Required evidence level:** automated-runtime

**Rationale:** A successful call path does not by itself establish durable artifact layout or failure observability.

## DIAG-VALREQ-005

**Classification:** REQUIRED

**Trigger:** every change to install, dispose, registry, process hooks, reentrancy, concurrency, or terminating-report behavior

**Category:** focused-regression

**Boundary:** process

**Targets:** both focused test targets

**Acceptance criteria:** Automated tests prove idempotent install, terminal disposal, race serialization, first/last hook transitions, registration-order fan-out, no observation without an accepting handler, concurrent-report de-duplication, and permanent terminating latch behavior.

**Required evidence level:** automated-runtime

**Rationale:** A stale shared hook or revived handler changes process exception semantics beyond one object lifetime.

## DIAG-VALREQ-006

**Classification:** REQUIRED

**Trigger:** every change to contributors, budgets, dump writers, extra lines, or outcome ordering

**Category:** focused-regression

**Boundary:** process

**Targets:** both focused test targets

**Acceptance criteria:** Automated tests prove per-contributor cooperative budgets plus settle margin, exception isolation, abandonment without cancellation, local caps, record-level formatting isolation, continued partial evidence, and event-time authority when a writer completes late.

**Required evidence level:** automated-runtime

**Rationale:** Crash-path extensions run while application state may be failing and must not suppress the remaining evidence.

## DIAG-VALREQ-007

**Classification:** REQUIRED

**Trigger:** every change to redaction, dynamic evidence formatting, tails, exception persistence, or line truncation

**Category:** security

**Boundary:** filesystem

**Targets:** both focused test targets

**Acceptance criteria:** Automated tests prove batch and line redaction, fail-closed timeout/failure behavior, handler-lifetime redactor latching, line-length enforcement, and the explicit separation between persisted text, raw in-process callbacks, logger input, and unredacted dump bytes.

**Required evidence level:** automated-runtime

**Rationale:** Treating the persistence filter as a process-wide secrecy barrier would create false assurance around raw exceptions and native memory.

## DIAG-VALREQ-008

**Classification:** REQUIRED

**Trigger:** every publishable release candidate and every package, dependency, target, or XML-delivery change

**Category:** package

**Boundary:** package-feed

**Targets:** both Diagnostics packages and all supported target assets

**Acceptance criteria:** Immutable packages contain the expected assemblies, package-owned matching XML files, correct target groups, the Diagnostics-to-Core dependency, the Diagnostics.Windows-to-Diagnostics dependency, provenance metadata, and recorded hashes without unexpected repository assets.

**Required evidence level:** build-only

**Rationale:** Project outputs do not prove package contents, dependency groups, target selection, provenance, or immutability.

## DIAG-VALREQ-009

**Classification:** REQUIRED

**Trigger:** every publishable release candidate and every package, dependency, target, or XML-delivery change

**Category:** package-consumer

**Boundary:** package-feed

**Targets:** both Diagnostics packages and all supported target assets

**Acceptance criteria:** Immutable packages contain expected assemblies and matching XML files; isolated PackageReference-only consumers restore and build on supported targets without repository project references.

**Required evidence level:** build-only

**Rationale:** Project-reference builds cannot prove dependency groups, target asset selection, XML delivery, or real package consumption.

## DIAG-VALREQ-010

**Classification:** REQUIRED

**Trigger:** every publishable release candidate and every public/protected XML comment or documentation-generation change

**Category:** build

**Boundary:** in-process

**Targets:** all four target assemblies

**Acceptance criteria:** Documentation-enabled Release builds generate matching XML assets with zero missing-member, malformed, unresolved, or ambiguous XML-comment diagnostics.

**Required evidence level:** build-only

**Rationale:** API manifests prove shape but not IDE guidance or cross-target documentation parity.

## DIAG-VALREQ-011

**Classification:** REQUIRED

**Trigger:** every stable release and every change to native dump implementation or dump-level mapping

**Category:** runtime

**Boundary:** process

**Targets:** Diagnostics.Windows on each supported Windows target family

**Acceptance criteria:** A disposable child process produces and opens a real minidump for every affected level; `None`, native failure cleanup, invalid-level fallback, sensitive-memory implications, and event-time `DumpWritten` are recorded.

**Required evidence level:** interactive

**Rationale:** Delegate tests and API probes do not execute `MiniDumpWriteDump` or prove the resulting native artifact is usable.

## DIAG-VALREQ-012

**Classification:** REQUIRED

**Trigger:** every stable release and every change to `HookWinForms` or `CrashSuppressor`

**Category:** interactive-ui

**Boundary:** native-ui

**Targets:** WinForms `net481` and `net9.0-windows`

**Acceptance criteria:** A real message-loop scenario verifies early and late hook behavior, one dispatch after repeated calls, non-terminating continuation, no interactive WER/error dialog after suppression, and normal process exit after controlled cleanup.

**Required evidence level:** interactive

**Rationale:** Process error mode, WinForms exception mode, native dialog behavior, and message-loop dispatch are not established by headless tests.

## DIAG-VALREQ-013

**Classification:** CONDITIONAL

**Trigger:** an accepted application or product requirement for crash-evidence secrecy, upload, retention, deletion, or access governance

**Category:** security

**Boundary:** deployment

**Targets:** the consuming application and its actual storage/transport environment

**Acceptance criteria:** The consumer records threat model, ACLs, encryption, retention/deletion, dump access, redaction coverage, transport, failure handling, and operator procedure without claiming Diagnostics enforces policies it does not own.

**Required evidence level:** interactive

**Rationale:** Crash bundles can contain sensitive text and process memory, but the library intentionally does not own deployment policy.

## DIAG-VALREQ-014

**Classification:** CONDITIONAL

**Trigger:** a change to repeated incident handling, contributor lifetime, hook churn, filesystem allocation, or evidence volume

**Category:** recovery-soak

**Boundary:** process

**Targets:** representative long-running consumer on each affected target family

**Acceptance criteria:** Deterministic repeated faults and planned contributor failures complete without unexpected process-wide hook, thread, handle, disk, or bundle-collision growth; duration and measurements are recorded without inventing an unmeasured threshold.

**Required evidence level:** automated-runtime

**Rationale:** Short tests cannot classify slow retention, abandoned-worker accumulation, or rare timestamp collisions.

## DIAG-VALREQ-015

**Classification:** REQUIRED

**Trigger:** every coordinated family release candidate

**Category:** full-regression

**Boundary:** in-process

**Targets:** `NekoLib.sln`

**Acceptance criteria:** The canonical full build/test/package campaign passes and preserves the repository warning baseline, dependency graph, public APIs, packages, consumers, and unrelated module regressions.

**Required evidence level:** automated-runtime

**Rationale:** Focused evidence cannot establish compatibility with the coordinated family graph or package campaign.
