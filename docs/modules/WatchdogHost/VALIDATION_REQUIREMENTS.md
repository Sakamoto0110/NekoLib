# NekoLib.Watchdog.Host Validation Requirements

**Document ID:** WDGHOST-VALIDATION-REQUIREMENTS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** evidence contract for qualifying the NekoLib.Watchdog.Host boundary

**Surface:** validation-requirements

**Boundary:** watchdog.host

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

The [module manifest](MANIFEST.md) owns the inherited profile list. The
requirements below specialize it for what this boundary actually is: a package
whose entire contract is what it deploys, where it deploys it, how a consumer
selects it, and what the deployed executable then does. They are derived from
architecture and risk, not from the probes that happen to exist; several are
`NOT_RUN` in [`VALIDATIONS.md`](VALIDATIONS.md) and say so there.

Because the Host exports zero public types, **no API-compatibility requirement
applies to this boundary**, and no accepted manifest exists to verify. That is a
deliberate classification from the contract review, not an omission.

## WDGHOST-VALREQ-001

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to Host source, the project file, or the consumer targets file

**Category:** build

**Boundary:** in-process

**Targets:** `net481` and `net9.0-windows`

**Acceptance criteria:** Both target executables build with zero errors and no new normalized warning identity, and reflection over each built assembly reports zero exported public types.

**Required evidence level:** build-only

**Rationale:** The zero-exported-types property is what justifies having no API baseline at all. If a public type ever appeared, the classification in the manifest and the reference would be wrong, and nothing else in the pipeline would notice.

## WDGHOST-VALREQ-002

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to packaging, payload publication, or the packed asset list

**Category:** package

**Boundary:** package-feed

**Targets:** the produced `NekoLib.Watchdog.Host` package

**Acceptance criteria:** The package contains `build/NekoLib.Watchdog.Host.targets` and all three payload executables, contains no `buildTransitive` asset and no `lib/` entry, and the three payload executables carry the expected PE metadata — managed AnyCPU IL for `net481`, an x86 machine for `win-x86`, and an x64 machine for `win-x64`. The modern payloads must not carry `Newtonsoft.Json`.

**Required evidence level:** automated-runtime

**Rationale:** The payload roots and the absence of a compile-time asset are the package's public contract. A pack that silently omits a payload or reintroduces a `lib/` entry changes what consumers receive without changing any source file.

## WDGHOST-VALREQ-003

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to the selection conditions in the consumer targets file

**Category:** package-consumer

**Boundary:** deployment

**Targets:** `net481`, `net9.0-windows` x86, and `net9.0-windows` x64 consumers

**Acceptance criteria:** A modern consumer with no RID hint deploys the x64 payload; `NekoLibWatchdogHostRid=win-x86` deploys the x86 payload; an unsupported RID fails the build with a payload-specific diagnostic; and a `net481` consumer deploys the AnyCPU payload. Each deployed executable must be byte-identical to its package entry.

**Required evidence level:** automated-runtime

**Rationale:** A package can contain three correct payloads and still deploy the wrong one. Byte identity is what distinguishes a genuinely package-sourced payload from repository build output that happened to land in the same place.

## WDGHOST-VALREQ-004

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to the packed asset list or the import guard

**Category:** package-consumer

**Boundary:** package-feed

**Targets:** an executable consuming a wrapper package that itself references the Host

**Acceptance criteria:** The wrapper-only executable receives no `NekoLib.Watchdog.Host` directory in either build or publish output, and the same executable receives one after adding its own direct `PackageReference`.

**Required evidence level:** automated-runtime

**Rationale:** Direct-only ownership is an accepted pre-stable correction whose whole point is a negative outcome. Only a consumer that is expected to receive nothing can demonstrate it, and reintroducing a `buildTransitive` asset would otherwise be invisible.

## WDGHOST-VALREQ-005

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to the deploy, disable, or clean targets

**Category:** package-consumer

**Boundary:** deployment

**Targets:** `net481` and `net9.0-windows` consumers, build and publish

**Acceptance criteria:** Build and publish each populate the owned directory; a stale file placed in it before a rebuild is removed; `NekoLibWatchdogHostDeploy=false` removes the directory from both build and publish output; re-enabling restores it; and `dotnet clean` removes it. Nothing outside the owned child directory is ever removed.

**Required evidence level:** automated-runtime

**Rationale:** The package deletes and recreates a directory inside the consumer's output on every build. The replacement behavior is what prevents an upgrade from leaving a mixed payload, and the ownership boundary is what keeps a deletion from reaching the application itself.

