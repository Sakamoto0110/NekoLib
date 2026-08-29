# NekoLib.Http Validation Requirements

**Document ID:** HTTP-VALIDATION-REQUIREMENTS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** evidence contract for qualifying the NekoLib.Http boundary

**Surface:** validation-requirements

**Boundary:** http

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

The [module manifest](MANIFEST.md) owns the inherited profile list. The
requirements below specialize those profiles for a library that owns endpoint
declaration and response materialization while deliberately owning no transport,
no credential, and no resilience policy.

Two evidence layers stay separate throughout and neither substitutes for the
other. **Deterministic evidence** comes from tests using a controlled
`HttpMessageHandler` and reaches no network. **Provider evidence** comes from the
TheCatAPI scenario, requires a maintainer-owned key, mutates a real third-party
account, and proves only the flow and the date it records.

## HTTP-VALREQ-001

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to source, project, target, or package settings

**Category:** build

**Boundary:** in-process

**Targets:** `net481` and `net9.0`

**Acceptance criteria:** Both target assemblies build with zero errors and no new normalized warning identity.

**Required evidence level:** build-only

**Rationale:** The project carries no conditional compilation but does differ by target in how `System.Net.Http` is referenced, so a target-specific break would come from the reference model rather than from a visible branch in source.

## HTTP-VALREQ-002

**Classification:** REQUIRED

**Trigger:** every release candidate and every public declaration, accessibility, target, nullable, or default-value change

**Category:** api-compatibility

**Boundary:** in-process

**Targets:** both accepted `NekoLib.Http` manifests

**Acceptance criteria:** Release assemblies match both accepted manifests exactly; any delta carries an explicit compatibility disposition and a migration entry before acceptance. The manifests are identical apart from the per-target framework attribute.

**Required evidence level:** build-only

**Rationale:** This surface deliberately closes its endpoint hierarchy through accessibility rather than sealing, so an accidental widening back to `protected` would silently re-advertise an extension point that cannot work.

## HTTP-VALREQ-003

**Classification:** REQUIRED

**Trigger:** every change to catalog registration, lookup, or dispatch admission

**Category:** focused-regression

**Boundary:** in-process

**Targets:** deterministic suite on `net481` and `net9.0`

**Acceptance criteria:** The suite proves that duplicate names are rejected under ordinal case-insensitive comparison, that a builder captured outside the configure callback rejects later registration, that sending an endpoint instance absent from the catalog throws before any transport occurs, and that supplying a different instance whose name *is* registered produces the distinct message saying so.

**Required evidence level:** automated-runtime

**Rationale:** Registration matches by name and dispatch matches by object identity; the two-model design is the module's most surprising behavior and its error message is the only thing that makes it diagnosable.

## HTTP-VALREQ-004

**Classification:** REQUIRED

**Trigger:** every change to relative URI construction, escaping, or query assembly

**Category:** security

**Boundary:** in-process

**Targets:** both deterministic test targets

**Acceptance criteria:** The suite proves that each path segment and each query name and value is escaped independently, that a segment containing a slash is escaped rather than treated as a separator, that blank segments and blank query names are rejected, that repeated query names keep every value in insertion order, that a null query value omits the parameter entirely, and that an empty segment list targets the base address.

**Required evidence level:** automated-runtime

**Rationale:** Escaping is the mechanism — not a convenience — that prevents an endpoint route from replacing the scheme or authority the consumer configured. `RelativeUri` has no public constructor precisely so this is the only way to build one.

## HTTP-VALREQ-005

**Classification:** REQUIRED

**Trigger:** every change to client construction, request construction, body selection, or the request-configuration callback

**Category:** focused-regression

**Boundary:** in-process

**Targets:** both deterministic test targets

**Acceptance criteria:** The suite proves that a base address which is missing, relative, or lacking a trailing slash is rejected at construction, that invalid options report `ArgumentException` naming the caller's parameter, that write-verb factories construct the expected method and JSON body, that a body selector replaces the default whole-request serialization, and that the configuration callback runs after body assignment and exactly once per send.

**Required evidence level:** automated-runtime

