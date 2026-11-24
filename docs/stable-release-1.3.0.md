# NekoLib 1.3.0 Stable Watchdog Family Release

**Kind:** reference

**Lifecycle:** current

**Subject:** same-executable automatic desktop supervision, optional application control and exact release evidence

**Release date:** 2026-11-24

**Package source:** `39450335adfb007a13123d331399e84e7048f314` (`v1.3.0`)

The coordinated family contains 16 main and 15 symbol packages. The
[merged change](https://github.com/Sakamoto0110/NekoLib/pull/18) adds opt-in
Watchdog hosting without a separate supervisor executable or mandatory Main
calls. It is a compatible minor: only the two Watchdog manifests add the
accepted types/members; the other 28 manifests remain unchanged. Existing
manual sidecar bootstrap and Stop retain their behavior. The
[accepted contract](watchdog-auto-bootstrap.md) and
[technical reference](modules/Watchdog/REFERENCE.md#supported-composition-paths)
own the detailed startup, configuration and diagnostic rules.

## Consumer setup and behavior

Reference `NekoLib.Watchdog` directly at 1.3.0 and enable this in the executable:

```xml
<PropertyGroup>
  <NekoLibWatchdogAutoBootstrap>true</NekoLibWatchdogAutoBootstrap>
</PropertyGroup>
```

`SameExecutable` is the default hosting mode. The application executable runs
in a second, headless supervisor process; no Host payload or companion window
is deployed. The generated STA entry handles supervisor mode before original
application entry code. Normal application mode activates on first WinForms
idle or a WPF Loaded-priority callback. No Main call is required. Optional
startup code before that callback can adjust portable runtime policy:

```csharp
WatchdogAutoBootstrap.Configure(options =>
{
    options.RuntimeOptions.RestartDelayMs = 1500;
});
```

`WatchdogAutoBootstrap.Disable()` cancels pending activation or detaches active
supervision while leaving the application running. It is idempotent and may be followed by explicit Enable in the same execution. `WatchdogRuntime.Detach()` provides the corresponding
advanced runtime operation. Stop still terminates the supervised target.
Static WatchdogController.Enable/Disable/Shutdown expose the global application
controls. Enable starts a fresh session after Disable; Configure is allowed while
disabled. Shutdown first confirms detach, then calls Environment.Exit with the
requested code, including from a worker. Finish application-owned cleanup first.
Configuration is copied per supervisor session; Configure while active is rejected.

Normal return from the original Main detaches. Escaping exceptions, abrupt
exit and external termination leave recovery to the supervisor. A supervisor
stop/restart takes precedence over automatic normal-return cleanup. Internal
bounded wait slices keep detach responsive with a large configured poll interval;
the configured heartbeat observation boundary remains unchanged.

Automatic startup failures warn through isolated Warning subscribers, Trace,
standard error and bounded per-user `auto-bootstrap.log`, leaving the app
running without claiming confirmed supervision. Invalid reserved supervisor
invocation returns 1 before opening application UI. Build diagnostics reject
invalid/missing integration and unsupported entry points; NEKOWDG003 warns
about direct manual EnsureStarted overlap. Build assets must remain enabled.

`NekoLibWatchdogHostingMode=Sidecar` retains the separately deployed Host option
and requires a direct same-version Host reference. Referencing the Watchdog
package alone never activates supervision. Align all NekoLib family versions.

## Executed qualification

| Gate | Observed result |
|---|---|
| Clean final candidate and stable source | 1,873 tests, 24 suites, zero failed/skipped; 126 Watchdog tests per target |
| API/docs | All 30 manifests match; only the reviewed two Watchdog baselines changed; documentation and normalized warning checks pass |
| Legacy package integration | Existing four native module consumers and 24 recovery cases pass |
| Automatic package integration | 84 native process scenarios, four original-entry opt-outs and explicit build-guard rejection checks pass |
| Minimum compiler compatibility | .NET SDK 9.0.319 with C# 7.3 builds and routes generated net481/net9.0-windows entry points from exact stable packages |
| Independent public downloads | 16 main packages pass version/source provenance, every non-signature ZIP-entry comparison and repository-signature verification |
| Public package consumption | Fresh feed/cache repeats all native behavior, opt-out, build guards and recovery checks |

| Native UI / target | Same-executable scenarios | Optional sidecar scenarios | Original-entry opt-out | Legacy recovery | Stable / public |
|---|---|---|---|---|---|
| WinForms net481 | 15/15 | 6/6 | PASS | 6/6 | PASS / PASS |
| WinForms net9.0-windows | 15/15 | 6/6 | PASS | 6/6 | PASS / PASS |
| WPF net481 | 15/15 | 6/6 | PASS | 6/6 | PASS / PASS |
| WPF net9.0-windows | 15/15 | 6/6 | PASS | 6/6 | PASS / PASS |

Same-executable cases cover defaults without configuration code, configured
normal return, cancellation before/after activation, explicit re-enable before
the loop, repeated global disable/configure/enable, worker shutdown while active
or disabled, fail-open warning, fatal exit 71, external kill, forced/graceful
restart, stop and invalid invocation. Fatal replacements also toggle supervision
to verify re-enabling after an inherited supervision marker.
The driver verifies Unicode/quoted/empty/trailing-backslash arguments before
and after replacement, one initial/replacement native window, a headless
supervisor using the exact application executable, absence of Host payload,
configured log transfer and complete process/endpoint cleanup. Invalid invocation
does not consume an unowned file. Sidecar cases repeat normal return, active
detach, repeated re-enable, shutdown while active/disabled and fatal recovery. Opt-out preserves original Main on both targets.
Invalid hosting and explicitly excluded analyzers must fail with NEKOWDG001.

All desktop runtime runs were x64. Framework consumers report CLR
4.0.30319.42000; modern consumers report .NET 9.0.22. Private source-generator
tests additionally execute private/async/no-argument entry routing, original
exception propagation, Environment.ExitCode, unsupported entries, configuration
copy/ownership and warning subscriber isolation. The in-process RPC fixture
reserves worker capacity because the Framework pipe adapters and client share
one test host; real external-supervisor qualification uses normal product defaults.

Candidate session: `68e6570b2df44d4fa5d5ee0b5d43560e`.
Stable session: `9d28ac36b409437e8db34aaebad1017b`.
Downloaded-package session: `bc34adf0c7624fe1a92f2a47caef935e`.

## Publication and immutable artifacts

[Trusted-publication run 45430833157](https://github.com/Sakamoto0110/NekoLib/actions/runs/45430833157)
accepted all 31 pushes after checking the numeric draft release, source tag and
qualified package-set SHA-256. The public
[GitHub release](https://github.com/Sakamoto0110/NekoLib/releases/tag/v1.3.0)
contains those exact original artifacts. Existing 1.2.x packages remain immutable.

Original package-set aggregate SHA-256: `625755550776042BFFC3AFD216EC8CBB5AEFE6ACC508697CF800792304142D7C`.
The [31-asset manifest](https://github.com/Sakamoto0110/NekoLib/releases/download/v1.3.0/SHA256SUMS-1.3.0.txt)
records main/symbol hashes before NuGet signing. NuGet's repository signature
changes whole-file bytes; every other ZIP entry matches. Public signed hashes:

| Public main package | SHA-256 after repository signing |
|---|---|
| NekoLib.Core | `7921724329B0086C87B4DA35C76898F129D41E378FE1D93D0AD7DC5891754C05` |
| NekoLib.Data | `9CC4D991AF1DBAF6E8868A09E3C67F189A898A1007520CCE03C013BA19DED039` |
| NekoLib.Devices | `7FE16AAAF5BBD13214A97F6F9B0CA0FF7FC5DB63ABB7A1ABD2BD22F1FED10A84` |
| NekoLib.Diagnostics | `34D0F0ACFDDECB392119222FDA8508829738F42864E2B639593DD752AEFFD69C` |
| NekoLib.Diagnostics.Windows | `9CCE20E257E2BC635031C9A66AA05066426FF42610192F46F021C858CD01E3E8` |
| NekoLib.Http | `75E3A94A52D9302894EA642DEEC663B51AB2C03DDD6EDC1FB56CECEDB4A69174` |
| NekoLib.Inspection | `4D80581703C5BB3BB0D11569A2C6B3A72639C0C1B6BE47AF11B916229C8858AA` |
| NekoLib.Logging | `15E8E71A4CA561EA3EB0DE20BDAABC12DECE5809C4121A006E53818E818B4150` |
| NekoLib.Mvvm | `2980CE080F72AC5B953E65B780E9E203FFCC1BBC0CA938262595E1D6C45FF1CF` |
| NekoLib.Navigation | `C453977AA9BD27AB7D1C6A98281B3D1C0BABDE04CD4AD828007059179280D0A9` |
| NekoLib.Navigation.WinForms | `C842FCE24C07E48B3D61A17E3E529474C6B03D25176FFE1C9A6F5C8F06A12C29` |
| NekoLib.Navigation.Wpf | `4DF4E77C38584073C9ED73C0E48A4475ED57340C4A10EEB3F4814BE1C7BE826F` |
| NekoLib.Pipes | `A4B5914B57B941107442CC2234631B4B2CFC893284920EBB1E99AFEF912DFFF3` |
| NekoLib.Telemetry | `7B60C91621A4666D8B5FAADDA920EC626FB96DDE08461C1B53717B507806999C` |
| NekoLib.Watchdog | `B58EAB7DA03D1EFC8BF625D8C59F2D2181BADE972100DE2B83C5FE2225412BB9` |
| NekoLib.Watchdog.Host | `47A4F838C3D3FD5AF26C87D756DE629ABE3A1CA90BE1D722CAAD206C1BB57D31` |

The [evidence archive](https://github.com/Sakamoto0110/NekoLib/releases/download/v1.3.0/watchdog-auto-bootstrap-evidence-1.3.0.zip)
retains canonical logs, API/docs checks, compiler compatibility, public download
verification/signature logs, publication job metadata and scoped candidate,
stable and public scenario result/process logs. It excludes caches, binaries
already attached separately and raw incident bundles. Package source is distinct
from later documentation-only commits.

## Objective limits

This release implements hosting and automatic startup. It **does not implement
automatic UI-hang detection**: heartbeat measures process existence. Initialization
before the first supported UI callback is not a supervised responsiveness window.
The supervisor remains a separate process; embedding a monitor in its own target
cannot survive that target's process-wide failure. CLR module initializers run
in both processes, so application-only side effects must not be placed there.

Top-level statements, trimming and NativeAOT are rejected by this first integration.
Explicitly excluding all build assets also excludes their validation hooks.
Cross-user/elevation, restricted-token, mixed-runtime, x86 desktop, human visual,
designer and long-soak campaigns were not run. Application synchronization rules,
future console fault actions, SQL/ACE/TheCatAPI and physical devices were not
added or exercised by this Watchdog work. The prior desktop release's provider
prerequisite and live-system limitations remain independently scoped.

An earlier clean candidate stopped on the pre-existing Inspection sub-millisecond
shared-budget edge (INSP-ISSUE-001); its focused rerun passed on both targets.
Inspection code/tests were not changed in this Watchdog scope. The final full
family gate passed, but this known timing issue remains open and its negative
attempt is preserved. Another candidate exposed the analyzer-exclusion guard;
explicit package metadata validation corrected that integration diagnostic.
