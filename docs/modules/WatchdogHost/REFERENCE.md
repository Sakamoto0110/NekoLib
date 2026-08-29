# NekoLib.Watchdog.Host — Technical Reference

**Document ID:** WDGHOST-REFERENCE

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** normative deployment and protocol contract for the NekoLib.Watchdog.Host boundary

**Surface:** technical-reference

**Boundary:** watchdog.host

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

`NekoLib.Watchdog.Host` is the deployment sidecar used by
[`WatchdogBootstrap`](../Watchdog/REFERENCE.md). It is not a compile-time
library and exports no public types. Advanced in-process supervisors use
`WatchdogRuntime` from `NekoLib.Watchdog`; they do not launch or reference Host
implementation classes.

Because the compiled assemblies export nothing, there is no accepted API
manifest for this package and none should be created. Its public contract is
everything below: the payload roots, the consumer MSBuild properties, the
deployment destination and its replacement behavior, protocol v1, the exit
codes, and the fatal-evidence path.

## Installation and ownership

Reference the package directly from every executable that deploys the Host:

```xml
<PackageReference Include="NekoLib.Watchdog.Host" Version="$(NekoLibPackageVersion)" />
```

The package intentionally has no `buildTransitive` asset. A wrapper library does
not decide that each downstream executable should receive a sidecar. A
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
installer, updater, signing policy, or filesystem ACLs, and it must never delete
outside its fixed child directory.

## Payload and target matrix

| Consumer target | Package payload | Runtime requirement | Architecture |
|---|---|---|---|
| `net481` | `tools/net481/` | .NET Framework 4.8.1 | managed AnyCPU IL |
| `net9.0-windows7.0` x86 | `tools/net9.0-windows7.0/win-x86/` | x86 .NET 9 Windows runtime | x86 apphost |
| `net9.0-windows7.0` x64 | `tools/net9.0-windows7.0/win-x64/` | x64 .NET 9 Windows runtime | x64 apphost |

Each payload contains `NekoLib.Watchdog.Host.exe` and the framework-dependent
runtime closure produced by `dotnet publish`. All three close over
`NekoLib.Watchdog`, `NekoLib.Core`, and `NekoLib.Pipes`; only the `net481`
payload additionally carries `Newtonsoft.Json`, because the modern Watchdog
asset has no such dependency. Published PDBs are deliberate diagnostic content
but their individual names are not a compile-time or behavioral contract.

The package has no `lib/` asset, so it delivers no compile-time reference, no
managed XML documentation, and no symbol package. Packing is conditional on the
`NekoLibWatchdogHostPayloadRoot` property, so a plain `dotnet pack NekoLib.sln`
deliberately omits this package; `eng/pack-local.ps1` publishes the three
payloads first and is the canonical entry point. A pack attempted without one of
the three published executables fails with an explicit error naming the missing
payload. Package validation and the `NU5128` dependency-group warning are
disabled on purpose: this is a tools/build package that has dependency groups
but must not expose its executable as a compile-time asset.

## Consumer properties and payload selection

The stable consumer properties are:

- `NekoLibWatchdogHostDeploy`, default `true`, disables build and publish
  deployment when set to `false`;
- `NekoLibWatchdogHostRid`, when set, must be `win-x86` or `win-x64` for a
  modern consumer.

For modern consumers, selection uses explicit `NekoLibWatchdogHostRid`, then a
`RuntimeIdentifier` ending in `-x86` or `-x64`, then an x86 `PlatformTarget`,
`Platform`, or `Prefer32Bit`, and otherwise defaults to `win-x64`. A `net481`
consumer takes the single AnyCPU payload and ignores RID selection entirely.

Validation runs before deployment and fails the build when:

- the consumer's framework identifier is neither .NET Framework nor .NET Core;
- a modern consumer supplies an unrecognized `RuntimeIdentifier` — one that is
  not `win…-x86` or `win…-x64` — while `NekoLibWatchdogHostRid` is unset;
- the consumer targets ARM64 through `PlatformTarget` or `Platform`, which is
  rejected even when `NekoLibWatchdogHostRid` is set explicitly;
- the resolved RID is neither `win-x86` nor `win-x64`; or
- the selected payload executable is absent from the package.

Two limits of that validation are worth stating explicitly. It is gated on
deployment being enabled, so with `NekoLibWatchdogHostDeploy=false` an
unsupported RID does not fail the build, because nothing is being deployed. And
an explicit `NekoLibWatchdogHostRid` suppresses the unrecognized-`RuntimeIdentifier`
rejection — it is an override, not a hint — while the ARM64 platform rejection
applies regardless. Setting the property therefore accepts responsibility for
the pairing.

Payload selection also maps the consumer's framework identifier without checking
its version: any .NET Framework consumer receives the `net481` payload and any
.NET Core consumer receives the `net9.0-windows` payload. Only .NET Framework
4.8.1 and .NET 9 Windows consumers are supported; other versions are not
rejected at build time and fail when the Host is launched.

The underscore-prefixed MSBuild target, item, and property names are
implementation details; only the selection, destination, replacement, disable,
publish, and clean outcomes are stable.

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

Parsing rejects, before any supervision begins: an empty command line, an
unknown option, a duplicate option, an option missing its value, an absent or
unsupported `--protocol-version`, an absent `--target`, a `--target` that is not
an existing file, a non-numeric or non-positive `--attach-pid`, either half of
the PID/token pair without the other, a `--workdir` that names a file, and a
`--workdir` directory that does not exist. Each failure is an exception at the
executable boundary and therefore exit code `1`.

