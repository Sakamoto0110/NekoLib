# NekoLib.Watchdog Validation Requirements

**Document ID:** WDG-VALIDATION-REQUIREMENTS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** evidence contract for qualifying the NekoLib.Watchdog boundary

**Surface:** validation-requirements

**Boundary:** watchdog

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

The [module manifest](MANIFEST.md) owns the inherited profile list. The
requirements below specialize those profiles for what this boundary actually is:
a dual-target library that owns real child processes, a machine-wide named
endpoint, a global kernel-object guard, three worker threads, a filesystem
evidence pipeline, and two different JSON serializers. They are derived from
architecture and risk, not from the tests that happen to exist; several are
`NOT_RUN` in [`VALIDATIONS.md`](VALIDATIONS.md) and say so there.

## WDG-VALREQ-001

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to source, project, target, or package settings

**Category:** build

**Boundary:** in-process

**Targets:** `net481` and `net9.0-windows`

**Acceptance criteria:** Both target assemblies build with zero errors, no new normalized warning identity, and no `CS1591` or malformed XML-comment diagnostic, and both generated XML files are produced beside their assemblies. The `CS1591` check must be evaluated for the `net481` surface as well, whose project-level suppression would otherwise hide it.

**Required evidence level:** build-only

**Rationale:** The two targets compile different serializer and environment-guard code behind `NETFRAMEWORK`/`NET9`, so one can compile while the other does not. XML documentation is part of the shipped managed asset, and `WDG-FINDING-002` records that the presence check is enforced by only one of the two compilations.

## WDG-VALREQ-002

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to a public declaration, target, nullable annotation, default value, or package dependency

**Category:** api-compatibility

**Boundary:** in-process

**Targets:** both accepted `NekoLib.Watchdog` manifests

**Acceptance criteria:** Release assemblies match both accepted manifests without an automatic baseline update; any delta carries an explicit compatibility disposition and a migration entry before acceptance.

**Required evidence level:** build-only

**Rationale:** The accepted surface is deliberately narrow after F1-WDOG, and several removals — update placeholders, the raw log server, the implementation helpers — are only enforceable against compiled metadata. Source review cannot prove that an internalized helper stayed internal.

## WDG-VALREQ-003

**Classification:** REQUIRED

**Trigger:** every implementation or contract change and every release candidate

**Category:** focused-regression

**Boundary:** process

**Targets:** `net481` and `net9.0-windows` test targets

**Acceptance criteria:** The complete focused suite passes with zero failed or skipped tests on both targets and preserves construction-time capture and its clamps, constructor non-mutation, one-shot admission, stop-before-start, concurrent stop and dispose, stop during the crash-loop cooldown, `WaitForExit` ordering, the resolved system `taskkill` path, initial attach including exit-code observability after the launcher handle is discarded, the single-instance guard, the six acknowledgement strings, replay and live log events, the separated loss counters, sink saturation, crash-bundle outcomes and manifest validity, and file rotation.

**Required evidence level:** automated-runtime

**Rationale:** The suite starts real child processes and binds real named pipes despite its `Unit` project location, so it is simultaneously the contract oracle and the only routinely executed supervision coverage. Lifecycle races, terminal ordering, and counter separation are unreachable by compilation.

## WDG-VALREQ-004

**Classification:** REQUIRED

**Trigger:** every change to a command name, an acknowledgement string, the status payload, the bootstrap handshake, or the protocol version

**Category:** protocol

**Boundary:** ipc

**Targets:** `net481` and `net9.0-windows`

**Acceptance criteria:** Every public and internal wire literal is pinned by an assertion, the four mutating operations return true only for their exact acknowledgement and false for absence, timeout, and protocol error, `protocol_version` answers `1`, `attach_status` produces `attached:v1:<pid>:<token>` only after startup completes, a ready but mismatched Host produces a version-specific diagnostic rather than a timeout, and the `update` command answers with the `not_implemented` error code.

**Required evidence level:** automated-runtime

**Rationale:** These literals are a compatibility contract between two independently versioned packages. `WDG-FINDING-003` records that `log_write`, `log_write_batch`, and `update` currently have no pinned literal, so a rename would compile and ship.

