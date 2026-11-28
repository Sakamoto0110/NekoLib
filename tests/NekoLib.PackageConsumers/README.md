# Package-only desktop qualification

**Kind:** guide

**Lifecycle:** current

**Subject:** isolated NuGet consumption and desktop recovery qualification

Run `eng/pack-local.ps1 -PackageVersion <new-version>` from a clean checkout.
The canonical flow builds and tests the family, then calls
`eng/test-local-packages.ps1` with a fresh private NuGet cache and explicit feed.
Keep evidence with `-KeepIntermediateArtifacts`. No consumer uses a product
ProjectReference. The shared fake ADO.NET provider is test source only.

The four x64 executables cover WPF/WinForms on net481/net9.0-windows. Default
execution checks Logging, Telemetry, Inspection and Diagnostics composition;
Mvvm notifications; Devices raw protocol; deterministic Http offline/online
responses; SQL Server and Access translation with named/positional ADO.NET
binding, transactions and failure propagation; a real current-user Pipes RPC;
native Navigation, worker focus, explicit activity policy, anchored toast
auto-dismiss, dialog/prompt/popover completion and awaited UI-thread cleanup.
Core contracts are exercised through those integrations. Watchdog and its
directly referenced Host deployment package are exercised by recovery.

For family 2.0 and later, `DataLegacyOverloads` verifies the removed QueryBuilder
calls through a package reference. The baseline fixture builds for `net481`
and `net9.0`. Each legacy call is then compiled independently on each target
and must fail. Dictionary INSERT/UPDATE and condition-template WHERE require
`CS1501`; the removed Join requires `CS1061`. All eight rejections are part
of `eng/test-local-packages.ps1`. The native consumers compile migrated calls
and exercise the replacement query path.

`eng/test-desktop-recovery.ps1` runs six bounded scenarios per executable:
fatal dispatcher exception, fatal worker exception, external process kill,
explicit restart, recoverable dispatcher exception with a subsequent UI
callback, and clean Host stop. It asserts initial and replacement single-window
counts, changed process identity, exactly one incident report/bundle where
expected, handling policy, Host status, and process/Host cleanup. The two fatal
scenarios and the restart/stop scenarios are distinct. Restart/stop veto native
close requests and spawn a test-owned windowless helper; the driver checks that
forced termination kills that child while preserving the Host, and records both
attached-branch and replacement `taskkill` execution in Host logs. The helper is
qualification infrastructure, not a companion window or application feature.
The two fatal
scenarios explicitly exit with code 71 after incident collection. This is
application policy; neither reporting nor a heartbeat means automatic UI-hang
detection. Recovery flags are qualification-only and are not a product console
control protocol.

For an already produced feed:

```powershell
./eng/test-local-packages.ps1 -PackageVersion <version> -FeedPath <absolute-feed> -KeepArtifacts
```

For independent remote verification, first download all published NekoLib
packages into a new empty feed and pass that feed with another new consumer
cache. Package provenance and payloads must also be compared against the
qualified family; a successful restore alone does not prove package identity.

An interactive Windows session is required. Qualification windows default to
off-screen coordinates. To select a monitor, set both
`NEKOLIB_QUALIFICATION_WINDOW_X` and `NEKOLIB_QUALIFICATION_WINDOW_Y` in the
driver environment before launching the tests (for example, `2700` and `100`).
The executables position their own initial, replacement and native modal windows
without moving the pointer or requesting focus. Set
`NEKOLIB_QUALIFICATION_WINDOW_EVIDENCE` to an evidence directory to record and
assert each native window rectangle. Restarted children inherit these settings.
These are qualification settings, not package behavior or application APIs.
`eng/test-desktop-recovery.ps1 -SkipPassedResultsPath <prior-results.json>`
skips matching passed executable/framework/mode identities while retaining the
original evidence; its new result file contains only the newly executed cases.
Both desktop recovery and automatic-bootstrap drivers accept `-Resume` to
retain their own passed records and skip those identities. Resume only with
the same sealed package inputs, output paths and evidence root; the caller
must verify package identity. Scenario directories use compact GUID names
so Framework evidence files stay within legacy path-length limits.
net481 targeting/runtime support and the .NET 9 Windows
Desktop runtime must be installed. The deterministic HTTP handler contacts no
external API. The ADO.NET fake does not prove live SQL Server or ACE behavior.
Use `eng/check-desktop-prerequisites.ps1` to inventory local provider bitness.
Actual TheCatAPI credentials, SQL endpoint/schema, Access file, ACE runtime,
application synchronization policy and final console controls are separate
inputs. This harness does not implement pending-operation or conflict rules.
