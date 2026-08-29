# NekoLib.Core Validation Requirements

**Document ID:** CORE-VALIDATION-REQUIREMENTS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** evidence contract for the NekoLib.Core boundary

**Surface:** validation-requirements

**Boundary:** core

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

The inherited profile is owned by [`MANIFEST.md`](MANIFEST.md). These
requirements are derived from Core's dual-target stable API, zero-dependency
package boundary, caller-owned service lifetimes, explicit operation and
registration lifecycles, outer-snapshot guarantees, callback contracts,
process-wide Inspection slot, experimental marker, and raw evidence values.

## CORE-VALREQ-001

**Classification:** REQUIRED

**Trigger:** every Core source or build configuration change

**Category:** build

**Boundary:** in-process

**Targets:** `net481`, `net9.0`

**Acceptance criteria:** `NekoLib.Core.csproj` builds both targets with zero errors and preserves nullable enablement, the target pair, assembly/package identity, and one shared source surface.

**Required evidence level:** build-only

**Rationale:** Core is the transitive contract foundation for six shipped capability families; a target-specific compile break blocks every dependent package on that family.

## CORE-VALREQ-002

**Classification:** REQUIRED

**Trigger:** every Core source change and every release candidate

**Category:** focused-regression

**Boundary:** in-process

**Targets:** `net481`, `net9.0`

**Acceptance criteria:** the complete `NekoLib.Core.Tests.Unit` suite passes on both targets with zero failures and zero skips.

**Required evidence level:** automated-runtime

**Rationale:** compilation cannot establish constructor normalization, collection protection, singleton behavior, delegate non-evaluation, provider ownership, or warning-marker semantics.

## CORE-VALREQ-003

**Classification:** REQUIRED

**Trigger:** every Core source, project, or public XML-attribute change

**Category:** api-compatibility

**Boundary:** in-process

**Targets:** `net481`, `net9.0`

**Acceptance criteria:** `verify-public-api.ps1 -PackageId NekoLib.Core` matches both accepted manifests without updating either baseline; the two target surfaces remain identical unless a separately accepted target-specific contract says otherwise.

**Required evidence level:** build-only

**Rationale:** nullable annotations, optional defaults, enum values, type shapes, interface inheritance, and `NEKOEXP0001` are compatibility-significant even when method bodies do not change.

## CORE-VALREQ-004

**Classification:** REQUIRED

**Trigger:** every Core project, dependency, solution, or packaging change

**Category:** build

**Boundary:** in-process

**Targets:** `net481`, `net9.0`

**Acceptance criteria:** the project remains in `NekoLib.sln`, declares no authored `PackageReference` or `ProjectReference`, and packs only as the `NekoLib.Core` shipped library for the two supported targets.

**Required evidence level:** build-only

**Rationale:** a dependency or topology drift would change the foundation's architecture and transitive consumer graph even if the public API text stayed equal.

## CORE-VALREQ-005

**Classification:** REQUIRED

**Trigger:** every public symbol or XML documentation change

**Category:** build

**Boundary:** filesystem

**Targets:** `net481`, `net9.0`

**Acceptance criteria:** documentation-enabled Release builds emit one non-empty `NekoLib.Core.xml` beside each target assembly with no `CS1591`, malformed XML-comment, unresolved reference, or blank-member entry; both files document the same public surface.

**Required evidence level:** build-only

**Rationale:** Core is primarily a consumer contract package, so IntelliSense documentation is part of usable package evidence even though it does not alter the compiled API.

## CORE-VALREQ-006

**Classification:** CONDITIONAL

**Trigger:** every immutable package candidate or public release that contains Core changes

**Category:** package

**Boundary:** package-feed

**Targets:** `net481`, `net9.0`

**Acceptance criteria:** the canonical clean-tree pack flow produces an immutable `NekoLib.Core` package with both assemblies, matching sibling XML files, exact source provenance, accepted API surfaces, and recorded hashes without overwriting an existing version.

**Required evidence level:** build-only

**Rationale:** source and build evidence do not prove NuGet layout, XML delivery, provenance, or the bits a consumer restores.

## CORE-VALREQ-007

**Classification:** CONDITIONAL

**Trigger:** every immutable package candidate or public release that changes a Core public contract or its packaged XML guidance

**Category:** package-consumer

**Boundary:** package-feed

**Targets:** `net481`, `net9.0`

**Acceptance criteria:** isolated PackageReference-only consumers restore, compile, and run against both target families; representative Core sinks, snapshot sources, models, null objects, and the experimental warning policy compile from package assets rather than project outputs.

**Required evidence level:** automated-runtime