## WDG-VALREQ-005

**Classification:** REQUIRED

**Trigger:** every change to the monitor loop, restart policy, crash-loop accounting, termination, or shutdown

**Category:** runtime

**Boundary:** process

**Targets:** at least one supported target per campaign, both across a release

**Acceptance criteria:** Against real supervised processes: an ordinary exit and an unhandled crash each produce a newer healthy generation, a fast-crash burst produces the documented cooling gap, pause suppresses replacement while resume restores it, a restart request produces a new process identity, `restartCount` stays zero for the first supervised process in both attach and launch modes, and a run leaves no orphaned target, Host, worker thread, or bound endpoint.

**Required evidence level:** automated-runtime

**Rationale:** Supervision correctness is emergent from a foreground loop, a process handle, and interruptible waits. Ownership defects surface as a leaked process or a held endpoint that blocks the next run rather than as a failing assertion, so cleanup must be asserted rather than assumed.

## WDG-VALREQ-006

**Classification:** REQUIRED

**Trigger:** every change to termination, and any claim that forced termination works

**Category:** runtime

**Boundary:** process

**Targets:** `net481` and `net9.0-windows`

**Acceptance criteria:** A target that ignores the graceful close request is terminated by the forced path within the configured bound, its process tree is terminated with it, the helper process handle is released, and the graceful and forced attempts are both logged. Resolution of the system `taskkill.exe` must be observed as an executed termination, not only as a resolved path string.

**Required evidence level:** automated-runtime

**Rationale:** The forced path is the only guarantee that an unresponsive unattended application is replaced. The existing regression asserts that the resolved path ignores the process search path; it does not prove that termination happens or that the tree flag takes effect.

## WDG-VALREQ-007

**Classification:** REQUIRED

**Trigger:** every change to crash finalization, the manifest, checksums, retention, or the pending contract

**Category:** runtime

**Boundary:** filesystem

**Targets:** `net481` and `net9.0-windows`

**Acceptance criteria:** A pending crash directory is promoted with its artifacts intact, optional status and tail evidence are added when available, `manifest.json` parses as valid JSON with checksums matching file content, an optional-artifact failure yields a partial outcome and a truthful log naming the failed part, a throwing report callback cannot change the outcome or escape, retention keeps exactly `MaxBundles` newest bundles, and no pending directory survives a successful finalization.

**Required evidence level:** automated-runtime

**Rationale:** Crash evidence is the product's post-mortem value and is produced on the failure path, where a silent partial result is indistinguishable from success without an explicit outcome. The manifest is consumed by operators rather than by code, so validity matters more than shape stability.

## WDG-VALREQ-008

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to package identity, targets, dependencies, or documentation generation

**Category:** package-consumer

**Boundary:** package-feed

**Targets:** `lib/net481` and `lib/net9.0-windows7.0` package assets

**Acceptance criteria:** An immutable package contains both target assemblies with their matching XML files and no Host payload, declares `Newtonsoft.Json` only for `net481`, and an isolated `PackageReference` consumer restores those exact assets from the package and can call `WatchdogBootstrap`, `WatchdogController`, and `WatchdogRuntime` against a package-deployed Host on both target families.

**Required evidence level:** automated-runtime

**Rationale:** The Newtonsoft dependency is conditioned on one target, the XML asset has already shipped missing once for the whole family, and the bootstrap contract is only real when the library and a package-deployed Host are exercised together rather than from repository build output.

## WDG-VALREQ-009

**Classification:** REQUIRED

**Trigger:** any claim that `CurrentUserOnly` restricts access, and every change to the endpoint construction

**Category:** security

**Boundary:** ipc

**Targets:** `net481` and `net9.0-windows`

**Acceptance criteria:** A same-user peer reaches the control endpoint on both targets, and a peer running as a different operating-system user or at a different elevation level is denied. Denial must be observed, not inferred from the constructor arguments.

**Required evidence level:** automated-runtime