**Rationale:** The trailing-slash rule is what stops standard URI resolution from silently dropping the final base path segment, and callback ordering is what lets a consumer override the content type this module sets.

## HTTP-VALREQ-006

**Classification:** REQUIRED

**Trigger:** every change to response buffering, the size bound, or the evidence carried on its failure

**Category:** focused-regression

**Boundary:** in-process

**Targets:** both deterministic test targets

**Acceptance criteria:** The suite proves that a body of exactly the configured limit succeeds and one byte more throws, that the bound is enforced both from a declared `Content-Length` and again while streaming so an absent or understated header cannot bypass it, and that the resulting exception carries the endpoint name, the limit, the status code, the reason phrase, and the headers captured before the body was read.

**Required evidence level:** automated-runtime

**Rationale:** The bound is the only thing standing between a consumer and an unbounded allocation, and discarding a body must not discard the protocol evidence that explains why.

## HTTP-VALREQ-007

**Classification:** REQUIRED

**Trigger:** every change to response typing, header capture, or failure wrapping

**Category:** focused-regression

**Boundary:** in-process

**Targets:** both deterministic test targets

**Acceptance criteria:** The suite proves that a non-success status is returned rather than thrown, is never deserialized, keeps its raw body, reports no typed value, and makes the checked accessor throw; that a no-content endpoint yields the shared typed marker; that response and content headers merge case-insensitively with response values first and multi-value order preserved; and that a malformed success body raises the deserialization exception without placing the body in its message.

**Required evidence level:** automated-runtime

**Rationale:** Preserving protocol evidence instead of converting it into exceptions is this module's central design choice, and the one place it could leak sensitive content is an exception message built from a response body.

## HTTP-VALREQ-008

**Classification:** REQUIRED

**Trigger:** every change to charset resolution or body decoding

**Category:** focused-regression

**Boundary:** in-process

**Targets:** both deterministic test targets

**Acceptance criteria:** The suite proves that a declared charset unknown to the running framework falls back to UTF-8 without throwing and preserves the status, headers, and body; that a legacy code page produces the same outcome on both targets even though only one can resolve it; and that a leading byte-order mark is removed from the decoded body.

**Required evidence level:** automated-runtime

**Rationale:** `net481` ships the full code-page set and `net9.0` does not, so this is the one place where identical source can produce a different observable result per target. The regression must assert the outcome rather than the encoding.

## HTTP-VALREQ-009

**Classification:** REQUIRED

**Trigger:** every change to cancellation checkpoints or to how transport failures surface

**Category:** focused-regression

**Boundary:** in-process

**Targets:** both deterministic test targets

**Acceptance criteria:** The suite proves that a cancelled token reaches the message handler, and evidence and documentation record that cancellation is observed before request construction, passed to the send, and re-checked around body reading, and that timeout and cancellation remain BCL outcomes that this module never converts into its own exception type.

**Required evidence level:** automated-runtime

**Rationale:** Wrapping these would replace a well-known contract and hide a platform difference the documentation can simply state; the checkpoints are what make cancelling a slow body read effective rather than advisory.

## HTTP-VALREQ-010

**Classification:** REQUIRED

**Trigger:** every change to ownership, request construction, exception content, or scenario artifacts

**Category:** security

**Boundary:** in-process

**Targets:** both deterministic test targets, the current technical reference, and the scenario contract

**Acceptance criteria:** Evidence and documentation state that the module acquires no credential, certificate, proxy, or retry policy, never disposes the consumer's client, never logs headers or bodies, and never places a response body in an exception message; that the request-configuration callback is the single declared bypass through which a consumer can set an absolute request URI and is therefore the consumer's trust boundary; and that no scenario artifact contains a key, credential header, or request/response body.

**Required evidence level:** build-only

**Rationale:** Most of this module's security posture is what it refuses to do, and a refusal is only real while it is stated and checked — the one deliberate bypass has to be named rather than discovered.

## HTTP-VALREQ-011

**Classification:** CONDITIONAL

**Trigger:** a maintainer-owned provider key, an accepted cleanup boundary, and explicit authorization to send external requests are all available; and any change to endpoint construction, response materialization, or the scenario