## WDGHOST-VALREQ-006

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to the launch arguments, the protocol version, or the attachment identity

**Category:** protocol

**Boundary:** process

**Targets:** `net481` and `net9.0-windows`

**Acceptance criteria:** A coordinated package pair starts from a package-deployed Host, answers a control request, and shuts down cooperatively on both target families; a deliberately mismatched protocol version fails with a version-specific diagnostic rather than a timeout or an accepted attachment.

**Required evidence level:** automated-runtime

**Rationale:** Protocol v1 exists precisely because coordinated NuGet versions cannot prevent a stale or manually copied Host. Only a real package-backed launch exercises the argument encoding, the version exchange, and the attachment identity together.

## WDGHOST-VALREQ-007

**Classification:** REQUIRED

**Trigger:** every change to `HostArgumentParser`

**Category:** focused-regression

**Boundary:** filesystem

**Targets:** `net481` and `net9.0-windows` test targets

**Acceptance criteria:** Parsing preserves target, arguments, working directory, PID, and token for a well-formed command line, retains the runtime default when `--workdir` is absent, and rejects an empty command line, an unknown option, a duplicate option, a missing value, an absent or unsupported protocol version, a missing target file, a non-positive PID, either half of the PID/token pair alone, a file used as a working directory, and a missing working directory.

**Required evidence level:** automated-runtime

**Rationale:** The parser is the boundary between a package-internal protocol and real supervision. Every rejection it performs is a failure the application would otherwise discover as a restart loop rather than as a startup error.

## WDGHOST-VALREQ-008

**Classification:** REQUIRED

**Trigger:** every change to `HostFatalLog` or the bootstrap early-exit diagnostic

**Category:** focused-regression

**Boundary:** filesystem

**Targets:** `net481` and `net9.0-windows` test targets

**Acceptance criteria:** The default path resolves under per-user local application data; an entry records a UTC timestamp and the Host process ID; an oversized entry is bounded; an append that would cross the bound rotates to exactly one `.1` backup; an already-oversized active file is handled deterministically; and an unwritable path is swallowed without replacing the original failure.

**Required evidence level:** automated-runtime

**Rationale:** This log is the only evidence of a Host that never reached supervision, and its writes happen on the failure path where an exception must never escape. The already-oversized branch is called out because it is the one that discards retained evidence, per `WDGHOST-FINDING-003`.

## WDGHOST-VALREQ-009

**Classification:** REQUIRED

**Trigger:** every change to `Program.Main` or the runtime disposal path it relies on

**Category:** runtime

**Boundary:** process

**Targets:** at least one supported target

**Acceptance criteria:** An orderly runtime exit returns `0`; an argument, startup, or runtime failure returns `1` and writes fatal evidence; and after a Host that exited with `0`, no supervised child, worker thread, control endpoint, or instance guard remains held.

**Required evidence level:** automated-runtime

**Rationale:** The exit code is the only signal available to an installer, service wrapper, or scheduled task, and it is the documented interface for them. `WDGHOST-FINDING-004` records that no current test asserts it.

## WDGHOST-VALREQ-010

**Classification:** CONDITIONAL

**Trigger:** claiming support for a consumer framework version other than .NET Framework 4.8.1 or .NET 9 Windows

**Category:** package-consumer

**Boundary:** deployment

**Targets:** the claimed consumer framework version

**Acceptance criteria:** A consumer on the claimed version deploys a payload it can actually launch, and bootstrap reaches a confirmed attachment; or the incompatibility is rejected at build time before the claim is made.

**Required evidence level:** automated-runtime

**Rationale:** Selection maps the framework identifier without checking its version, so a mismatched consumer builds and deploys and only fails at launch, per `WDGHOST-FINDING-001`. This requirement exists so that widening the supported set cannot skip its evidence.

## WDGHOST-VALREQ-011

**Classification:** NOT_APPLICABLE

**Trigger:** none

**Category:** security

**Boundary:** deployment

**Targets:** none

**Acceptance criteria:** none

**Required evidence level:** build-only

**Rationale:** Filesystem ACLs on the application and sidecar directories, binary signing and integrity, elevation topology, service identity, and protection against sidecar replacement are explicitly application and installer responsibilities in [`REFERENCE.md`](REFERENCE.md). ARM64 payloads, self-contained deployment, per-RID package IDs, and installer-based distribution are declared non-goals. All are listed here so their absence reads as an accepted scope decision rather than an untested requirement; admitting any of them requires an accepted decision, not a new probe.
