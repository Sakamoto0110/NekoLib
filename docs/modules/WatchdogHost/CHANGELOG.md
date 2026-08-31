# NekoLib.Watchdog.Host Changelog

**Document ID:** WDGHOST-CHANGELOG

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** consumer-visible evolution of NekoLib.Watchdog.Host

**Surface:** changelog

**Boundary:** watchdog.host

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

The [coordinated family changelog](../../../CHANGELOG.md) remains the release
summary. This file records Host-specific consumer impact without duplicating
package hashes or release provenance.

Because this package ships no compile-time API, its consumer-visible surface is
the payload layout, the MSBuild properties and their outcomes, the launch
protocol, the exit codes, and the fatal-evidence path. A change to any of those
belongs here.

## 1.1.0

**Packages:** `NekoLib.Watchdog.Host`

**Compatibility class:** release-only

**Consumer impact:** The deployment package advances with the coordinated family version. Payload layout, build targets, bootstrap arguments, protocol v1, runtime prerequisites, and deployment behavior are unchanged.

**Migration:** none

- Upgrade `NekoLib.Watchdog` and `NekoLib.Watchdog.Host` together to preserve
  coordinated package and protocol identity.
- Immutable `1.1.0-local.9` is the qualifying package evidence for the unchanged
  payload topology and package-owned runtime probes.

## 1.0.0

**Packages:** `NekoLib.Watchdog.Host`

**Compatibility class:** mixed

**Consumer impact:** Deployment stopped propagating through wrapper packages, the launch protocol became versioned, and fatal evidence moved; consumers must add a direct reference where they relied on transitive deployment and must upgrade both Watchdog packages together.

**Migration:** `docs/modules/WatchdogHost/migrations/f1.md`

- The `buildTransitive` asset and its global import guard were removed. An
  executable that received the sidecar only because a wrapper package depended
  on this one now receives nothing; add an explicit `PackageReference` to each
  executable that owns a sidecar.
- The Host and library now require internal protocol v1: a mandatory
  `--protocol-version 1` launch option, an internal `protocol_version` check,
  and the `attached:v1:<pid>:<token>` attachment identity. A stale or mismatched
  pair fails with an explicit incompatible-protocol diagnostic instead of being
  accepted or timing out. There is no legacy-protocol shim.
- An explicitly supplied `--workdir` must exist and be a directory before
  supervision begins. Omitting the option still selects the target executable's
  directory.
- Fatal startup evidence moved from an unbounded, sidecar-relative
  `watchdog_host_fatal.log` to a fail-soft per-user
  `%LOCALAPPDATA%/NekoLib/Watchdog/watchdog-host-fatal.log` with a 256 KiB
  active bound, one `.1` backup, UTC timestamps, and the Host process ID. Remove
  any operational collection rule that watched the old path.
- The package remains separate and framework-dependent, still deploys an AnyCPU
  `net481` payload or an x86/x64 .NET 9 payload into the owned
  `NekoLib.Watchdog.Host` output subdirectory, still replaces stale contents,
  still supports `NekoLibWatchdogHostDeploy=false`, and still requires the
  matching machine runtime. It exports no compile-time API, and the cooperative
  same-user pipe and application/installer ACL boundaries are unchanged.