**Category:** runtime

**Boundary:** network

**Targets:** the scenario on `net481` and `net9.0`

**Acceptance criteria:** The scenario exits `0` with every check passed on each target it claims; typed GET, POST, and DELETE all behave coherently against the live provider; the run-owned identifier is unique per run; cleanup reconciles and reports no residue; and the recorded artifact contains no key, credential header, or body. A run without the key must exit `3` having sent no request.

**Required evidence level:** automated-runtime

**Rationale:** This is the only requirement that can observe a real server, and it is conditional because it costs a third-party credential and mutates a real account. Its result is evidence for the flow and date it records and never generalizes to provider uptime or policy.

## HTTP-VALREQ-012

**Classification:** RECOMMENDED

**Trigger:** a change to response reading, completion-option handling, or the documented timeout shape

**Category:** runtime

**Boundary:** network

**Targets:** a real server on each supported target

**Acceptance criteria:** Observed behavior for the transport concerns the in-process handler cannot reproduce: TLS, redirect following, response compression, connection reuse, and a real `HttpClient.Timeout` producing the documented per-target exception shape.

**Required evidence level:** automated-runtime

**Rationale:** Every deterministic test and every F1 probe used an in-process handler, so the module's interaction with real streamed transport is documented from source and BCL semantics rather than measured. See [`HTTP-FINDING-003`](FINDINGS.md). It stays below REQUIRED because transport is deliberately the consumer's contract, not this module's.

## HTTP-VALREQ-013

**Classification:** REQUIRED

**Trigger:** every publishable release candidate and every package, dependency, target, or XML-delivery change

**Category:** package

**Boundary:** package-feed

**Targets:** the `NekoLib.Http` package and both target assets

**Acceptance criteria:** The immutable package contains both target assemblies, a package-owned matching XML file per assembly, correct target groups, `Newtonsoft.Json` at the pinned version in **both** dependency groups, provenance metadata, and a recorded hash, with no unexpected repository assets.

**Required evidence level:** build-only

**Rationale:** This is the only boundary in the family whose package carries a third-party dependency, and that dependency reaches the compiled public surface, so a missing or divergent dependency group is a consumer-visible break rather than a packaging detail.

## HTTP-VALREQ-014

**Classification:** REQUIRED

**Trigger:** every publishable release candidate and every package, dependency, or target change

**Category:** package-consumer

**Boundary:** package-feed

**Targets:** isolated package-reference-only consumers on both supported targets

**Acceptance criteria:** Clean isolated consumers restore and build without repository project references, resolve the transitive `Newtonsoft.Json` dependency, and extract the expected assembly and XML pairs from the package cache.

**Required evidence level:** build-only

**Rationale:** A project-reference build cannot prove target asset selection, transitive dependency resolution, or the `net481` framework reference behaving correctly for a real consumer.

## HTTP-VALREQ-015

**Classification:** REQUIRED

**Trigger:** every publishable release candidate and every public XML comment or documentation-generation change

**Category:** build

**Boundary:** in-process

**Targets:** both target assemblies

**Acceptance criteria:** Documentation-enabled Release builds generate matching XML assets with zero missing-member, malformed, unresolved, or ambiguous XML-comment diagnostics.

**Required evidence level:** build-only

**Rationale:** Unlike the Logging, Telemetry, and Inspection packages, this project sets no `NoWarn`, so `CS1591` is live on both targets and an ordinary build already enforces this. That makes the requirement cheap to hold and expensive to lose silently if a suppression is ever added.

## HTTP-VALREQ-016

**Classification:** REQUIRED

**Trigger:** every coordinated family release candidate

**Category:** full-regression

**Boundary:** in-process

**Targets:** `NekoLib.sln`

**Acceptance criteria:** The canonical full build, test, and package campaign passes and preserves the repository warning baseline, dependency graph, public APIs, packages, consumers, and unrelated module regressions.

**Required evidence level:** automated-runtime

**Rationale:** HTTP has no NekoLib project dependency and no repository consumer, so it cannot break another module through code — but it does participate in the coordinated package family, where its third-party dependency is the shared risk.
