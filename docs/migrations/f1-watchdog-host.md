# F1-WDOG-HOST Migration — Watchdog Host

**Kind:** guide

**Lifecycle:** current

**Subject:** migration from the initial Watchdog Host candidate package to the
accepted direct-deployment and protocol-v1 contract

**Reference date:** 2026-08-20

The complete current contract is owned by the
[`NekoLib.Watchdog.Host` reference](../../src/Watchdog/NekoLib.Watchdog.Host/README.md).
These are pre-stable corrections for the first `1.0.0` family candidate.

## Breaking: reference the Host directly from each executable

The package no longer carries a `buildTransitive` target. If an executable
received the sidecar only because a wrapper package depended on
`NekoLib.Watchdog.Host`, add an explicit PackageReference:

```xml
<PackageReference Include="NekoLib.Watchdog.Host" Version="$(NekoLibPackageVersion)" />
```

Library projects should not deploy a Host on behalf of unknown downstream
applications. The direct executable owns architecture selection, the bootstrap
call, and deployment policy.

## Breaking: Host and library now require protocol v1

Update `NekoLib.Watchdog` and `NekoLib.Watchdog.Host` to the same coordinated
version and rebuild. Protocol v1 adds the required `--protocol-version 1`
launch option, the internal `protocol_version` check, and the attachment shape:

```text
attached:v1:<pid>:<attach-token>
```

Supported applications do not construct this command line; continue calling
`WatchdogBootstrap.EnsureStarted(args)`. A stale or mismatched Host now fails
with an incompatible-protocol diagnostic instead of being accepted or merely
timing out. There is no legacy-protocol compatibility shim before the first
stable baseline.

## Behavioral: explicit working directories fail fast

The package bootstrap already supplies the application's existing current
directory. Custom internal launchers must now ensure that an explicit
`--workdir` exists and is a directory before starting Host. Do not rely on Host
to create it or silently fall back. Omitting the option retains the target
directory default.

## Operational: fatal evidence moved out of the sidecar directory

Host no longer appends an unbounded relative `watchdog_host_fatal.log`. Inspect
bounded best-effort startup evidence at:

```text
%LOCALAPPDATA%/NekoLib/Watchdog/watchdog-host-fatal.log
```

The active file is capped at 256 KiB, keeps one `.1` backup, and records UTC and
process identity. Remove any operational collection rule that watched the old
sidecar-relative path.

## Unchanged package outcomes

The package remains separate and framework-dependent. It still deploys an
AnyCPU `net481` payload or an x86/x64 .NET 9 payload to the owned
`NekoLib.Watchdog.Host` output subdirectory, replaces stale contents, supports
`NekoLibWatchdogHostDeploy=false`, and requires the matching machine runtime.
It exports no compile-time API. The cooperative same-user pipe and
application/installer ACL boundaries are unchanged.
