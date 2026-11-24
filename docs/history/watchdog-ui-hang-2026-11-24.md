# Watchdog UI-hang recovery completion

**Kind:** reference

**Lifecycle:** historical

**Subject:** completed WDG-UIHANG-001 implementation, qualification and publication

**Date:** 2026-11-24

The owner explicitly authorized UI-hang recovery and configurable confirmation
time after the 1.3.0 automatic-hosting delivery. [PR 19](https://github.com/Sakamoto0110/NekoLib/pull/19)
was merged. The [accepted policy](../watchdog-ui-hang-detection.md) owns behavior
and compatibility; the [1.4.0 release record](../stable-release-1.4.0.md) owns
exact source, artifacts, hashes, CI and public-package evidence.

The external supervisor uses bounded native window-message probes, independently
of process liveness. Automatic desktop bootstrap enables configurable timeout
and initialization grace without Main calls or extra default executables.
Manual runtime defaults remain opt-in. Global controls retain their contracts;
pause/resume resets the failure history and invalidates old uncommitted decisions.
The existing protected-tree termination path preserves the initially launched
supervisor, and the replacement must have a responsive window and a new PID.

Deterministic timing/configuration regressions and package-only native scenarios
qualified WPF/WinForms on both targets and both hosting modes. Brief stalls,
initialization grace, native modal loops, paused monitoring and detection disable
continued without false-positive replacements in the clean local matrices.
Independently downloaded signed packages matched every qualified payload entry;
their GUI repetition initially stopped at the owner's execution limit, then
completed all 162 remaining cases after explicitly authorized continuation.
The [completion supplement](../stable-release-1.4.0-public-qualification.md)
records the combined 168 distinct public passes and qualification-only placement. The first development attempt exposed
the launcher's SW_HIDE fixture setting; qualification now exposes actual native
offscreen windows and rejects vacuous no-window negative-case results.

The policy follows one observed native UI thread, not all workers or business
operation progress. The public matrix combines six retained cases and 162 newly executed passes;
all four UI/framework pairs and both hosting modes are covered from signed
public packages. The earlier paused results remain unchanged historical evidence. No-window targets,
pre-activation startup and the other
release-record exclusions remain outside the observed coverage. No final demo,
companion, console fault protocol or synchronization engine was added.
