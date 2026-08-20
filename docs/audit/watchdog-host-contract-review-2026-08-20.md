# Watchdog Host Contract Review — 2026-08-20

**Kind:** audit

**Lifecycle:** current

**Subject:** F1-WDOG-HOST deployment package, payload layout, build and publish
targets, bootstrap arguments, Host/application protocol, startup evidence,
target behavior, security boundary, and release validation

**Status:** review complete; six proposed dispositions await the decision gate

**Reference date:** 2026-08-20

**Reference commit:** `3ec2c63e2d60d96a8462c1a91483dea863015c01`

**Last reconciliation:** none

**Current state:** [`TODO.md`](../../TODO.md) F1-WDOG-HOST remains open; no
proposal in this review is accepted or implemented

## Baseline and worktree

This review covers committed `HEAD` on branch
`phase-e/sqlserver-and-orchestration`. At entry, `HEAD` was exactly
`3ec2c63e2d60d96a8462c1a91483dea863015c01`, the index and worktree were clean,
and the branch was 45 commits ahead of
`origin/phase-e/sqlserver-and-orchestration`. Recent history was:

```text
3ec2c63 docs(f1): close Watchdog public API review
6580b6f feat(watchdog): finalize F1 public API
81b6866 docs(f1): accept Watchdog public API direction
37e38f5 docs(f1): review Watchdog public API
075bb75 docs(f1): close Pipes public API review
```

Build outputs and a diagnostic compiler log created during review are ignored
artifacts and are not repository evidence. The review used the NekoLib routing
skill, the repository-hygiene workflow, and the repository working agreements.
Watchdog has no specialized module skill, so current projects, source, tests,
pack automation, package-only consumer projects, and current documentation are
the operative authorities.

## Scope and authorities

Included, in authority order:

1. current Host source and package assets under
   `src/Watchdog/NekoLib.Watchdog.Host`;
2. the Host and Watchdog project files, evaluated target behavior, direct
   references, and actual compiler output;
3. current Host/parser/bootstrap/protocol tests under
   `tests/NekoLib.Watchdog.Tests/Unit`;
4. `eng/pack-local.ps1`, `eng/test-local-packages.ps1`, and the
   PackageReference-only consumer projects;
5. the finalized Watchdog runtime and application bootstrap where they define
   the Host/application boundary;
6. the public API/release policy, root package documentation, Watchdog current
   reference, and `TODO.md` F1-WDOG-HOST;
7. the tracked Watchdog runtime-scenario documentation as a validation
   inventory only;
8. `watchdog-first-pass.md` and prior packaging notes as historical leads only.

Excluded:

- implementation of any proposed disposition;
- changes to product source, tests, projects, manifests, or `TODO.md`;
- creation, replacement, or publication of a package;
- package-consumer execution, Host launch, crash/restart injection, interactive
  hotkeys, cross-user/elevation probes, or any other runtime scenario;
- generic Pipes changes, Watchdog library API redesign, update orchestration,
  remote supervision, and hostile-same-user authentication design;
- full-solution build or test execution and pushing.

Ignored local packages were not treated as evidence for this committed
baseline. The historical first-pass audit was used only to locate questions;
every fact below was confirmed in current source, projects, or tests.

## Deliverable, targets, constants, and dependencies

`NekoLib.Watchdog.Host` is a Windows sidecar executable and deployment package,
not a compile-time library. Its project targets `net481` and
`net9.0-windows7.0`, uses `WinExe`, enables nullable analysis, disables implicit
usings, and declares no Host-specific compile constant. It directly references
`NekoLib.Watchdog`; the resulting published payload closes over Watchdog's Core
and Pipes dependencies and, on `net481`, Newtonsoft.Json.

| Target payload | Build form | Direct project dependency | Effective runtime closure |
|---|---|---|---|
| `tools/net481/` | framework-dependent managed AnyCPU executable | `NekoLib.Watchdog` | Watchdog, Core, Pipes, Newtonsoft.Json, .NET Framework 4.8.1 |
| `tools/net9.0-windows7.0/win-x86/` | framework-dependent x86 apphost | `NekoLib.Watchdog` | Watchdog, Core, Pipes, x86 .NET 9 Windows runtime |
| `tools/net9.0-windows7.0/win-x64/` | framework-dependent x64 apphost | `NekoLib.Watchdog` | Watchdog, Core, Pipes, x64 .NET 9 Windows runtime |

