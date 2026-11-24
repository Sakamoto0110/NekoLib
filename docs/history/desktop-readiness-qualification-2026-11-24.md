# Desktop readiness implementation qualification

**Kind:** reference

**Lifecycle:** historical

**Subject:** pre-release desktop correction decisions and executed qualification

**Date:** 2026-11-24

The owner authorized implementation, validation, integration and coordinated
publication. The accepted boundary and migration guidance live in the
[desktop integration decision](../desktop-readiness-1.2.0.md). The eventual
stable release record owns exact clean-source artifacts and remote publication;
this record alone does not declare a release.

## Findings closed in source

- Added scoped WPF dispatcher crash observation with explicit recoverable versus
  native propagation policy, owner-thread leases and fatal duplicate suppression.
- Removed WPF worker execution after dispatcher shutdown and runtime worker
  teardown fallback. Bootstrap native timer/observer cleanup now marshals to the
  UI owner. WPF toolkit focus requests marshal; WinForms ignores dead handles.
- Added explicit shared activity modes and a bootstrap observer factory without
  changing the original platform defaults or interfaces.
- Preserved native toast click routing and native geometry. Explicit close
  affordances remain necessary for interactive children. Added focus, routing,
  geometry, auto-dismiss and owner-thread cleanup evidence.
- Found and corrected an additional Watchdog defect in the new real-process
  matrix: the first forced restart after bootstrap used `taskkill /T` on the
  initial application, also killing its descendant Host. Forced termination now
  excludes the supervisor branch; later Host-launched target trees are unchanged.
- Repaired an older toolkit test that shut down a handle-less native root after
  its STA had exited. Its teardown now occurs on that owner. The in-process
  Watchdog controller fixture now waits for fake-Host readiness and reserves
  ThreadPool workers before invoking the synchronous facade; initial full runs
  had transient connection/replay failures. Product timeouts/retry semantics
  were not relaxed.

## Executed implementation checks

| Check | Result |
|---|---|
| Full Release solution tests, both target families | 1,821 passed across 24 target suites; zero failed/skipped. |
| Compiled public API comparison | All 30 baselines pass; 8 manifests receive additive changes only. |
| Documentation and warning baseline | Pass; no new warning identity in the qualifying test/build log. |
| Package family `1.2.0-local.6` | 16 main packages and 15 symbol packages; canonical structure/deployment/package consumers pass. |
| Package application recovery | 24/24: six scenarios on each WPF/WinForms × net481/net9.0-windows. |
| Updated package native behavior probes | All four pass, including focus-based popover dismissal, toast DPI geometry and UI-owner disposal. |

The six process scenarios are fatal UI exception, fatal worker exception,
external kill, explicit restart, recoverable UI exception followed by another
UI callback, and clean Host stop. They check one main window, replacement PID,
one incident bundle/report where appropriate, and process/Host cleanup. Fatal
exit code 71 belongs to the qualification application, not the reporting API.

The local candidate was intentionally produced with `-AllowDirty` while fixing
the implementation; its nuspec baseline commit is not proof of its dirty source.
It is disposable prequalification. The stable release must be rebuilt without
that switch from an identified clean integrated commit, and its downloaded
published packages must be qualified independently.

The final pre-release candidate `1.2.0-local.7` subsequently passed the entire
canonical flow without `-AllowDirty`, from clean commit
`26e9602fa3f92a9cdef050b4050e4c55cdb7475c`. It repeated all 1,821 tests and all
24 process scenarios, including test-owned child termination and observed
graceful/forced Host logs. All four consumers record x64 execution; the .NET 9
processes report runtime 9.0.22. Its consumer session is
`artifacts/package-smoke/sessions/5864c7f27f3747e29bbb75c815c97283/`, and the
canonical log is `artifacts/e2e-readiness/candidate-7-clean.log`.

Commands and local evidence are retained under `artifacts/e2e-readiness/`:
`full-tests-final.log`, `api-final.log`, `docs-final.log`, `candidate-6.log`, the
four `ui-*.log` files, and `prerequisites.json`. The candidate's private consumer
session is `artifacts/package-smoke/sessions/5f96a8f7cd234b4fa67689be02e57396/`;
its `desktop-recovery/results.json` contains the 24 executed process results.

## Objective boundaries

The .NET 9 SDK/Windows Desktop runtime and .NET Framework runtime were already
available. The host used SDK 10.0.403 to build the declared target frameworks;
the runtime inventory included Windows Desktop 9.0.22. Final package probes
assert the executing CLR major and record its version and bitness. The local
inventory found no ACE 12/16 registration in either registry view, no MSSQL
service and no SqlLocalDB command. No live SQL/Access or TheCatAPI request was
attempted. Deterministic ADO.NET/HTTP probes are not live-provider evidence.

No pending-operation journal, synchronization engine, conflict/idempotency
business rule, companion window or final console-control protocol was added.
Those application requirements remain separate. Watchdog heartbeat measures
process existence, not UI responsiveness. No new long soak, manual visual
acceptance or multi-monitor DPI campaign was performed.

## Stable delivery and completion

`DESKTOP-E2E-001` completed with the coordinated **1.2.1** family. The
[stable release record](../stable-release-1.2.1.md) owns exact source, hashes,
publication and final evidence. PRs 16 and 17 were merged; the immutable
package source is `85c1fc8227edc7b5c6ba643bb48dc4afc88922c0` (`v1.2.1`). Clean
candidate and stable gates each passed 1,837 tests across 24 suites and all 30
API manifests. Each gate ran all four package-only native consumers and 24
recovery scenarios.

Trusted-publication run `45329130534` accepted the 16 main and 15 symbol
packages. All 16 independent NuGet downloads passed repository-signature,
provenance and non-signature ZIP-entry comparisons; a new feed/cache then
passed all four native consumers and all 24 recovery cases again. Public
consumer session: `1013a79ad8114e6f84c9dc033567d3da`.

The intermediate 1.2.0 family remains immutable and superseded. Its original
qualification passed, but an additional enabled-binding refresh case later
failed against its signed WPF and WinForms packages. The compatible 1.2.1 patch
restores the existing modal ownership contract and adds those regressions.
The final evidence archive preserves the positive and negative records.

Application-specific synchronization rules and later console fault actions
remain separate future requirements. The single-window scope and the provider,
manual visual, security-campaign and long-soak limitations remain explicit;
no companion UI or automatic hang-detection claim was introduced.
