# NekoLib.Navigation Validation Requirements

**Document ID:** NAV-VALIDATION-REQUIREMENTS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** evidence contract for qualifying the NekoLib.Navigation family boundary

**Surface:** validation-requirements

**Boundary:** navigation

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

The [module manifest](MANIFEST.md) owns the inherited profile list. The
requirements below specialize those profiles for the single-mounted-context,
stateful, native-UI, and adapter boundaries implemented by this family.

## NAV-VALREQ-001

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to source, project, target, or package settings

**Category:** build

**Boundary:** in-process

**Targets:** `NekoLib.Navigation` on `net481` and `net9.0`; WinForms and WPF adapters on `net481` and `net9.0-windows`

**Acceptance criteria:** All six target assemblies build with zero errors and no new normalized warning identity is introduced.

**Required evidence level:** build-only

**Rationale:** Conditional compilation, Windows desktop targets, nullable contracts, and adapter-specific references can diverge independently even when one target compiles.

## NAV-VALREQ-002

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to a public or protected declaration, target, nullable annotation, default value, or package boundary

**Category:** api-compatibility

**Boundary:** in-process

**Targets:** all six accepted Navigation API manifests

**Acceptance criteria:** Release assemblies match every accepted manifest without an automatic baseline update; any delta has an explicit compatibility disposition and migration entry before acceptance.

**Required evidence level:** build-only

**Rationale:** The stable contract spans three packages and two target families, while source review alone cannot prove compiled metadata parity.

## NAV-VALREQ-003

**Classification:** REQUIRED

**Trigger:** every Navigation implementation or contract change and every release candidate

**Category:** focused-regression

**Boundary:** in-process

**Targets:** `net481` and `net9.0-windows` test targets

**Acceptance criteria:** The complete focused suite passes with zero failed or skipped tests and preserves lifecycle order, guard outcomes, history/state, reuse, load modes, rollback, surfaces, idle/session behavior, observation, reset, shutdown, and static-facade isolation.

The 2.0.0 candidate requires the following shared-core regressions before these issues can close. Record the agreed behavior and link each passing test and its execution result to the corresponding issue.

