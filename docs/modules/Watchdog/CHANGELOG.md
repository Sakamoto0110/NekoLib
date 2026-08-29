# NekoLib.Watchdog Changelog

**Document ID:** WDG-CHANGELOG

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** consumer-visible evolution of NekoLib.Watchdog

**Surface:** changelog

**Boundary:** watchdog

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

The [coordinated family changelog](../../../CHANGELOG.md) remains the release
summary. This file records Watchdog-specific consumer impact without duplicating
package hashes or release provenance.

## Unreleased

**Packages:** `NekoLib.Watchdog`

**Compatibility class:** additive

**Consumer impact:** Package candidates produced through the corrected flow deliver XML member documentation for the accepted public API; compiled signatures and runtime behavior are unchanged.

**Migration:** none

- Documentation-enabled builds produce XML assets for both target assemblies.
  Immutable candidate `1.1.0-local.8` proved that the managed package contains
  its package-owned XML file and that isolated `PackageReference` consumers
  receive it. This candidate is evidence, not a public stable release
  declaration.
- Six public XML comments now describe behavior that already existed and was
  previously under-described: `WatchdogOptions.LogPath` defaults to
  `watchdog.log` only when file logging is enabled; a non-positive
  `HeartbeatIntervalMs` disables the heartbeat, which is observed on a
  `MonitorPollMs` boundary; a `WatchdogPipeLogSink` placed in
  `WatchdogOptions.LogSinks` is skipped as a feedback-loop guard;
  `WatchdogRuntime.PipeName` names the RPC endpoint whose event sibling is
  `.events`; entries forwarded through `WatchdogController.NotifyLog` are
  recorded at info severity with the original level in metadata and their
  exception text is not retained; and `WatchdogBootstrap.EnsureStarted` throws
  `InvalidOperationException` for an incompatible Host protocol or a launched
  Host that exited before confirming. This corrects documentation only; the
  behavior is unchanged and predates this entry.

## 1.0.0

**Packages:** `NekoLib.Watchdog`

**Compatibility class:** mixed

**Consumer impact:** The pre-stable candidate surface was corrected before the first stable contract; consumers upgrading from an earlier candidate require source changes and recompilation.

**Migration:** `docs/modules/Watchdog/migrations/f1.md`

- Runtime construction captures normalized configuration without mutating the
  caller's `WatchdogOptions`, copies the sink array, and exposes the effective
  identity through `WatchdogRuntime.PipeName`. `WatchdogOptions.PipeName` and
  public `Normalize()` were removed.
- The four update placeholders were removed. The internal wire response remains
  `not_implemented`; there is no replacement because no update behavior exists.
- `Stop(bool)` became terminal `Stop()`. Start is one-shot, stop before start is
  terminal, concurrent stop and dispose join one cleanup, and `WaitForExit`
  requires a successful start and waits for complete terminal cleanup.
- `Pause`, `Resume`, `Restart`, and `Stop` on `WatchdogController` return whether
  the exact acknowledgement was accepted. `Ping`, status evidence, and fail-soft
  crash and log notification are unchanged.
- `LogEvent.Meta` became nullable serializer-neutral `MetaJson`, and `Level`,
  `Msg`, and `Line` became nullable to match wire data.
- `WatchdogOptions.EnableHotkeys` was added, defaulting to `true`, with
  observable registration failure when enabled.
- `CrashBundler`, `CrashBundlerOptions`, `WatchdogHotkeys`, `WatchdogLogFile`,
  `NotifyLogBatch`, and the non-control command constants became implementation
  details; the obsolete `WatchdogLogPipeServer` was removed.
- Shutdown interrupts the crash-loop cooldown, drains owned workers, disposes
  process handles, and resolves the system `taskkill.exe` explicitly.
- Status distinguishes `historyEvictions`, `eventQueueDropped`, and
  `eventPublishFailures`, keeps `eventsDropped` as an alias, and defines
  `restartCount` consistently across launch and attach. Crash finalization
  reports complete, partial, failed, or no-pending outcomes internally.
- Bootstrap requires coordinated protocol v1: it emits the launch version,
  checks `protocol_version`, expects `attached:v1:<pid>:<token>`, and reports an
  incompatible Host instead of timing out. Update `NekoLib.Watchdog` and
  `NekoLib.Watchdog.Host` together and rebuild.
- Targets, project and package dependencies, deterministic target identity,
  `CurrentUserOnly` endpoints, the cooperative same-user security model, and
  separate Host packaging are unchanged.
