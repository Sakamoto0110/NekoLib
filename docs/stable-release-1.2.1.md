# NekoLib 1.2.1 Stable Desktop Family Release

**Kind:** reference

**Lifecycle:** current

**Subject:** coordinated desktop readiness release, exact package provenance and bounded qualification

**Release date:** 2026-11-24

**Package source:** `85c1fc8227edc7b5c6ba643bb48dc4afc88922c0`

The coordinated **1.2.1** family contains 16 packages, including the deployment
package `NekoLib.Watchdog.Host`. Align their versions and reference Host directly
from the executable project. [PR 16](https://github.com/Sakamoto0110/NekoLib/pull/16)
introduced the compatible desktop APIs and corrections;
[PR 17](https://github.com/Sakamoto0110/NekoLib/pull/17) restored modal ownership
across native enabled-state binding refreshes. The annotated `v1.2.1` tag fixes
the exact clean source recorded in every package nuspec. Later documentation
commits do not change those package bytes.

## Compatibility and delivered behavior

The [desktop integration decision](desktop-readiness-1.2.0.md) owns the
17-corresponding-class adapter review and the additive API/migration choices.
The [modal binding correction](desktop-readiness-1.2.1.md) owns the compatible
patch disposition. Existing constructors and legacy observation/crash defaults
remain available; 1.2.1 changes no public API or accepted manifest.

- Scoped WPF exception forwarding distinguishes recoverable handling from fatal
  propagation, shares compatible leases and detaches on final disposal/shutdown.
  The Diagnostics fatal latch prevents a second fatal report. Reporting does
  not exit the process: application policy decides termination.
- Dispatch, worker focus and asynchronous bootstrap teardown preserve native UI
  ownership. Await Navigation shutdown before destroying the host's dispatcher.
  The void dispatch contract still cannot acknowledge posts abandoned by native
  shutdown.
- Explicit observer modes give both UIs the same activity policy while retaining
  their legacy defaults. Programmatic changes count only in the broader mode.
- Native toast routing, child close controls and coordinate units remain
  documented framework semantics. Real native probes cover focus, auto-dismiss,
  modal ownership, enabled binding refresh/restoration, prompts and popovers.
- Watchdog remains external and UI-independent. Forced termination of the
  initially attached app preserves the supervisor's branch; replacement tree
  termination still uses the absolute system taskkill path. Restart recreates
  the application's **single main window**. Heartbeat measures process
  existence, not UI responsiveness.
- Data's SQL Server/Access translators, exact positional parameters and
  transaction/rollback behavior support application composition. Pending work,
  replay identity, duplicate/conflict policy and interrupted synchronization
  recovery remain application business rules. No synchronization engine or
  future console-control protocol was introduced.

## Executed qualification

All runs used Windows x64. The SDK was 10.0.403, with installed .NET 9 SDK
9.0.319; modern native consumers actually ran on .NET 9.0.22, and net481
consumers reported CLR 4.0.30319.42000. No runtime installation was necessary.

| Gate | Exact result |
|---|---|
| Clean candidate `1.2.1-local.2` | 1,837 tests, 24 suites, zero failed/skipped; complete package/consumer gate passed |
| Clean stable `1.2.1` | 1,837 tests, 24 suites, zero failed/skipped; 16 main and 15 symbol packages produced |
| API and documentation | All 30 manifests matched; no patch baseline update; documentation and normalized warning verification passed |
| Package contents and Host | Managed assembly/XML pairs, dependencies/provenance, direct Host deployment, target/RID negative guards, protocol startup/stop, rebuild and clean probes passed |
| Independent public download | All 16 main packages verified: nuspec identity/version/source, every uncompressed entry except NuGet's added signature, and `dotnet nuget verify --all` |
| Downloaded-package consumption | Fresh feed and private NuGet cache; all four behavioral consumers and 24 recovery scenarios passed |

| Native consumer | Produced 1.2.1 behavior | Produced recovery | Downloaded 1.2.1 behavior | Downloaded recovery |
|---|---|---|---|---|
| WinForms net481 | PASS | 6/6 | PASS | 6/6 |
| WinForms net9.0-windows | PASS | 6/6 | PASS | 6/6 |
| WPF net481 | PASS | 6/6 | PASS | 6/6 |
| WPF net9.0-windows | PASS | 6/6 | PASS | 6/6 |

Each recovery set executes UI fatal exception, worker fatal exception, external
kill, explicit restart, recoverable UI exception and clean stop. The driver
asserts one initial/replacement window, process identity changes, exactly one
fatal report and finalized bundle where applicable, fatal exit code 71,
recoverable continuation without restart, and owned process/Host cleanup.
Restart/stop cases veto graceful close and spawn a **windowless test helper**;
the helper must terminate while the supervisor survives. Both the protected
initial tree path and executed taskkill replacement-tree path are observed.
These are short bounded qualification scenarios, not the historical long soak.

Shared package-only module probes exercise Core, Logging, Telemetry,
Inspection, Diagnostics, Mvvm, Devices framing, mock HTTP retry/typed cats,
Data transactions and both translators, and real same-user Pipes RPC. Native
Navigation and real deployed Watchdog/Host behavior complete the family. Fake
ADO.NET/HTTP/device inputs do not constitute live provider/API/hardware tests.

## Provenance and publication

The [trusted-publication run 45329130534](https://github.com/Sakamoto0110/NekoLib/actions/runs/45329130534)
completed successfully and accepted all 31 pushes (16 main packages and 15
symbol packages). It consumed the qualified immutable assets from draft release
ID **456319975**, checked their aggregate hash and tag/source provenance,
then used a short-lived NuGet credential. No public artifact was overwritten.
The [GitHub release](https://github.com/Sakamoto0110/NekoLib/releases/tag/v1.2.1)
and [NuGet family](https://www.nuget.org/packages/NekoLib.Navigation.Wpf/1.2.1)
identify this coordinated patch.

Candidate consumer session: `76431432913442d39fe0366da84c2c95`.
Stable consumer session: `ee3a2cd3ef5c4adb86c0796f2ff9d91f`.
Downloaded-package consumer session: `1013a79ad8114e6f84c9dc033567d3da`.

Original 31-asset aggregate SHA-256: `BFE733AD2BAD04A085CBA255CC1C83A69A945B8819D3B610BBD55ABDB6FBC12E`.

[SHA256SUMS-1.2.1.txt](https://github.com/Sakamoto0110/NekoLib/releases/download/v1.2.1/SHA256SUMS-1.2.1.txt) records every pre-signing main/symbol hash. NuGet repository signing changes whole-file bytes; the independently downloaded hashes below are therefore recorded separately. Every non-signature ZIP entry matched the qualified artifact.

| Downloaded package | SHA-256 after NuGet repository signing |
|---|---|
| NekoLib.Core | `06923A09DF800D6EF58E693A815954702F22218998C47ACE4DC5E49A2CB2F82C` |
| NekoLib.Data | `115DEF5211F9801960A1FD4214F7C74AC67417C9C188B8D2AFC8658030FAA7F9` |
| NekoLib.Devices | `EB2E9D00C668BEC432D0B89666E2DC0AA021B58CD8006370BE4ABB0FA2EE3DF9` |
| NekoLib.Diagnostics | `429034DD89745FAE43B5D3CE8A2639A7A6072AB2D0FAFC6F177ACAAFAE1FB06E` |
| NekoLib.Diagnostics.Windows | `D068B1111C3CE299357BAEC3EBE2310EF1461682A0139A385738451A8D2B5081` |
| NekoLib.Http | `F326B046D96AA11CAA2E640D39B0BA3EEADA9DA9FFFA6DD23C097A2A924403B8` |
| NekoLib.Inspection | `9547970E31794CD323580DFB30412FFBACDBE4443487952D9E08CD5138D57785` |
| NekoLib.Logging | `DB5EE6AA93CD0FED5262F07D02180BAE619B21A8C983F770EECF4FA18C41F084` |
| NekoLib.Mvvm | `ACEE945EEF01041F5D8BD97C1452E4ABB23E387374E4FF972F200E7230B57290` |
| NekoLib.Navigation | `F67B6FFD67A59369AD49F511644DC4686697C45393B93F71B8B13071CA6A73F0` |
| NekoLib.Navigation.WinForms | `52120168E9602418834E8AE52426FA335CDFD5FC5376CA72D7C5424573CEF18E` |
| NekoLib.Navigation.Wpf | `C9B7F45C0719CF34202A9E57AADE7DB98E7A81B2FC734C4D72C77CA963BFF1F6` |
| NekoLib.Pipes | `473F17258B4310D5370B82327ADD6EB646139EA7FA2473102DC28A310F187345` |
| NekoLib.Telemetry | `9A45E86DA2A8A87140F4CBEAFE54980E64CB0AD626B663BF4E1B62FEC7D576E7` |
| NekoLib.Watchdog | `5D841D2AF98DAC6FC5A671AD019C37C9085F25DF6EC9513C6BCC71F4EFEC1BEC` |
| NekoLib.Watchdog.Host | `FB4BCEF84320AA3091F21D41D45C211E2F01571C1DE04752F86EB1D3588B3457` |

The intermediate 1.2.0 family was accepted in
[run 45323598956](https://github.com/Sakamoto0110/NekoLib/actions/runs/45323598956)
from source `25ac421678089de3ad1a140338a6591e33e90383`. Its original four-way
probes/24 recovery cases and 16 signature/payload checks passed. A later added
binding-refresh stress case then failed against signed 1.2.0 WPF and WinForms
packages; those negative logs motivated 1.2.1. Version 1.2.0 remains immutable
and superseded. The first 1.2.0 publication attempt, run 45323300842, failed
before NuGet login/push because draft asset reads required GitHub contents
write permission. The corrected workflow uses the numeric draft release ID and
verifies the source tag and package-set hash before publication.

## Evidence and limits

The [release evidence archive](https://github.com/Sakamoto0110/NekoLib/releases/download/v1.2.1/desktop-readiness-evidence-1.2.1.zip)
preserves canonical candidate/stable logs, all 30 API checks, documentation and
warning checks, prerequisite inventory, both published 1.2.0 negative cases,
the 1.2.1 payload/signature verification manifest and logs, publication job
metadata, and candidate/stable/downloaded recovery result sets with their
scoped process logs. It excludes caches, package binaries already attached to
the release, and raw incident bundles. Local originals remain under ignored
`artifacts/e2e-readiness/` and `artifacts/package-smoke/sessions/`.

The native behavioral gate covers WPF net481 as well as the other three
combinations. It does not claim human visual/accessibility approval or designer
interaction. The explicit unattended-release exception in the desktop decision
preserves those distinctions and the historical long-duration gaps. Watchdog
cross-user/elevation, restricted-token, global hotkey and mixed-runtime pairing
campaigns were not run. UI hang detection is not implemented by heartbeat.

Merged user/machine ClassesRoot registration probes found neither ACE 12 nor
ACE 16 in the 32-bit or 64-bit registry view. No local SQL Server service or
LocalDB command was found. No live database connection, TheCatAPI request,
physical device test or SQL/ACE installation was attempted. ACE is a separate
native prerequisite whose bitness must match the eventual application. The
future application's schema, credentials, synchronization/conflict rules and
console-driven fault actions remain inputs for its own implementation.