The `net481` compiler command used `/platform:AnyCPU`. Reflection reported
`ILOnly` without `Required32Bit`; an I386 managed PE header is normal for this
AnyCPU form. The evaluated `PlatformTarget` property alone was therefore not
used to classify the payload architecture.

Packing is deliberately conditional on `NekoLibWatchdogHostPayloadRoot`.
`IncludeBuildOutput` is false, package validation is disabled because this is a
tools/build package, and the package contains no `lib/` compile asset
([`NekoLib.Watchdog.Host.csproj:11`](../../src/Watchdog/NekoLib.Watchdog.Host/NekoLib.Watchdog.Host.csproj#L11),
[`NekoLib.Watchdog.Host.csproj:18`](../../src/Watchdog/NekoLib.Watchdog.Host/NekoLib.Watchdog.Host.csproj#L18),
[`NekoLib.Watchdog.Host.csproj:34`](../../src/Watchdog/NekoLib.Watchdog.Host/NekoLib.Watchdog.Host.csproj#L34)).

The canonical pack flow requires a clean Git tree by default, publishes one
`net481` payload plus framework-dependent `win-x86` and `win-x64` modern
payloads, packs the coordinated family version, runs PackageReference-only
smoke consumers, and refuses to overwrite an immutable version
([`pack-local.ps1:103`](../../eng/pack-local.ps1#L103),
[`pack-local.ps1:173`](../../eng/pack-local.ps1#L173),
[`pack-local.ps1:185`](../../eng/pack-local.ps1#L185),
[`pack-local.ps1:243`](../../eng/pack-local.ps1#L243)).

## Contract inventory and classification

The compiled Host assemblies export zero public types on both targets. There
is therefore no Host compiled-API manifest to approve. The public package
contract is instead the following deployment and protocol surface, as required
by the release policy
([`public-api-release-policy.md:20`](../public-api-release-policy.md#L20)).

| Contract element | Current behavior | Proposed classification |
|---|---|---|
| Package ID and coordinated family version | `NekoLib.Watchdog.Host`; same version as the library family | stable |
| `tools/net481/` payload root | framework-dependent managed AnyCPU Host and runtime closure | stable |
| `tools/net9.0-windows7.0/win-x86/` payload root | framework-dependent x86 Host and runtime closure | stable |
| `tools/net9.0-windows7.0/win-x64/` payload root | framework-dependent x64 Host and runtime closure | stable |
| Build/publish destination | `<output>/NekoLib.Watchdog.Host/`, replaced as one package-owned directory | stable |
| `NekoLibWatchdogHostDeploy` | opt-out deployment switch, default `true` | deliberate public extension |
| `NekoLibWatchdogHostRid` | explicit modern payload selector, `win-x86` or `win-x64` | deliberate public extension |
| RID inference | runtime identifier, then platform/prefer-32-bit, then x64 default | stable after focused validation |
| Named `_NekoLib*` MSBuild targets and items | implementation machinery | internal implementation detail |
| `NekoLibWatchdogHostTargetsImported` | global import guard exposed by the package | candidate for internalization/removal with the transitive asset |
| `build/NekoLib.Watchdog.Host.targets` | direct-reference deployment behavior | stable outcome; target name is internal |
| `buildTransitive/NekoLib.Watchdog.Host.targets` | propagates deployment through wrapper packages | candidate for removal before the stable baseline |
| `--target`, `--args`, `--workdir`, `--attach-pid`, `--attach-token` | package-internal bootstrap-to-Host launch protocol | stable only after protocol versioning and validation |
| Direct interactive Host command line | not documented or designed as an end-user CLI | internal; unsupported composition path |
| Attach status response | `attached:<pid>:<token>` | candidate for pre-stable replacement by a versioned identity |
| Exit code `0` / `1` | clean runtime exit / any fatal startup or runtime exception | stable operational convention after documentation |
| `watchdog_host_fatal.log` | relative, unbounded best-effort fatal evidence | candidate for replacement before the stable baseline |
| Published PDBs | diagnostic files carried by publish output | deliberate package content, not a stable filename/API guarantee |
| Update command | internal `not_implemented` compatibility response owned by Watchdog | not a Host feature; no experimental public surface |

No Host element should be declared experimental. There is no obsolete Host
surface that needs a released migration window because no stable Host baseline
exists yet. The two removals proposed here are pre-stable candidate corrections.

## Ownership, lifecycle, threading, security, and package boundaries

### Deployment and package ownership

The executable project must reference the Host package directly. On build and
publish, the package owns exactly the `NekoLib.Watchdog.Host` child directory
under the consumer output. It removes and recreates that directory to prevent
old payload files from surviving an upgrade; disabling deployment and cleaning
remove the same owned directory
([`NekoLib.Watchdog.Host.Package.targets:36`](../../src/Watchdog/NekoLib.Watchdog.Host/NekoLib.Watchdog.Host.Package.targets#L36),
[`NekoLib.Watchdog.Host.Package.targets:53`](../../src/Watchdog/NekoLib.Watchdog.Host/NekoLib.Watchdog.Host.Package.targets#L53),
[`NekoLib.Watchdog.Host.Package.targets:70`](../../src/Watchdog/NekoLib.Watchdog.Host/NekoLib.Watchdog.Host.Package.targets#L70),
[`NekoLib.Watchdog.Host.Package.targets:88`](../../src/Watchdog/NekoLib.Watchdog.Host/NekoLib.Watchdog.Host.Package.targets#L88)).

The package does not own its parent application output, application settings,
credentials, target executable, target working directory, .NET runtime
installation, install ACL, service wrapper, or installer/updater. It must never
delete outside its fixed child directory. PDBs may be shipped for diagnostics,
but consumers must not bind behavior to them.

### Bootstrap and process lifecycle

The application owns calling `WatchdogBootstrap.EnsureStarted` early in startup
and supplies its original argument array. Bootstrap resolves the deployed Host,
creates a correlation token, launches it from the sidecar directory, and waits
within one total handshake budget. It terminates only the unconfirmed Host
process that it launched. Once the exact PID/token attachment is confirmed, the
Host owns `WatchdogRuntime`, blocks in `WaitForExit`, and the finalized F1-WDOG
runtime owns process supervision and shutdown
([`WatchdogBootstrap.cs:132`](../../src/Watchdog/NekoLib.Watchdog/WatchdogBootstrap.cs#L132),
[`WatchdogBootstrap.cs:149`](../../src/Watchdog/NekoLib.Watchdog/WatchdogBootstrap.cs#L149),
[`Program.cs:14`](../../src/Watchdog/NekoLib.Watchdog.Host/Program.cs#L14)).

The Host adds no independent worker or event thread. Its main thread blocks in
the runtime and returns zero after orderly completion. Fatal exceptions are
isolated at the executable boundary and return one.

### Protocol and cancellation boundary

The bootstrap launch uses exact, case-sensitive, single-occurrence options.
The parser rejects missing values, duplicates, unknown options, missing target
files, non-positive PIDs, and incomplete PID/token pairs
([`HostArgumentParser.cs:20`](../../src/Watchdog/NekoLib.Watchdog.Host/HostArgumentParser.cs#L20),
[`HostArgumentParser.cs:71`](../../src/Watchdog/NekoLib.Watchdog.Host/HostArgumentParser.cs#L71),
[`HostArgumentParser.cs:75`](../../src/Watchdog/NekoLib.Watchdog.Host/HostArgumentParser.cs#L75)).
Command-line quoting is centralized in the application library and has a
Windows round-trip regression. Bootstrap connection and request operations are
bounded by the caller's one handshake timeout; there is no public asynchronous
or cancellation-token bootstrap operation.

The attach token proves that the responding runtime accepted the launch's
specific attachment request. It is a correlation value, not a credential or a
cryptographic protocol. The Host and application currently have no explicit
wire-version field.

### Security boundary

Watchdog RPC and event endpoints remain `CurrentUserOnly` on both targets. This
prevents ordinary cross-user access but does not authenticate a hostile process
already running as the same Windows user. The deterministic pipe name, PID, and
token are identity/correlation data; command-line values are discoverable by a
sufficiently privileged same-user process and must not be documented as
secrets.

The application or installer owns filesystem ACLs on the application and
sidecar directories, signing policy, elevated/non-elevated topology, service
identity, and protection against binary replacement. Adding network transport,
credentials, remote control, or an authorization framework is outside this
local cooperative sidecar contract.

## Confirmed findings

### WDHOST-01 — High compatibility impact — the Host/application protocol has no explicit version

**Observed fact.** Bootstrap emits only target, PID/token, target arguments, and
working-directory options. The Host parser accepts exactly those names. The
attachment identity is the unversioned string `attached:<pid>:<token>` and both
the initial wait and already-running-Host preflight parse that shape
([`WatchdogBootstrap.cs:135`](../../src/Watchdog/NekoLib.Watchdog/WatchdogBootstrap.cs#L135),
[`HostArgumentParser.cs:22`](../../src/Watchdog/NekoLib.Watchdog.Host/HostArgumentParser.cs#L22),
[`WatchdogBootstrap.cs:202`](../../src/Watchdog/NekoLib.Watchdog/WatchdogBootstrap.cs#L202),
[`WatchdogBootstrap.cs:290`](../../src/Watchdog/NekoLib.Watchdog/WatchdogBootstrap.cs#L290)).

**Risk or hypothesis.** Coordinated packaging reduces mismatch likelihood but
cannot prevent a stale copied or already-running Host, an independently
resolved Watchdog library version, or manual deployment. A future protocol
change could time out, partially interoperate, or return an opaque invalid
identity instead of reporting a deterministic incompatibility.

**Proposed decision.** Establish internal Host protocol v1. Require
`--protocol-version 1`, include the version in attachment status, and make
bootstrap distinguish an unsupported/legacy protocol from timeout and missing
attachment. Keep this package-internal: it is a compatibility contract between
the coordinated library and Host, not a new public controller API.

**Compatibility and migration.** This intentionally rejects mismatched
pre-stable Host/application pairs. A supported consumer updates the coordinated
`NekoLib.Watchdog` and `NekoLib.Watchdog.Host` package versions together and
rebuilds so the owned sidecar directory is replaced. No compatibility shim is
proposed before the first stable baseline.

**Rejected alternatives.** Relying only on equal NuGet versions does not detect
stale or manually copied binaries. Accepting both versioned and unversioned
forms would immediately make the legacy format part of the baseline. Making
the token a secret or adding authentication does not solve version negotiation
and exceeds the accepted same-user threat model.

**Validation if accepted.** Add dual-target parser, formatter, fresh-attach,
already-running-Host, missing-version, unknown-version, and legacy-response
tests. Package-backed validation must show a coordinated pair succeeds and a
deliberately mismatched pair fails with a version-specific diagnostic.

### WDHOST-02 — Medium package-boundary impact — deployment currently propagates transitively

**Observed fact.** The project packs the deployment target under both `build/`
and `buildTransitive/`. The transitive asset imports the direct build target
through the public-looking `NekoLibWatchdogHostTargetsImported` guard
([`NekoLib.Watchdog.Host.csproj:44`](../../src/Watchdog/NekoLib.Watchdog.Host/NekoLib.Watchdog.Host.csproj#L44),
[`NekoLib.Watchdog.Host.csproj:47`](../../src/Watchdog/NekoLib.Watchdog.Host/NekoLib.Watchdog.Host.csproj#L47),
[`buildTransitive/NekoLib.Watchdog.Host.targets:1`](../../src/Watchdog/NekoLib.Watchdog.Host/buildTransitive/NekoLib.Watchdog.Host.targets#L1)).
Current consumer documentation instead requires a direct reference from the
executable project
([`README.md:262`](../../README.md#L262)).

**Risk to consumers.** A wrapper library can cause an executable to receive a
sidecar it did not select, including targets that replace and delete a directory
in its output. Transitive deployment also obscures which executable owns the
bootstrap call, target bitness, runtime prerequisite, and opt-out policy.

**Proposed decision.** Remove the `buildTransitive` asset and its global import
guard. Retain the `build/` target and require every executable that deploys the
Host to reference `NekoLib.Watchdog.Host` directly. Keep the named `_NekoLib*`
targets/items internal and stabilize outcomes, not target names.

**Compatibility and migration.** A consumer relying on accidental transitive
deployment adds an explicit Host PackageReference to each executable project.
This is a pre-stable package correction and does not require a released
deprecation window.

**Rejected alternatives.** Keeping both assets for hypothetical wrapper
packages conflicts with the documented ownership boundary. Replacing them with
custom SDK-wide discovery would make deployment still less explicit. Renaming
the import guard while retaining transitive behavior does not fix ownership.

**Validation if accepted.** Package a wrapper-library consumer and prove its
executable receives no Host without a direct reference, then add the direct
reference and prove build, publish, disable, re-enable, stale-directory
replacement, and clean behavior.

### WDHOST-03 — Medium operational impact — fatal startup evidence is relative, unbounded, and disposable

**Observed fact.** Every unhandled Host exception is appended to the relative
file `watchdog_host_fatal.log` with local wall-clock time. Append failures are
correctly swallowed so evidence cannot replace the original failure, but there
is no size bound, rotation, deterministic user-data location, process identity,
or discovery hint
([`Program.cs:24`](../../src/Watchdog/NekoLib.Watchdog.Host/Program.cs#L24),
[`Program.cs:31`](../../src/Watchdog/NekoLib.Watchdog.Host/Program.cs#L31)).
The supported bootstrap launches the Host with the package-owned sidecar
directory as its working directory
([`WatchdogBootstrap.cs:152`](../../src/Watchdog/NekoLib.Watchdog/WatchdogBootstrap.cs#L152)).

**Risk to consumers.** The log can grow without limit, can be unwritable under a
machine installation, and is deleted when the package replaces its owned
sidecar directory. A direct launch can write it somewhere else. The application
receives only an early-exit or timeout exception and has no stable place to
inspect the Host's original failure.

**Proposed decision.** Write best-effort bounded Host-fatal evidence under a
documented per-user LocalApplicationData NekoLib/Watchdog location, use UTC and
process identity, retain one rotated backup, and name that path in the
bootstrap early-exit diagnostic. Keep reporting non-throwing. Do not make the
fatal log an application-owned sink or a general logging API.

**Compatibility and migration.** Operators looking in the sidecar directory
move to the documented per-user path. The old relative file is not retained as
a compatibility copy because duplicating unbounded fatal evidence preserves
the defect.

**Rejected alternatives.** The package directory is replaceable and may be
read-only. The target working directory is application policy and may also be
unwritable. Windows Event Log requires source/installer policy and privileges.
Console or standard-error output is ineffective for a hidden `WinExe` bootstrap.

**Validation if accepted.** Add dual-target tests for deterministic path,
UTC/process fields, rotation and size bound, unwritable-path isolation, and the
bootstrap diagnostic. A packaged startup-failure probe must confirm the
documented file is produced without changing the Host exit code.

### WDHOST-04 — Medium recovery impact — an explicit working directory is normalized but not validated

**Observed fact.** The parser confirms that the target file exists but converts
`--workdir` only with `Path.GetFullPath`; it does not require an existing
directory
([`HostArgumentParser.cs:75`](../../src/Watchdog/NekoLib.Watchdog.Host/HostArgumentParser.cs#L75),
[`HostArgumentParser.cs:89`](../../src/Watchdog/NekoLib.Watchdog.Host/HostArgumentParser.cs#L89)).
Runtime configuration preserves that path and process launches use it
([`WatchdogRuntimeOptions.cs:62`](../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntimeOptions.cs#L62),
[`WatchdogRuntime.cs:869`](../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs#L869)).

**Risk to consumers.** Attach-mode startup can confirm the already-running
application before the directory is needed. A later restart can then enter
repeated launch failures because the invalid working directory was accepted at
the Host boundary. The current bootstrap normally supplies an existing current
directory, so this is a recovery-policy risk rather than a common startup path.

**Proposed decision.** Require an explicitly supplied `--workdir` to exist and
be a directory when the Host starts. When the option is absent in an internal or
test composition, retain the Watchdog runtime default of the target directory.
Document that the application owns keeping the directory available for future
restarts.

**Compatibility and migration.** Package bootstrap callers need no change.
Unsupported manual launchers that supplied a future directory must create it
before launch or omit the option. Later deletion remains an application
operational failure and is reported by existing restart evidence.

**Rejected alternatives.** Automatically creating an arbitrary working
directory would let the Host mutate application policy and can hide a typo.
Falling back silently to the target directory changes child semantics. Checking
only at every restart detects the problem too late to reject a bad bootstrap
contract.

**Validation if accepted.** Add parser tests for an existing directory, a file
used as workdir, a missing directory, the absent-option default, relative paths,
and target/workdir paths containing spaces on both targets.

### WDHOST-05 — Medium release-governance impact — the Host has no dedicated current contract owner

**Observed fact.** The root README documents direct reference, output path,
payload architecture, selection order, opt-out, and runtime prerequisite. The
Watchdog reference explicitly defers Host package, build target, argument, and
protocol details to F1-WDOG-HOST
([`README.md:262`](../../README.md#L262),
[`NekoLib.Watchdog/README.md:221`](../../src/Watchdog/NekoLib.Watchdog/README.md#L221)).
There is no Host README. The Host assembly correctly exports zero public types,
so a compiled public API manifest would be empty and would not baseline the
real contract.

**Risk to consumers.** Stable and internal package elements are not classified
in one current owner. Package layout, target behavior, deployment properties,
CLI non-goals, protocol version, security, exit behavior, fatal evidence, and
application responsibilities can drift independently across root docs, source,
and smoke scripts.

**Proposed decision.** Create
`src/Watchdog/NekoLib.Watchdog.Host/README.md` as the current Host technical
contract. Record the classification table from this review, required payload
roots and runtime closure, stable deployment outcomes and properties, internal
target names, protocol v1, lifecycle/ownership, exit/fatal evidence, target
differences, security/application policy, package content policy, and non-goals.
Link it from root/module/docs indexes. Use source tests and package smoke as the
executable baseline rather than inventing a compiled API manifest for an
executable with no exported types.

**Compatibility and migration.** Documentation adds no runtime break. It makes
the WDHOST-01, WDHOST-02, WDHOST-03, and WDHOST-04 pre-stable changes explicit
and supplies their migration path.

**Rejected alternatives.** Treating this audit as the permanent current owner
violates audit lifecycle rules. Adding an empty `eng/public-api` manifest would
create false confidence. A generic repository-wide protocol-manifest framework
is disproportionate to this one sidecar and has no existing verifier.

**Validation if accepted.** Run documentation verification, validate every
link, and cross-check the tables against package contents, evaluated targets,
the parser, bootstrap, and package-only consumers on both targets.

### WDHOST-06 — High release-evidence impact — package smoke does not close the selected-payload and protocol matrix

**Observed fact.** Current smoke requires both build assets, the three Host
executables, no `lib/` assets, and correct PE machine values for the modern
package entries. It builds/runs PackageReference-only consumers, checks stale
directory replacement, builds and publishes both target families, verifies
deployment opt-out/re-enable, and cleans the sidecar
([`test-local-packages.ps1:164`](../../eng/test-local-packages.ps1#L164),
[`test-local-packages.ps1:188`](../../eng/test-local-packages.ps1#L188),
[`test-local-packages.ps1:250`](../../eng/test-local-packages.ps1#L250),
[`test-local-packages.ps1:282`](../../eng/test-local-packages.ps1#L282),
[`test-local-packages.ps1:323`](../../eng/test-local-packages.ps1#L323),
[`test-local-packages.ps1:371`](../../eng/test-local-packages.ps1#L371)).
It does not explicitly select `NekoLibWatchdogHostRid=win-x86` and inspect the
deployed file, prove the x64 default and unsupported-RID failure, assert the
`net481` deployed executable is AnyCPU, exercise direct-only deployment, or
validate a package-backed protocol v1 startup/mismatch.

**Risk to consumers.** The package can contain correct individual payloads
while selection deploys the wrong one. An ARM or malformed RID can regress from
fail-fast behavior. A build-only smoke cannot prove that the packaged Host and
application library agree on launch arguments and the attach response. No
current immutable package was produced for this review baseline.

**Proposed decision.** Extend the purpose-built package smoke to verify exact
required/forbidden layout after removing `buildTransitive`; deployed x86, x64
default, and AnyCPU `net481` architecture; supported and unsupported RID
selection; direct-reference ownership; build, publish, disable/re-enable,
replacement, and clean; and coordinated protocol v1 startup plus explicit
mismatch failure. After implementation and a clean commit, create one new
immutable coordinated package version through `pack-local.ps1` without
`-AllowDirty` and record package ID/version/hash/provenance.

**Compatibility and migration.** This changes validation rather than the
supported package outcome. Consumers continue to reference the Host directly,
align family versions, and select only x86/x64 on modern Windows. A protocol
mismatch becomes a clear supported failure instead of a timeout.

**Rejected alternatives.** Inspecting only `.nupkg` entries misses deployment
selection. ProjectReference consumers are not external package evidence.
Reusing an old local version violates immutable provenance. Self-contained
payloads, one NuGet ID per RID, MSI/MSIX installation, and a service Host would
multiply distribution policy without a demonstrated requirement.

**Validation if accepted.** Run the expanded smoke on a clean committed tree,
verify repository commit metadata and hashes, and separately record build,
package, package-backed startup, and runtime-scenario evidence. Do not conflate
the existing tracked crash/recovery scenario with package evidence unless it
actually consumes the newly created package.

## Items analyzed and rejected

1. **Split the `net481` payload by x86/x64.** Rejected. The actual compiler
   emitted AnyCPU IL and the managed PE has no `Required32Bit`; the README's
   AnyCPU claim is correct.
2. **Make Host classes or the argument parser public.** Rejected. The executable
   is package infrastructure, exports zero public types, and advanced consumers
   already use the deliberate `WatchdogRuntime` library surface.
3. **Keep transitive deployment for wrapper packages.** Rejected under
   WDHOST-02 because executable ownership must be explicit.
4. **Stabilize MSBuild target and item names.** Rejected. Only destination,
   selection, opt-out, replacement, publish, and clean outcomes are consumer
   contract. Underscore-prefixed implementation names may change.
5. **Guarantee every PDB filename.** Rejected. Published PDBs are useful
   diagnostic content, but applications must not depend on them and their
   presence is not a runtime compatibility boundary.
6. **Use the attach token as authentication.** Rejected. It is visible process
   correlation within the cooperative same-user boundary.
7. **Add Host self-supervision or update orchestration.** Rejected. A process
   cannot guarantee its own recovery after termination; services, scheduled
   tasks, installers, and update policy belong to an external application
   deployment design.
8. **Add remote transport or same-user hostile-process authentication.**
   Rejected for F1. The current product is a local Windows sidecar and app/install
   ACL policy remains external.
9. **Replace the current payload strategy with self-contained or per-RID
   package IDs.** Rejected. The coordinated tools/build package already isolates
   runtime closures and keeps selection explicit; no requirement justifies the
   larger immutable artifact matrix.
10. **Treat an existing ignored local package as current evidence.** Rejected.
    Local artifacts can predate `HEAD` and are not shared repository authority.

## Consolidated proposal for the decision gate

No item below is accepted by this review alone.

1. **Adopt internal Host protocol v1:** require a version launch argument,
   version attachment identity, distinguish mismatch from timeout, and require
   coordinated Host/library versions.
2. **Make deployment direct-only:** remove `buildTransitive` and its import
   guard; keep explicit executable PackageReference plus the `build/` target.
3. **Replace fatal evidence:** use a documented bounded per-user LocalAppData
   log with UTC/process context, one backup, fail-soft writes, and a discoverable
   bootstrap diagnostic.
4. **Validate explicit working directories:** reject missing or non-directory
   `--workdir` values before supervision while retaining the target-directory
   default when omitted.
5. **Create the current Host contract owner:** add a dedicated Host README and
   index links covering package, properties, arguments, protocol, ownership,
   security, target differences, evidence, and non-goals; do not create an empty
   compiled-API manifest.
6. **Close package release evidence:** expand package-only smoke for layout,
   direct ownership, AnyCPU/x86/x64 selection, unsupported RID, protocol success
   and mismatch, then produce one clean immutable coordinated package and record
   its provenance after implementation.

If accepted, promote these exact decisions to `TODO.md` before implementation.
Do not infer acceptance from the existence of this audit.

## Validation commands and results

Review-time commands were intentionally focused:

| Command | Result |
|---|---|
| `dotnet build src/Watchdog/NekoLib.Watchdog.Host/NekoLib.Watchdog.Host.csproj -f net481 --no-restore` | passed; 0 warnings, 0 errors |
| `dotnet build src/Watchdog/NekoLib.Watchdog.Host/NekoLib.Watchdog.Host.csproj -f net9.0-windows --no-restore` | passed; 0 warnings, 0 errors |
| reflection over both built Host assemblies | 0 exported public types on each target |
| diagnostic `net481` build plus `GetPEKind` | compiler used AnyCPU; `ILOnly`, I386 managed PE, no `Required32Bit` |
| `dotnet test tests/NekoLib.Watchdog.Tests/Unit/NekoLib.Watchdog.Tests.Unit.csproj -f net9.0-windows --no-restore` | passed 92/92 |
| same focused suite for `net481` | final run passed 92/92; two earlier full runs each passed 91/92 and intermittently returned zero replay entries in `SubscribeLogs_ReplayUsesSerializerNeutralMetadataJson`; the isolated regression passed 5/5 and the complete controller class passed 5/5 |
| `git diff --check` before authoring | passed; clean worktree |
| `.\eng\verify-docs.ps1` after indexing and staging | passed; the expected note reported that no build log was supplied for warning-baseline comparison |
| `git diff --check` after authoring | passed |

The intermittent `net481` result is not attributed to test parallelism: the
assembly explicitly disables test parallelization
([`AssemblyInfo.cs:3`](../../tests/NekoLib.Watchdog.Tests/Unit/AssemblyInfo.cs#L3)).
The affected fixture binds a deterministic current-process pipe and the failure
did not reproduce in isolation or in the final full run
([`WatchdogControllerTests.cs:55`](../../tests/NekoLib.Watchdog.Tests/Unit/WatchdogControllerTests.cs#L55),
[`WatchdogControllerTests.cs:159`](../../tests/NekoLib.Watchdog.Tests/Unit/WatchdogControllerTests.cs#L159)).
The exact environmental or cleanup cause remains unconfirmed; it is a residual
validation limitation, not a confirmed Host product defect.

## Residual limitations

- No Host process was launched, so startup, attach, restart, shutdown, visible
  window, hotkey, taskkill, fatal-log, or crash-recovery behavior was exercised.
- No package was created. Payload contents, dependency ranges, selected deployed
  architecture, clean replacement, build/publish import behavior, and repository
  provenance were inspected in source or existing tests but not executed from a
  package produced at this commit.
- No deliberate old/new Host-library mismatch was run; WDHOST-01 is established
  from the absent version field and possible independent binary lifetime.
- No unsupported RID build, x86-selected deployment, ARM64 consumer, machine
  without the required .NET runtime, read-only installation directory, or
  LocalApplicationData denial was exercised.
- No cross-user, elevated/non-elevated, hostile-same-user, service-account,
  installer ACL, binary-signing, or tamper probe was run.
- The two early `net481` full-suite failures remain unexplained despite isolated,
  class-level, and final full-run passes.
- The full solution, package consumer campaign, and runtime scenarios were not
  run by design.

This review produced no product fix, test change, project or manifest change,
public API baseline update, package, publish, runtime execution, `TODO.md`
promotion, or push. F1-WDOG-HOST is not marked complete.