**Rationale:** Core is normally transitive, and repository project references cannot prove package reachability, XML extraction, nullable annotations, or extension-contract usability.

## CORE-VALREQ-008

**Classification:** REQUIRED

**Trigger:** every change to `TelemetryCheckpoint`, `TelemetryOperation`, `InspectionSnapshot`, `LogEntry`, or `InspectionOperation`

**Category:** focused-regression

**Boundary:** in-process

**Targets:** `net481`, `net9.0`

**Acceptance criteria:** source collection mutations cannot alter published outer collections; exposed wrappers reject mutation; exception, checkpoint, operation, payload, and state objects remain the documented shallow references; required-null and normalization behavior remains exact.

**Required evidence level:** automated-runtime

**Rationale:** the distinction between structural protection and deep immutability is a stable consumer contract and was the principal F1 Core correction.

## CORE-VALREQ-009

**Classification:** REQUIRED

**Trigger:** every change to a Core null object, `Disposable.Empty`, or ignored delegate path

**Category:** focused-regression

**Boundary:** in-process

**Targets:** `net481`, `net9.0`

**Acceptance criteria:** null singletons remain shared and non-throwing for valid calls, expose empty reads and completed defaults as documented, never evaluate ignored payload/provider/action delegates, and return idempotent no-op handles where specified.

**Required evidence level:** automated-runtime

**Rationale:** optional capabilities depend on disabled behavior being safe and free of application side effects.

## CORE-VALREQ-010

**Classification:** REQUIRED

**Trigger:** every change to `InspectionProvider` or its registration handle

**Category:** focused-regression

**Boundary:** in-process

**Targets:** `net481`, `net9.0`

**Acceptance criteria:** concurrent installation admits at most one enabled recorder, rejects invalid owners, rolls back a recorder that disables during installation, publishes a non-null current value, conditionally restores the null recorder, and never disposes the installed recorder.

**Required evidence level:** automated-runtime

**Rationale:** the slot is process-wide mutable state crossing package boundaries; ownership and race behavior cannot be inferred from its small API.

## CORE-VALREQ-011

**Classification:** CONDITIONAL

**Trigger:** any change to logging, telemetry, or Inspection callback, timeout, ordering, concurrency, or failure semantics

**Category:** full-regression

**Boundary:** in-process

**Targets:** every supported target of Core and its affected dependent packages

**Acceptance criteria:** affected concrete Logging, Telemetry, Inspection, Diagnostics, Navigation, and Watchdog suites pass, and their references remain consistent with the intentionally narrow Core contract rather than inheriting behavior Core does not promise.

**Required evidence level:** automated-runtime

**Rationale:** the interfaces are active cross-package seams, but dispatch, retention, timeout, and failure isolation belong to concrete owners and must be validated there when their assumptions change.

## CORE-VALREQ-012

**Classification:** CONDITIONAL

**Trigger:** a coordinated release candidate or any change with transitive family impact

**Category:** full-regression

**Boundary:** in-process

**Targets:** all solution targets

**Acceptance criteria:** the complete solution build and test matrix passes, warning identities introduce no regression, and no dependent package requires an unapproved Core API or behavior change.

**Required evidence level:** automated-runtime

**Rationale:** focused Core tests cannot reveal a source-compatible semantic drift in a concrete implementation or transitive consumer.

## CORE-VALREQ-013

**Classification:** RECOMMENDED

**Trigger:** every change to a raw evidence model, callback surface, formatter, or experimental action contract

**Category:** security

**Boundary:** in-process

**Targets:** `net481`, `net9.0`

**Acceptance criteria:** review and focused tests preserve the documented raw/shallow data boundary, isolate the one promised throwing payload formatter, keep redaction and access control with persistence/transport owners, and retain the exact warning-only `NEKOEXP0001` marker without representing it as authorization.

**Required evidence level:** automated-runtime

**Rationale:** exception, dimension, payload, state, and action values may expose sensitive or state-changing application behavior even though Core itself performs no transport or persistence.

## CORE-VALREQ-014

**Classification:** NOT_APPLICABLE

**Trigger:** while Core remains a contracts-only library with no concrete pipeline, transport, persistence, platform hook, worker, or external resource

**Category:** runtime

**Boundary:** in-process

**Targets:** `net481`, `net9.0`

**Acceptance criteria:** no standalone Core runtime scenario is claimed; any runtime, soak, transport, UI, process, provider, or persistence requirement is owned and executed by the concrete dependent boundary that implements it.

**Required evidence level:** build-only

**Rationale:** compiling or running another module's scenario is evidence for that implementation, not proof that Core owns a runtime it deliberately does not contain.
