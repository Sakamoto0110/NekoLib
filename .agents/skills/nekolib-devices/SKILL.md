---
name: nekolib-devices
description: Implement, diagnose, review, document, or test NekoLib.Devices, including HardwareEngine, ICommTransport, hardware protocols, serial ports, stream transports, TCP, named pipes, endpoint configuration, timeouts, cancellation, and device communication. Use for work under src/Devices or tests/NekoLib.Devices.Tests, and for application code whose behavior depends on NekoLib device APIs.
---

# Work on NekoLib Devices

Preserve byte-stream and protocol behavior across `net481` and `net9.0` while
keeping dependency decisions proportional to the requested capability.

## Establish current truth

1. Read `../../../AGENTS.md`.
2. Inspect `../../../src/Devices/NekoLib.Devices/NekoLib.Devices.csproj`.
3. Read the affected implementation and existing tests before proposing or
   making a change.
4. Consult `../../../docs/audit/devices-first-pass.md` only for historical
   leads. Reverify every finding against current source and tests.
5. Inspect the worktree before editing and preserve unrelated or concurrent
   user changes.

## Classify the change

Identify every affected layer:

- models, endpoint configuration, and shared delegates
- `HardwareEngine` orchestration and operation serialization
- protocol command building and response parsing
- the transport-neutral `ICommTransport` contract
- serial, stream, TCP, named-pipe, virtual, or test transports
- logging, diagnostics, or optional observability

Keep device-specific semantics in protocols. Keep connection, buffering, read,
write, timeout, and disposal behavior in transports. Keep the engine focused on
orchestration.

## Decide dependencies without over-constraining

Treat Devices' current no-project-reference graph as the default baseline, not
as a permanent prohibition.

- Do not add a project dependency merely to reuse a convenient implementation.
- Allow a reference to `NekoLib.Core` when the requested capability explicitly
  requires shared Logging, Telemetry, or Inspection contracts and the
  repository's current freeze permits that work.
- When Core is justified, depend only on its contracts, null objects, and
  provider abstractions. Never reference concrete `NekoLib.Inspection` or
  `NekoLib.Logging` implementations from Devices.
- Keep Core unaware of Devices-specific operations, endpoints, protocols, and
  payload types. Core may expose the sink or contract; Devices owns the event
  semantics and snapshots.
- Do not treat `NekoLib.Pipes` as an automatic dependency of a named-pipe
  transport. Use `System.IO.Pipes` for a raw byte stream. Reference
  `NekoLib.Pipes` only when its higher-level RPC or pub/sub protocol is
  explicitly required.
- Keep COM-port emulators and live hardware harnesses outside the library
  dependency graph. Put runtime scenarios under `runtime_tests/` unless a
  fully in-process, hardware-independent test fixture is deliberately adopted.
- When changing the dependency graph, update the relevant `*.csproj`, package
  documentation, package-consumer probes, and tests together.

For observability work, read the current freeze in `../../../ROADMAP.md` and any
promoted Devices work in `../../../TODO.md`. Surface the architectural tradeoff
when the request does not clearly authorize an unfreeze; do not silently reject
Core as an invalid dependency.

## Preserve Devices invariants

- Preserve both target frameworks and use the project's declared
  `NETFRAMEWORK` and `NET_9` constants.
- Preserve nullable and implicit-using settings from each affected project.
- Avoid `record` for types shared with `net481`.
- Keep `ICommTransport` behavior transport-neutral. Do not leak TCP, pipe, or
  serial implementation details into the engine or protocols.
- Preserve cancellation as cancellation; do not convert
  `OperationCanceledException` into a failed hardware response.
- Keep timeout, quiet-period, buffering, and connection-closure semantics
  explicit and consistent across transports.
- Ensure a timed-out stream read cannot consume a later operation's response.
- Serialize operations and transport mutation according to the current locking
  model, and release gates on every failure path.
- Dispose streams, sockets, pipes, serial ports, cancellation sources, and
  synchronization primitives according to ownership.
- Preserve raw bytes exactly. Apply text encoding only on documented text
  paths.

## Add or update tests

- Mirror the source area under
  `../../../tests/NekoLib.Devices.Tests/Unit/`.
- Name tests `MethodName_Condition_ExpectedResult`.
- Use in-memory streams and transport/protocol fakes for deterministic unit
  behavior.
- Cover partial reads, delayed chunks, timeouts, cancellation, connection
  closure, reopen behavior, serialization, and disposal when those paths
  change.
- Validate real serial behavior with a COM-port emulator or runtime scenario;
  do not claim fake-transport coverage proves real serial I/O.
- Cover both target frameworks when conditional compilation or framework API
  behavior is involved.

## Verify proportionally

Start with the narrowest relevant test, then expand according to impact:

```powershell
dotnet test tests/NekoLib.Devices.Tests/Unit/NekoLib.Devices.Tests.Unit.csproj
dotnet build src/Devices/NekoLib.Devices/NekoLib.Devices.csproj -f net481
dotnet build src/Devices/NekoLib.Devices/NekoLib.Devices.csproj -f net9.0
dotnet test NekoLib.sln
```

Report exactly which transports, target frameworks, and runtime environments
were verified. Do not generalize stream or fake coverage into physical-device
coverage.
