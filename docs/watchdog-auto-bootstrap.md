# Watchdog same-executable auto bootstrap

**Kind:** reference

**Lifecycle:** current

**Subject:** accepted opt-in automatic desktop bootstrap, supervisor hosting and optional application control

**Decision date:** 2026-11-24

The owner authorized this scope after the 1.2.1 desktop release. The primary
composition uses the application executable in a second, headless supervisor
process. The existing deployed Host and manual bootstrap remain supported.
Automatic UI-hang detection remains a separate next step.

## Integration contract

`NekoLibWatchdogAutoBootstrap=true` opts an executable into build integration.
The package contributes a source generator and direct-only build targets; it
does not activate because a library or transitive dependency references it.
`NekoLibWatchdogHostingMode` selects `SameExecutable` (default) or `Sidecar`.
Sidecar mode still requires a direct same-version Host package reference.

The generated STA entry point handles supervisor arguments before invoking any
application entry code. On the application path it prepares configuration,
preserves the original Main signature/arguments/exit result, and schedules
activation at the first WinForms idle callback or WPF Loaded-priority dispatcher
callback. It adds no window. Optional application startup code may configure or
disable supervision before that callback; no Main call is required. Startup
before the native loop is not a supervised hang-detection window.

Cancellation before activation prevents startup. Disabling active supervision
detaches the supervisor without terminating the application. This differs from
the existing Stop command, which retains its target-termination semantics.
Normal return from the original Main detaches supervision; an exception escaping
Main, abrupt process exit or external termination remains recoverable by the
external supervisor. Environment.Exit does not run this normal-return path.

Configuration is captured when supervision starts and belongs to that supervisor
session. Optional Configure calls after activation are rejected; cancellation
remains available. Configuration is portable scalar process/runtime policy;
in-process sink and telemetry objects cannot be transferred to another process.
Target identity, original arguments, attach PID and token remain bootstrap-owned.

## Diagnostics and compatibility

Unsupported or ambiguous entry points fail with specific build diagnostics.
NEKOWDG003 warns about direct manual EnsureStarted calls when auto bootstrap is
enabled. Excluding the required analyzer produces NEKOWDG001 instead of silently
skipping supervisor routing. Large monitor intervals do not delay cooperative
detach: internal waits/sleeps observe shutdown in bounded slices while preserving
the configured heartbeat poll boundary.
Top-level statements, trimming and NativeAOT are outside this first desktop
integration. Existing explicit entry points and WPF's generated App.xaml entry
must qualify on net481 and net9.0-windows. Automatic startup failures emit a
warning and leave the application running without claiming active supervision;
invalid reserved supervisor invocation fails before opening application UI.

The executable must directly reference the package with its build and analyzer
assets enabled. Explicitly excluding the entire build integration also removes
its validation hooks; the library cannot diagnose code that was excluded from
the build. Automatic startup is never inferred from a transitive reference.

The build integration requires a C# compiler compatible with Roslyn 4.12 or
later (the .NET 9 SDK family). It preserves private Main, supported async Main,
arguments, explicit return values and Environment.ExitCode for void/Task Main.
Module initializers are ordinary CLR assembly initialization and still run in
both processes; application-only side effects must not be placed there.

### Accepted API review

The authorized compatible minor adds `WatchdogHostingMode`,
`WatchdogAutoBootstrapOptions`, `WatchdogAutoBootstrap` and
`WatchdogRuntime.Detach()`. Both compiled Watchdog target diffs contain only
these additions; every existing declaration is retained. Prepare,
CompleteApplication and TryRunSupervisor are deliberate public generated-code
contracts, not hidden implementation APIs. The two Watchdog manifests may be
updated for this reviewed addition; the other 28 manifests must remain unchanged.
Legacy EnsureStarted and Stop keep their existing sidecar and target-termination
semantics. The new internal detach request identifies the expected current PID
and is rejected while a supervisor-owned stop/restart is already in progress.

The generator is build-only infrastructure, not a new public package. The family
remains 16 coordinated packages. Public additions require a compatible minor,
explicit API review, clean package gates, independent published-package probes
and retained legacy behavior. No existing version is overwritten.

## Completion evidence

Qualify generated entry routing, private/async Main, arguments and exit values,
real WPF App.xaml, opt-in isolation, configuration before activation, cancellation
before/after activation, invalid invocation and warnings, one-window recovery,
normal shutdown, self-host packaging without Host payload, and optional sidecar
deployment on both target families. Preserve the original checkout's unrelated
Devices edit. Integrate the completed change and delete the work branch.