If `--workdir` is absent in an internal composition, the runtime defaults to the
target's directory. The application owns keeping the selected directory
available for future restarts; a directory deleted later is a restart-time
failure reported by the runtime's existing evidence, not by the Host boundary.

The runtime answers the internal `protocol_version` command with `1`. Bootstrap
checks it before accepting an attachment and reports an incompatible package
pair rather than treating the response as a timeout. Successful attachment
identity has this exact shape:

```text
attached:v1:<pid>:<attach-token>
```

PID and token correlate a specific initial process; they are not credentials.
The public `Ping`, `Status`, `Pause`, `Resume`, `Restart`, and `Stop` controller
contract remains owned by the [Watchdog library reference](../Watchdog/REFERENCE.md).
Other wire names are Host/library implementation protocol.

## Lifecycle, exit codes, and cleanup

`WatchdogBootstrap.EnsureStarted` resolves the sidecar below the application
base directory, launches it hidden, and shares one bounded timeout across
preflight, process launch, protocol check, pipe I/O, and attachment identity.
Bootstrap terminates only a Host process it launched but could not confirm.

After confirmation the Host owns exactly one `WatchdogRuntime`. Its single
main thread constructs the runtime, starts it, blocks in `WaitForExit`, disposes
it, and selects the exit code. The Host adds no worker thread, timer, or event
loop of its own.

| Exit code | Meaning |
|---|---|
| `0` | the runtime reached orderly terminal cleanup |
| `1` | any uncaught argument, startup, or runtime failure |

Because disposal runs through the runtime's terminal path, a Host that exits
normally has already terminated its supervised target and released the control
endpoints and the per-target instance guard. A Host killed externally does not:
the supervised child survives, and the next bootstrap re-establishes
supervision. Child-process ownership, the graceful and forced termination
bounds, and thread draining are all owned by the
[Watchdog library reference](../Watchdog/REFERENCE.md) and are not restated here.

The application should not replace or clean the sidecar directory while a Host
from it is running. Build and publish replacement is a deployment-time
operation; process coordination during an application update remains
installer or updater policy.

## Fatal startup evidence

Fatal Host evidence is best effort and never replaces the original failure. It
is written to:

```text
%LOCALAPPDATA%/NekoLib/Watchdog/watchdog-host-fatal.log
```

Entries use a round-trip UTC timestamp and include the Host process ID followed
by the full exception text. One entry is truncated to a character count chosen
so that even worst-case UTF-8 encoding cannot exceed the file bound. The active
file is bounded to 256 KiB and keeps at most one `.1` backup: when the next
entry would cross the bound, an existing backup is deleted and the active file
becomes the new backup. An active file that is already larger than the bound is
discarded instead of rotated, which also means the previous backup is gone —
that path trades old evidence for a bounded file, by design.

Directory creation, write, rotation, and permission failures are all swallowed.
The bootstrap early-exit diagnostic names the expected path but cannot guarantee
that evidence was writable, so an absent log is not proof that the Host did not
run.

## Security boundary

Watchdog RPC and event endpoints use `CurrentUserOnly` on both targets. This is
a cooperative local same-user boundary, not authentication against a hostile
process running as that user. Pipe names, PIDs, tokens, and process command
lines are identity and correlation data and must not be treated as secrets — the
attach token in particular is visible in the Host's command line to any
same-user process that can enumerate it.

The application or installer owns directory ACLs, binary signing and integrity,
elevation topology, service identity, runtime provisioning, and protection
against sidecar replacement. Because deployment replaces the owned directory on
every build and publish, an attacker who can write to the application output can
also replace the Host binary; nothing in this package detects or prevents that.

The Host adds no network transport, remote control, credentials, authorization
framework, service installation, self-supervision, or update orchestration.

## Non-goals

This package does not provide: ARM64 or self-contained payloads, one package ID
per RID, MSI/MSIX installation, a service wrapper, Host self-supervision or
self-restart, update orchestration, an end-user command-line interface, a
compile-time API, managed XML documentation, a symbol package, or transitive
deployment through wrapper packages. Each was considered and rejected in the
recorded [contract review](audits/contract-review-2026-08-20.md); admitting one
requires a new accepted decision rather than a new property.

## Package verification

The canonical clean flow is:

```powershell
.\eng\pack-local.ps1 -PackageVersion <new-immutable-version>
```

It publishes all three payloads, packs the coordinated family, and runs
`PackageReference`-only probes. Host-specific probes verify the required and
forbidden package layout, the absence of any `lib/` asset, AnyCPU/x86/x64 PE
metadata for the three payload executables, SHA-256 identity between each
deployed file and its package entry, default x64 and explicit x86 selection,
unsupported-RID build failure, direct-only deployment through a wrapper-package
consumer that must receive nothing, build, publish, disable, re-enable,
stale-directory replacement and clean behavior, the absence of `Newtonsoft.Json`
from the modern payload, protocol mismatch, and real package-backed startup and
cooperative shutdown on both target families.

Package evidence is valid only for its recorded version, repository commit, and
hashes. [`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md) owns the
evidence contract derived from the profile the [manifest](MANIFEST.md) inherits,
and [`VALIDATIONS.md`](VALIDATIONS.md) records what actually ran and what did
not. Confirmed defects live in [`ISSUES.md`](ISSUES.md) and unverified
observations in [`FINDINGS.md`](FINDINGS.md); neither is scheduled work until it
is promoted to [`TODO.md`](../../../TODO.md).
