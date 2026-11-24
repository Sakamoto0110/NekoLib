# NekoLib 1.4.0 Stable UI-Hang Recovery Release

**Kind:** reference

**Lifecycle:** current

**Subject:** configurable external UI responsiveness recovery and exact public distribution evidence

**Release date:** 2026-11-24

**Package source:** `86340ab27ade1322225a3ae2005b6a32c59ab005` (`v1.4.0`)

The coordinated family contains 16 main and 15 symbol packages. The
[merged implementation](https://github.com/Sakamoto0110/NekoLib/pull/19) adds
UI-hang recovery to the same-executable and optional sidecar supervisor. It adds
three reviewed WatchdogOptions properties on both targets; all prior declarations
and the other 28 manifests remain unchanged. The
[accepted contract](watchdog-ui-hang-detection.md) and
[technical reference](modules/Watchdog/REFERENCE.md#supported-composition-paths)
own the policy, configuration and lifecycle details.

## Consumer setup and compatibility

Align the NekoLib package family at 1.4.0. A direct Watchdog reference and the
existing executable-project opt-in are sufficient for automatic UI recovery:

```xml
<PropertyGroup>
  <NekoLibWatchdogAutoBootstrap>true</NekoLibWatchdogAutoBootstrap>
</PropertyGroup>
```

No application Main call or extra supervisor executable is required in default
SameExecutable mode. The application executable also runs in a separate,
headless supervisor process. Optional Sidecar still requires a direct same-version
Host reference. Referencing the package alone never activates supervision.

Automatic bootstrap enables UI detection with a 30-second initialization grace,
followed by a 15-second consecutive failed-probe threshold. This new automatic
desktop policy is an explicit behavior addition for 1.3 consumers upgrading to
1.4. Manual runtime options leave UI detection disabled by default. Optional
configuration before activation can adjust either duration:

```csharp
WatchdogAutoBootstrap.Configure(options =>
{
    options.RuntimeOptions.UiHangTimeoutMs = 10000;
    options.RuntimeOptions.UiHangStartupGraceMs = 20000;
});
```

WinForms may configure in its form constructor, before the native loop; WPF may
configure in OnStartup before the scheduled activation callback. Timeouts below
1,000 ms normalize to 1,000, and negative grace normalizes to zero. For long
intentional synchronous UI work, choose a suitable policy or explicitly disable
detection. The policy is captured for the supervisor session. To change it while
running, use WatchdogController.Disable, Configure, then Enable.

Global Disable detaches while keeping the application alive. Enable starts a
fresh session. Shutdown confirms detach then calls Environment.Exit; callers own
prior application cleanup. Pause suspends automatic recovery, clears the old
failure interval, and Resume grants a new grace. Existing Stop still terminates
the target. Normal application return detaches; fatal/abrupt exit remains
recoverable. No companion or final demo/console-control product was added.

## Mechanism and observed bounds

The external supervisor sends WM_NULL to a discovered target UI window using
SendMessageTimeout with a 100-ms native budget. It caches the HWND while it
remains owned by the target, including when Windows hides it behind a ghost.
Missing windows and explicit interop/access failures do not confirm a hang.
Probes run between the existing short process-wait slices, outside the target
and lifecycle locks, at a 250-1,000-ms sampling interval according to policy.

Successful responses, changes of PID/HWND, unavailable observations and pauses
clear consecutive failure history. Each attached/launched target and resume gets
its own monotonic grace. Before committing termination, the monitor revalidates
target identity, shutdown/control state and the observation revision. This
prevents an old timeout decision from winning after pause/resume or accepted
detach. Recovery uses the existing graceful/forced path and preserves the
supervisor branch when terminating an initially attached target tree. Failed
termination does not create a duplicate live target.

Status and telemetry add uiResponsiveness: enabled policy, observation state,
PID/HWND, consecutive failure duration, timeout/grace and native error code.
Recovery logs ui_hang with the target PID and reports restartReason=ui_hang.
The existing heartbeat remains a process-existence signal; UI responsiveness is
a separate observation. Detection, OS scheduling, kill budgets and RestartDelayMs
add latency beyond the configured failure threshold.

## Executed qualification

| Gate | Observed result |
|---|---|
| Clean candidate and integrated stable source | 1,897 tests across 24 suites; zero failed/skipped; 138 Watchdog tests per target |
| API and documentation | All 30 compiled manifests and documentation/normalized warning checks pass; only two Watchdog baselines add three properties each |
| Minimum compiler | SDK 9.0.319 / C# 7.3 builds and routes both target entry points from exact stable packages |
| Native package consumers | Existing four framework/target module consumers and all Host deployment/protocol guards pass |
| Clean candidate and stable isolated package gates | 168 checks each: 24 retained legacy recovery, 88 retained automatic/control/opt-out and 56 new UI scenarios |
| Independent public downloads | All 16 main packages pass version/source provenance, every non-signature ZIP-entry comparison and repository-signature verification |
| Public-package consumers | Four native module consumers pass; all 168 distinct recovery/control checks pass across the initial six cases and 162-case continuation |

| UI / target | Same executable | Optional sidecar | Original-entry opt-out | Legacy recovery | Stable native matrix |
|---|---|---|---|---|---|
| WinForms net481 | 22/22 | 13/13 | PASS | 6/6 | PASS |
| WinForms net9.0-windows | 22/22 | 13/13 | PASS | 6/6 | PASS |
| WPF net481 | 22/22 | 13/13 | PASS | 6/6 | PASS |
| WPF net9.0-windows | 22/22 | 13/13 | PASS | 6/6 | PASS |

The seven UI cases run for each hosting/framework/target pair: 2-second and
6-second sustained hangs, separated brief stalls, a block longer than the
failure threshold within startup grace, a native modal loop longer than the
threshold, paused UI blocking followed by resume, and blocking with detection
explicitly disabled. This yields 16 actual hang recoveries and 40 negative UI
cases per full gate. The driver verifies a different replacement PID, responsive
replacement UI, correct reason/count, argument preservation, one primary window,
supervisor continuity/forced attached-tree termination and complete endpoint/process
cleanup. Negative cases must observe a responsive HWND unless detection is
explicitly disabled, preventing a vacuous no-window pass.

The public-package GUI repetition initially stopped after four native module
consumers and six WinForms net481 legacy recovery cases. The owner subsequently
authorized continuation after reboot with windows at `(2700, 100)` and without
computer-use tools. All 162 remaining cases passed from unchanged signed public
packages in a new private feed/cache. Together, the two sessions cover all 168
distinct cases; no passed historical case was repeated or counted twice.
The [public completion supplement](stable-release-1.4.0-public-qualification.md)
owns exact fixture/session identity, per-case evidence and placement receipts.
The initially uploaded report and evidence archive remain immutable snapshots
of the paused phase; the supplemental archive preserves its completion.
The public native repeat required by WDG-VALREQ-015 is now executed at the
stated version and observed scope.

Deterministic source regressions cover threshold boundaries, startup grace,
successful/no-window/unavailable reset, changed HWND/PID, pause, explicit disable,
native errors, configuration capture/normalization and revision invalidation of
an uncommitted old timeout decision. All desktop runtime runs are x64, reporting
.NET 9.0.22 or CLR 4.0.30319.42000. No x86 desktop campaign is claimed.

Candidate session: `19ada7033cdc4f77bafbb75294eba1f9`.
Stable session: `16b501bb0c0e4db4b1637b7c81a577e4`.
Initial public-download session: `a8e17a048cd64544a9342ec27e9a7d5a`.
Public continuation: `public-resume-c44f993be59a4c6c837ebd840f464439`.

The first dirty development run correctly reported no_window because its launcher
hid the native window with SW_HIDE. The qualification fixture was corrected to
expose a real offscreen HWND, and the next 56-case development run passed. Its
negative attempt is retained. Dirty development artifacts are not clean-source
release provenance. Clean final gates requalify the actual integrated source.

The first stable attempt stopped at the pre-existing WPF consumer's physical
keyboard-focus assertion. The identical executable passed three focused reruns
without code changes, and the complete canonical gate was repeated. The exact
cause of that intermittent focus observation was not conclusively isolated;
Navigation implementation and the assertion were not changed or waived. The
failed attempt and focused results are retained separately from the final pass.

## Publication and immutable artifacts

[Trusted publication run 45455772938](https://github.com/Sakamoto0110/NekoLib/actions/runs/45455772938)
accepted 16 main packages and 15 symbol packages after checking the source tag,
numeric draft release and complete qualified package-set hash. The
[GitHub release](https://github.com/Sakamoto0110/NekoLib/releases/tag/v1.4.0)
contains the exact original artifacts. Prior versions remain immutable.

Original aggregate SHA-256: `3C854E5852B26C8DF7FA3A60F369DE52171150020096B606A47914876D1E776A`.
The [31-asset manifest](https://github.com/Sakamoto0110/NekoLib/releases/download/v1.4.0/SHA256SUMS-1.4.0.txt)
records bytes before NuGet signing. Repository signing changes whole-file hashes;
all other ZIP entries match. Public signed main-package hashes:

| Public main package | SHA-256 after repository signing |
|---|---|
| NekoLib.Core | `1C5434B8503E201FE4D9B1AD905292D2D5E78965AB6086A369A0DDF75757B1D6` |
| NekoLib.Data | `1ECA53243F8734FA6899C63D67816DB2F0278B8E4F230128C22E1AD268C4DCCA` |
| NekoLib.Devices | `F6C02F161BC63D31E145D1B149A149F3665340AEC56D9F64A2D316C85567A071` |
| NekoLib.Diagnostics | `406399AD8DA7CC2AF30519A8B5A8CEDEAAF5EBD737BB9074BDCAAABE9661AD56` |
| NekoLib.Diagnostics.Windows | `010C49F5334EC2328DCB54EA441097A995E6134508E2B0BDA9B0C47A7CAC0C54` |
| NekoLib.Http | `EA697EF97F93D845B63EAE565DE481237E560508712FFE453150717002872259` |
| NekoLib.Inspection | `35846AF0B865956A03208324840617CC799D1C4D6908AB72255050A8C2ACFB12` |
| NekoLib.Logging | `54C4FE079178A3F5E6F9ECB69F3AF9DFFEC56D4E6A5F81C83A03CCC880861B04` |
| NekoLib.Mvvm | `05C023E7925956AD51768F95F2A6B74F6E0B3E14745E3DD13C6D65BC6D18DF41` |
| NekoLib.Navigation | `16BC1DE1D66C7658FF7EF7E65FCEE46643D39F2FE10B1F60B30D103B07FFC603` |
| NekoLib.Navigation.WinForms | `0637BA79E65CF672729BA44C9E69242905F392464C58F787043C7E7DE88EE084` |
| NekoLib.Navigation.Wpf | `04942D6ACAD019F0522B0E09F055CE8EA776460881AC09F4FFE02C256E9C7468` |
| NekoLib.Pipes | `F1BC81A41DFCCCA1B453F5AB891F8A6D4A17F9E8C4C079C919DF02DA1FA75E42` |
| NekoLib.Telemetry | `F3F15C81327D7F97F6841C04F621F3D60E7536A00541B82E95E720CE73D21C64` |
| NekoLib.Watchdog | `4BFC2372709C5D2D45D9480ED865DBBB3E3364AB0970DD663F63E9F957EC7C76` |
| NekoLib.Watchdog.Host | `3131A971D72CA8F2CD6302702DCEA89B7404B7E051E23C57B6F9162A9249DF7A` |

The [evidence archive](https://github.com/Sakamoto0110/NekoLib/releases/download/v1.4.0/watchdog-ui-hang-evidence-1.4.0.zip)
retains canonical/API/docs/compiler logs, publication job metadata and receipts,
public verification/signature logs, the negative development attempt and scoped
development/candidate/stable native scenario results, partial public results and
the explicit GUI-stop receipt and process files.
It excludes private caches, separately attached package binaries and raw incident
bundles. Package source remains distinct from later documentation-only commits.

## Objective limits

This detector follows one observed native UI window thread per process. It does
not certify business-operation completion, all workers, every dispatcher or UI
rendering progress. An application that still handles sent window messages may
pass this probe while an application-level operation is stalled. A target with
no observable window and startup before the first automatic native-loop callback
are not covered. The 100-ms budget bounds the native message call, not an
OS-wide freeze or unbounded behavior in a caller-owned custom log sink.

Cross-user/elevation, restricted-token, mixed-runtime, x86 desktop, human visual,
designer and long-soak campaigns were not run. No SQL/ACE/TheCatAPI, devices,
sync engine, business conflict policy or final console fault protocol was added
or exercised by this Watchdog release. The existing Inspection shared-budget
timing issue INSP-ISSUE-001 remains open; this work does not change that module.
