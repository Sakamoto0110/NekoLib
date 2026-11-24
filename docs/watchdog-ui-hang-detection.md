# Watchdog UI-hang recovery

**Kind:** reference

**Lifecycle:** current

**Subject:** accepted external UI responsiveness policy and additive 1.4 API review

**Decision date:** 2026-11-24

The owner explicitly authorized implementing UI-hang detection after the 1.3.0
automatic-hosting release. This work retains the separate supervisor process,
the same-executable default, optional sidecar and globally accessible controls.
No additional application Main call or UI-framework package dependency is added.

## Policy and compatibility

The accepted additive stable API adds only three WatchdogOptions properties:
EnableUiHangDetection, UiHangTimeoutMs and UiHangStartupGraceMs. Both Watchdog
API manifests may receive those declarations; the other 28 manifests remain
unchanged. Existing declarations and the version-one Host protocol remain.

Manual WatchdogOptions default to detection disabled. Automatic desktop
bootstrap enables it by default starting with 1.4.0, with a 15-second consecutive
failure threshold and a 30-second startup grace. This is an explicit new policy
for consumers upgrading automatic bootstrap. Configure may disable it or adjust
the bounds before activation, or after global Disable and before Enable.
Timeouts below 1,000 ms normalize to 1,000; negative grace normalizes to zero.

The supervisor sends WM_NULL using a bounded SendMessageTimeout to an observable
top-level window of the current target process. It caches the selected HWND so
Windows replacing a hung window with a ghost does not hide the original target.
The native probe has a 100-ms budget and samples at most once per 250-1,000 ms,
depending on the timeout. It does not hold the target or lifecycle lock while
probing. The monitor's existing short wait slices allow sampling independently
of a large process/heartbeat polling interval.

See Microsoft's [WM_NULL contract](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-null)
and [SendMessageTimeout contract](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendmessagetimeoutw).
This is native window message responsiveness, not business-operation completion
or worker-thread health. The existing heartbeat still reports process liveness.

Successful probes, changes of selected HWND/PID, unavailable probes and pauses
clear the consecutive failure interval. Every replacement and resume receives
its own grace. Grace observations discover windows but do not accumulate failure.
No observable window means no UI-hang decision. Explicit access/interop errors
are reported as unavailable and never treated as a confirmed hang.

After sustained failed acknowledgement beyond the configured timeout, the
supervisor logs ui_hang, terminates the target through the existing bounded
graceful/forced path and starts its replacement. It preserves the supervisor
when terminating an initially attached application tree. A failed termination
does not launch a duplicate live target. The control-pipe status/telemetry adds
uiResponsiveness with policy, current observation state, PID/HWND, consecutive
failure duration and native error code. Restart reason is ui_hang.

Pause suppresses automatic UI recovery and discards the old failure interval;
Resume starts a fresh grace. Disable detaches and stops all supervision. Enable
starts a fresh session. Shutdown confirms detach before exiting. Accepted
detach/stop and manual restart take precedence over an uncommitted probe result.

## Qualification and limits

Qualify deterministic timing/state transitions and configuration copying on both
targets, then real WPF/WinForms native-loop hangs, brief stalls, startup grace,
modal loops, paused supervision and explicit detection disable. Run both
same-executable and optional sidecar packaging on net481/net9.0-windows. Retain
all prior package and control scenarios, compare all API manifests, publish a
new immutable coordinated family and repeat isolated public-package consumption.

This first detector follows one discovered UI window thread per target process.
It is not an all-dispatcher or all-worker health contract. No-window processes
and startup before automatic bootstrap's first native-loop activation are not
covered by UI-hang recovery. A loop that still handles sent window messages can
remain responsive to this probe despite an application-level operation stall.
Polling, OS scheduling, termination and restart add latency beyond the threshold.
Legitimate synchronous UI work lasting longer than the policy must use an
appropriate timeout or disable/pause policy. Human visual, cross-user/elevation,
restricted-token, x86 desktop and long-duration soak campaigns remain separate.
