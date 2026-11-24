# Watchdog automatic desktop supervision completion

**Kind:** reference

**Lifecycle:** historical

**Subject:** completed WDG-AUTOBOOT-001 implementation and publication

**Date:** 2026-11-24

The owner authorized the hosting change, global application controls, validation,
integration and coordinated publication. [PR 18](https://github.com/Sakamoto0110/NekoLib/pull/18)
was merged. The [accepted contract](../watchdog-auto-bootstrap.md) owns startup,
configuration and compatibility; the [1.3.0 release record](../stable-release-1.3.0.md)
owns exact source, artifacts, qualification, public downloads and publication.

The opt-in build integration generates supervisor routing before the original
entry point. Default SameExecutable hosting runs a second headless process of
the application executable, requires no separate Host payload and no Main call.
Optional Sidecar and legacy manual startup remain supported. Referencing the
package alone never activates supervision. Build diagnostics reject unsupported
integration and warn about direct manual bootstrap overlap; runtime startup
failures report warnings without claiming an active supervisor.

Static WatchdogController.Disable/Enable/Shutdown are globally accessible.
Disable leaves the app running, permits configuration and may be followed by
explicit Enable. Shutdown confirms detach then exits with the caller's code;
application cleanup must finish first. Legacy Stop still terminates the target.
Normal entry return detaches; abrupt/fatal exit and external kill remain
recoverable. Bounded internal waits allow cooperative detach with long poll
intervals without changing the heartbeat observation boundary.

Clean candidate and exact integrated stable source passed the complete source,
API, documentation, compiler and isolated package gates. Independently downloaded
signed NuGet packages repeated the four-way WPF/WinForms and net481/net9.0-windows
process and native-loop checks, including repeated reconfiguration/re-enable and
shutdown from a worker. The release record retains exact counts, hashes, CI and
the failed pre-release attempts.

Automatic UI-hang detection is a separate next step; heartbeat still measures
process existence. This work introduced no final demo, companion, console fault
protocol, sync engine or application business policy. Human visual, security,
x86 desktop and long-soak campaigns remain outside the observed qualification.
The pre-existing Inspection timing issue INSP-ISSUE-001 remains open.