**Rationale:** [`REFERENCE.md`](REFERENCE.md) makes an access-control claim that only a denial observation supports. Pipes implements the boundary by different mechanisms per target — an explicit Windows ACL on `net481` and a platform pipe option on the modern target — so one can silently fail to restrict while the other works.

## WDG-VALREQ-010

**Classification:** NOT_APPLICABLE

**Trigger:** none

**Category:** security

**Boundary:** ipc

**Targets:** none

**Acceptance criteria:** none

**Required evidence level:** build-only

**Rationale:** Resistance to a hostile process already running as the same user — command authorization, endpoint squatting, attach-token replay, impersonation — is explicitly outside the accepted boundary stated in [`REFERENCE.md`](REFERENCE.md) and recorded as the accepted disposition of `WDG-FINDING-005`. It is listed here so its absence reads as an accepted scope decision rather than an untested requirement. Admitting it requires an accepted threat-model change, not a new test.

## WDG-VALREQ-011

**Classification:** CONDITIONAL

**Trigger:** deploying to, or claiming support for, an interactive account that is not an administrator and does not hold `SeCreateGlobalPrivilege`

**Category:** runtime

**Boundary:** process

**Targets:** at least one supported target

**Acceptance criteria:** A runtime started under a restricted standard-user token claims its single-instance guard, starts supervision, and rejects a second runtime for the same target; or the failure mode is observed, documented, and dispositioned before the support claim is made.

**Required evidence level:** automated-runtime

**Rationale:** The guard is created in the Windows global kernel-object namespace, whose creation Windows documents as privileged. The target product class is unattended terminals, which are exactly where a restricted interactive account is likely. `WDG-FINDING-001` records the observation; this requirement exists so that adding a support claim cannot skip its evidence.

## WDG-VALREQ-012

**Classification:** CONDITIONAL

**Trigger:** claiming that the enabled global hotkeys work, or changing the hotkey thread, window creation, or registration

**Category:** interactive-ui

**Boundary:** native-ui

**Targets:** at least one supported target

**Acceptance criteria:** On an interactive Windows desktop, Ctrl+Alt+P, Ctrl+Alt+R, and Ctrl+Alt+Q each produce their documented effect; a conflicting registration is reported to configured logging with its Win32 error and does not fail startup; and duplicate-start window activation brings the existing target to the foreground.

**Required evidence level:** interactive

**Rationale:** Registration, the message-only window, and window activation all require a real interactive desktop and cannot be asserted headlessly. `REFERENCE.md` documents chords that no executed evidence has demonstrated.

## WDG-VALREQ-013

**Classification:** CONDITIONAL

**Trigger:** supervising a target whose application uses `WatchdogController` on a different target framework than the supervising runtime

**Category:** protocol

**Boundary:** ipc

**Targets:** a `net481` process paired with a `net9.0-windows` process, in both role directions

**Acceptance criteria:** Status evidence, replay history, live log events, `MetaJson` payloads, and every acknowledgement round-trip correctly when the runtime and the controller are on different targets.

**Required evidence level:** automated-runtime

**Rationale:** `WatchdogRuntime` is a supported advanced surface and nothing binds a custom supervisor to the supervised application's target framework. In that composition the wire is produced by one serializer and consumed by the other. The package-deployed Host always matches its consumer's target, so the shipped path is same-target; this requirement covers only the advanced composition and is conditional for that reason.

## WDG-VALREQ-014

**Classification:** CONDITIONAL

**Trigger:** an unattended deployment that keeps supervision live for extended periods, or any change to the event queue, counters, log rotation, or bundle retention

**Category:** recovery-soak

**Boundary:** process

**Targets:** at least one supported target

**Acceptance criteria:** A sustained run over its stated nominal window records duration, workload, concurrency, operations, injected faults, expected and actual recovery, and resource measurements, and shows no crash, deadlock, leaked process or endpoint, unrecovered state, or unexplained growth in the log, bundle, or handle footprint.

**Required evidence level:** automated-runtime

**Rationale:** Watchdog is designed to run for the life of an unattended terminal. Log rotation, bundle retention, the replay history, and the three loss counters are all bounded mechanisms whose behavior only becomes visible over time, and the best available run records itself as below its specified window.
