# NekoLib.Watchdog.Host

**Kind:** reference

**Lifecycle:** current

**Subject:** Watchdog sidecar deployment package, payload selection, build and
publish behavior, bootstrap protocol, lifecycle, fatal evidence, and security

`NekoLib.Watchdog.Host` is the deployment sidecar used by
[`WatchdogBootstrap`](../NekoLib.Watchdog/README.md). It is not a compile-time
library and exports no public types. Advanced in-process supervisors use
`WatchdogRuntime` from `NekoLib.Watchdog`; they do not launch or reference Host
implementation classes.

## Installation and ownership

Reference the package directly from every executable that deploys the Host:

```xml
<PackageReference Include="NekoLib.Watchdog.Host" Version="$(NekoLibPackageVersion)" />
```

The package intentionally has no `buildTransitive` asset. A wrapper library
does not decide that each downstream executable should receive a sidecar. A
downstream executable adds its own direct reference, selects its architecture,
and calls `WatchdogBootstrap.EnsureStarted`.

Build and publish copy the selected payload to the package-owned directory:

```text
<application output>/NekoLib.Watchdog.Host/
```

The target removes and replaces that complete child directory on each build or
publish, removes it when deployment is disabled, and removes it on clean. The
package never owns the parent output directory, application settings,
credentials, target executable, working directory, runtime installation,
installer, updater, signing policy, or filesystem ACLs.

## Payload and target matrix

| Consumer target | Package payload | Runtime requirement | Architecture |
|---|---|---|---|
| `net481` | `tools/net481/` | .NET Framework 4.8.1 | managed AnyCPU IL |
| `net9.0-windows7.0` x86 | `tools/net9.0-windows7.0/win-x86/` | x86 .NET 9 Windows runtime | x86 apphost |
| `net9.0-windows7.0` x64 | `tools/net9.0-windows7.0/win-x64/` | x64 .NET 9 Windows runtime | x64 apphost |

Each payload contains `NekoLib.Watchdog.Host.exe` and the framework-dependent
runtime closure produced by `dotnet publish`. The package has no `lib/` asset.
Published PDBs are deliberate diagnostic content but their individual names are
not a compile-time or behavioral contract.

The stable consumer properties are:

- `NekoLibWatchdogHostDeploy`, default `true`, disables build and publish
  deployment when set to `false`;
- `NekoLibWatchdogHostRid`, when set, must be `win-x86` or `win-x64` for a
  modern consumer.

For modern consumers, selection uses explicit `NekoLibWatchdogHostRid`, then
`RuntimeIdentifier`, then x86 platform/prefer-32-bit settings, and otherwise
defaults to `win-x64`. Unsupported RIDs fail the build. The underscore-prefixed
MSBuild target, item, and property names are implementation details; only the
selection, destination, replacement, disable, publish, and clean outcomes are
stable.

## Bootstrap protocol v1

The executable command line is an internal protocol between the coordinated
`NekoLib.Watchdog` and `NekoLib.Watchdog.Host` packages. It is not an end-user
CLI. Host and library package versions must be updated together and the
application must be rebuilt so the owned sidecar directory is replaced.

Protocol v1 requires these exact, case-sensitive, single-occurrence options:

| Option | Meaning |
|---|---|
| `--protocol-version 1` | required Host/application protocol version |
| `--target <absolute-or-relative-path>` | existing target executable |
| `--attach-pid <positive-pid>` | already-running initial process; requires token |
| `--attach-token <value>` | correlation identity for the initial attach; requires PID |
| `--args <windows-command-line>` | original application arguments for later restarts |
| `--workdir <existing-directory>` | application-owned working directory for later restarts |

Unknown options, duplicates, missing values, unsupported protocol versions,
missing targets, invalid PIDs, incomplete PID/token pairs, and missing or
non-directory explicit working directories fail before supervision. If
`--workdir` is absent in an internal composition, the runtime defaults to the
target directory. The application owns keeping the selected directory
available for future restarts.

The runtime answers the internal `protocol_version` command with `1`. Bootstrap
checks it before accepting an attachment and reports an incompatible package
pair rather than treating the response as a timeout. Successful attachment
identity has this exact shape:

```text
attached:v1:<pid>:<attach-token>
```

PID and token correlate a specific initial process; they are not credentials.
The public `Ping`, `Status`, `Pause`, `Resume`, `Restart`, and `Stop` controller
contract remains owned by the Watchdog library reference. Other wire names are
Host/library implementation protocol.

## Lifecycle and failures

`WatchdogBootstrap.EnsureStarted` resolves the sidecar below the application
base directory, launches it hidden, and shares one bounded timeout across
preflight, process launch, protocol check, pipe I/O, and attachment identity.
Bootstrap terminates only a Host process it launched but could not confirm.
After confirmation, Host owns one `WatchdogRuntime`, blocks in `WaitForExit`,
and returns exit code `0` after orderly terminal cleanup. Any uncaught startup
or runtime failure returns `1`.

Fatal Host evidence is best-effort and never replaces the original failure. It
is written to:

```text
%LOCALAPPDATA%/NekoLib/Watchdog/watchdog-host-fatal.log
```

Entries use UTC timestamps and include the Host process ID. The active file is
bounded to 256 KiB and keeps at most one `.1` backup. An old oversized active
file is discarded before the next bounded entry. Write, directory, rotation,
and permission failures are swallowed; the bootstrap early-exit diagnostic
names the expected path but cannot guarantee that evidence was writable.

## Threading and shutdown

Host adds no worker beyond `WatchdogRuntime`. Its main thread owns construction,
start, blocking wait, disposal, and exit-code selection. Runtime monitor,
control, event, hotkey, logging, crash evidence, restart, and terminal shutdown
semantics are documented by the
[`NekoLib.Watchdog` reference](../NekoLib.Watchdog/README.md).

The application should not replace or clean the sidecar directory while a Host
from it is running. Build/publish replacement is a deployment-time operation;
process coordination during an application update remains installer/updater
policy.

## Security boundary

Watchdog RPC and event endpoints use `CurrentUserOnly` on both targets. This is
a cooperative local same-user boundary, not authentication against a hostile
process running as that user. Pipe names, PIDs, tokens, and process command
lines are identity/correlation data and must not be treated as secrets.

The application or installer owns directory ACLs, binary signing and integrity,
elevation topology, service identity, runtime provisioning, and protection
against sidecar replacement. Host adds no network transport, remote control,
credentials, authorization framework, service installation, self-supervision,
or update orchestration.

## Package verification

The canonical clean flow is:

```powershell
.\eng\pack-local.ps1 -PackageVersion <new-immutable-version>
```

It publishes all three payloads, packs the coordinated family, and runs
PackageReference-only probes. Host-specific probes verify required and
forbidden layout, no `lib/` asset, AnyCPU/x86/x64 PE metadata, default and
explicit selection, unsupported RID failure, direct-only deployment,
build/publish/disable/re-enable/replacement/clean behavior, protocol mismatch,
and real package-backed startup/stop on both targets. Package evidence is valid
only for its recorded version, repository commit, and hashes.