- [NAV-ISSUE-002](ISSUES.md#nav-issue-002): Drive the idle timer deterministically up to its deadline. Exercise user navigation originating outside the observed host, activity inside the host, and programmatic navigation. Deliver an already scheduled tick after a renewal. Assert the current page and session state at the old and renewed deadlines.
- [NAV-ISSUE-003](ISSUES.md#nav-issue-003): Open a popover with synthetic session-specific content and a pending result. Trigger automatic sign-out. At the first presentation of the signed-out Idle page, assert that the old surface has been detached and can receive no input. Check that its result settles exactly once and that signing in as a second user does not restore the surface. Cover ordinary navigation and explicit Reset as controls. Verify the corrected candidate through a package-only consumer.
- [NAV-ISSUE-004](ISSUES.md#nav-issue-004) covers a prompt containing synthetic typed text. Exercise automatic sign-out and direct `GoIdleAsync()` independently. At the first Idle presentation in each case, verify that the view is gone, input is unavailable, the modal block is released, and the pending result has completed once without submitting the text. Assert that the automatic timeout signed the user out and that direct navigation preserved the signed-in user's roles and permissions. Also verify that explicit `Session.SignOut()` dismisses the prompt while keeping the current page.
- For the optional prompt draft, reopen the same prompt type on its original page in the same session and confirm that the field is prefilled. The new call must remain pending until the user acts. Check that opening the prompt on another page does not consume the draft and that a new session cannot recover it.
- For [NAV-PROPOSAL-002](../../proposals/navigation-session-timeout.md#nav-proposal-002), drive the absolute deadline independently of the inactivity timer. Exercise continuous input, direct Idle navigation, both orderings when the deadlines compete, a sign-in while a previous expiration is queued, shutdown, and disabled configuration. A package-only consumer must demonstrate sign-out at the original deadline despite continuous input.

**Required evidence level:** automated-runtime

**Rationale:** Navigation combines process-wide state, asynchronous lifecycle work, caches, timers, and synchronous callbacks; compilation cannot qualify their ordering or cleanup.

## NAV-VALREQ-004

**Classification:** REQUIRED

**Trigger:** every stable release and every change to a native host, dispatcher, interaction observer, focus observer, blocker, timer, surface base, toolkit, or default loading mask

**Category:** interactive-ui

**Boundary:** native-ui

**Targets:** WinForms `net481` and `net9.0-windows`; WPF `net481` and `net9.0-windows`

**Acceptance criteria:** A human-driven native scenario verifies page visibility, focus, input reachability, modal blocking, toast and popover dismissal, idle interaction, reset, awaited shutdown, and clean window/process exit on every affected adapter/target combination.

**Required evidence level:** interactive

**Rationale:** Focus routing, event bubbling, handle/dispatcher state, z-order, DPI, and design-surface behavior are not established by headless tests.

## NAV-VALREQ-005

**Classification:** CONDITIONAL

**Trigger:** a release or change affecting lifecycle admission, concurrency, background work, guards, rollback, idle, reset/shutdown, native cleanup, caches, or resource ownership

**Category:** recovery-soak

**Boundary:** native-ui

**Targets:** representative recovery execution plus native smoke parity across every affected supported adapter/target combination

**Acceptance criteria:** Deterministic workload and planned faults recover without unexpected failure; awaited shutdown leaves zero scenario-owned pages, surfaces, native children, timers, background operations, providers, processes, or windows; duration and resource measurements are recorded without inventing an unmeasured leak threshold.

**Required evidence level:** automated-runtime

**Rationale:** Short unit runs cannot expose slow retention, late completion, repeated-mount, or teardown races in a long-lived desktop shell.

## NAV-VALREQ-006

**Classification:** CONDITIONAL

**Trigger:** a change to a public WinForms or WPF page/surface base, constructor shape, parenting behavior, design-mode path, or designer metadata

**Category:** interactive-ui

**Boundary:** native-ui

**Targets:** every affected Visual Studio native designer plus both automated Navigation test targets

**Acceptance criteria:** A real consumer subclass loads and renders in the applicable designer without executing unsafe runtime setup, while automated tests preserve non-abstract loadability and handle-less parenting behavior; generic WinForms prompt consumers use a non-generic closed shim.

**Required evidence level:** interactive

**Rationale:** Visual Studio instantiates base classes and parents controls under conditions that ordinary builds and most runtime tests do not reproduce.

## NAV-VALREQ-007

**Classification:** REQUIRED

**Trigger:** every publishable release candidate and every package, dependency, target, or XML-documentation delivery change

**Category:** package-consumer

**Boundary:** package-feed

**Targets:** all three Navigation packages and their supported target assets

**Acceptance criteria:** Immutable packages contain the expected assemblies and matching XML files; isolated PackageReference-only WinForms and WPF consumers restore from the candidate feed and build on all supported targets without repository project references.

**Required evidence level:** build-only

**Rationale:** Source and project-reference builds cannot prove transitive dependencies, target asset selection, XML delivery, or real package consumption.

## NAV-VALREQ-008

**Classification:** CONDITIONAL

**Trigger:** a change to Navigation diagnostics, Logging/Telemetry/Inspection bridges, page/guard failure projection, session snapshots, or trace payload selection

**Category:** security

**Boundary:** in-process

**Targets:** `net481` and `net9.0-windows` test targets

**Acceptance criteria:** Automated tests prove correlation and terminal ordering, subscriber/writer failure isolation, provider cleanup, disabled-recorder behavior, and the exclusion of page instances, payloads, captured state, role names, and permission names from scalar traces and Inspection snapshots.

**Required evidence level:** automated-runtime

**Rationale:** Observation is synchronous and process-local, but accidental retention or projection of application state would expand the privacy and lifecycle boundary.

## NAV-VALREQ-009

**Classification:** REQUIRED

**Trigger:** every publishable release candidate and every change to public or protected XML comments or documentation-generation settings

**Category:** build

**Boundary:** in-process

**Targets:** all six Navigation target assemblies

**Acceptance criteria:** Documentation-enabled Release builds generate the matching XML asset for every assembly with zero missing-member, malformed, unresolved, or ambiguous XML-comment diagnostics; intentional inheritance resolves to a documented contract.

**Required evidence level:** build-only

**Rationale:** Accepted API manifests prove compiled shape but do not prove IDE member guidance, cross-target documentation parity, or valid generated XML.
