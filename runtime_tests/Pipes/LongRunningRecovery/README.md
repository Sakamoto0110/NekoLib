# Pipes / long-running and recovery (E3-PIPE)

**Kind:** guide

**Lifecycle:** current

**Owner:** `NekoLib.Pipes`

**OS / target:** Windows, `net481` and `net9.0`

**Prerequisites:** none beyond the .NET SDK. No container, no service, no
hardware. The scenario allocates its own named pipe per run.

**Last verification:** 2026-08-10 — **build-only.** See
[Verification record](#verification-record). **This scenario is a first pass and
is not complete**; see [What is not implemented yet](#what-is-not-implemented-yet)
before citing anything from it.

## Purpose

Drive `NekoLib.Pipes` across **real process boundaries** over **real Windows
named pipes**: a controller, a server child and client children, none of which
share an address space.

That separation is the point. In-process tests can prove a handler returns the
right message; they cannot prove that a connection survives a peer that lies
about a frame length, that a killed server releases its endpoint, or that a name
can be bound again afterwards.

## Architecture, and why one executable

One binary in three roles rather than three projects:

| Role | What it is |
|---|---|
| **controller** (default) | owns the run, the artifacts and every check; starts the children; asserts as an ordinary client would |
| **server** (`--role server`) | a real process hosting `PipeServer` and its `PipeEventHub` |
| **client** (`--role client`) | a real process generating request/response traffic |

The operating system sees three unrelated processes on a real pipe, which is
what the suite asks for. Three `.csproj` files would add build surface without
adding a byte of separation.

**Children assert nothing.** They are traffic; the verdict belongs to the
controller, which reads their exit codes and their small result documents.
Scattering assertions across processes would make a deliberately killed child
look like a failed check.

### The harness question this scenario was meant to answer

Before it was written, this scenario was expected to force a decision: does the
shared harness grow multi-process support?

**It does not, and that is the finding.** The controller owns the single
`RunArtifacts` and writes the one `result.json` the suite specifies; the children
are workload rather than workers. Nothing in the harness changed for E3-PIPE.
Multi-process support would have been speculative generality for a shape one
scenario needs, which is exactly what the harness's second rule forbids.

## What a passing run does *not* establish

- **Nothing about security.** Every process is local and runs as the same user.
  This is not evidence about pipe ACLs, remote hosts, elevated users or an
  adversarial peer, and the suite forbids claiming any of them from a same-user
  test.
- **Nothing was added to the module.** Every fault comes from a process the
  controller started, a handler this project wrote, or a raw pipe peer it opened
  itself. No fault-injection or control API was added to `NekoLib.Pipes`; a
  privileged control plane is explicitly forbidden, and a scenario that added
  one would be testing its own back door.
- **The limits are deliberately small.** A 64 KiB frame limit against the
  module's 1 MiB default, and a subscriber queue of 8. That makes the over-limit
  and overflow paths reachable in seconds. It proves their mechanics, not a
  capacity figure.

## Build

```powershell
dotnet build runtime_tests\Pipes\LongRunningRecovery\NekoLib.Pipes.RuntimeTests.LongRunningRecovery\NekoLib.Pipes.RuntimeTests.LongRunningRecovery.csproj
```

## Launch

```powershell
.\runtime_tests\Pipes\LongRunningRecovery\NekoLib.Pipes.RuntimeTests.LongRunningRecovery\bin\Debug\net9.0\NekoLib.Pipes.RuntimeTests.LongRunningRecovery.exe --smoke
```

`--role server` and `--role client` exist for the controller to launch and are
not meant to be run by hand.

## What is implemented

Each is a check with its own exit-code contribution, in four phases.

**`request`**
- `payload-sizes` — 32 B, 4 KiB and 60 KiB round-trip byte-for-byte against a
  64 KiB limit;
- `concurrent-correlation` — 8 concurrent clients × 12 requests, asserting no
  response ever reaches the wrong caller;
- `error-contracts` — an unmapped operation returns `not_found`, a throwing
  handler returns `exception`, and the connection stays usable after both;
- `timeout-does-not-corrupt-the-next-response` — a request that outlives its
  timeout fails inside its bound, and the next request on a fresh connection
  receives its own response rather than the abandoned one;
- `client-reconnect-cycles` — 40 client-initiated connect/request/disconnect
  cycles.

**`events`**
- `ordered-delivery` — a subscriber receives published events in publication
  order, waited for boundedly rather than slept at;
- `subscriber-churn` — 10 connect/disconnect cycles, after which the hub still
  serves.

**`protocol`**
- `oversize-request-refused` — an over-limit request is refused and the
  connection survives;
- `oversize-response-reported` — an over-limit response returns
  `response_too_large` rather than truncating or dropping;
- `malformed-peer-does-not-disturb-the-server` — a raw peer that promises 4096
  bytes and sends 16, and another that connects and closes without writing,
  neither of which stops ordinary requests afterwards.

**`lifecycle`**
- `dispose-and-rebind-a-private-endpoint` — a server disposed while a request is
  in flight does not throw, releases its name within a bounded wait, and the name
  is then bound again and served on.

## What is not implemented yet

Stated plainly, because a scenario that looks finished and is not is worse than
one that is obviously partial. **Non-smoke modes refuse to start** rather than
report success without injecting anything.

- **No fault dispatcher.** The seeded schedule is generated, persisted and
  hash-stable, and `--recovery-rehearsal` and `--soak` exit `3` with a message
  saying so. The six fault kinds are declared and described but nothing acts on
  them: killing the server and a client, closing a raw peer mid-frame, delaying a
  handler, overflowing a slow subscriber, and disposing under admitted load.
- **No client children are started.** `--clients` parses and is unused, so the
  sustained multi-process load and the per-client result documents do not run
  yet. The concurrency that *is* covered is in-controller, across 8 clients.
- **No sustained window.** `--smoke-duration` parses but the matrices run once
  rather than cycling to fill the suite's 15–30 minutes.
- **Missing checks** from the specification: the `DisconnectSubscriber` overflow
  policy (only `DropNewest` is configured), truthful dropped-event metrics,
  server-initiated disconnect as its own check, token cancellation as distinct
  from timeout, disposal while *publishes and subscribers* are active, and
  metric stability under sustained traffic.

## The fault schedule

Generated before anything starts and persisted before the first process, even
though nothing dispatches it yet.

```powershell
.\...\NekoLib.Pipes.RuntimeTests.LongRunningRecovery.exe --recovery-rehearsal --print-schedule
```

Seed `20260808` produces `fnv1a64:42db44086ce556a2` on **both** targets and on
repeated runs.

### A determinism defect this caught immediately

The first version of the fault vocabulary interpolated the run's pipe name into
each fault's target. That string is covered by the schedule hash, and the pipe
name derives from the campaign id — which carries the target framework **and a
millisecond timestamp**. The same seed therefore produced a different hash on
every single run, and a different one again on the other target.

A fault target must describe the *class* of resource, never the instance. The
instance belongs in `environment.json`, which records the endpoint. The check
that caught it costs nothing and should be the first thing run against any new
scenario's schedule.

## Cleanup and side effects

The scenario creates one named pipe per run — `nekolib.e3pipe.<campaign-id>`,
plus its `.events` companion — and the child processes it starts. It touches
nothing else: no service, no machine configuration, no environment variable.

Cleanup asks the server to shut down before forcing anything, waits boundedly for
each child, and forces only children it started, re-verifying process name before
killing so a reused PID cannot make it end a stranger. It then asserts that **the
endpoint is unbound again**, because a named pipe is a machine-wide resource and
a leaked one would block the next run. A still-bound endpoint is a cleanup
problem and exit code `6`.

Preflight refuses to start if the allocated endpoint is already bound.

## Procedure and expected result

1. Build. Expected: success, no warnings, both targets.
2. `--print-schedule` twice on each target. Expected: the same hash all four
   times; a different `--seed` gives a different hash.
3. `--smoke`. Expected: exit `0`, every check passing, and a cleanup line
   reporting the endpoint released.
4. `--recovery-rehearsal`. Expected **today**: exit `3` with the message that no
   dispatcher exists.

## Verification record

| Date | Target | Result |
|---|---|---|
| 2026-08-10 | both | **Build-only.** Both targets build with no warnings. |
| 2026-08-10 | both | **Schedule determinism, automated.** `fnv1a64:42db44086ce556a2` on `net481` and `net9.0` and across repeated runs; seed 99 differs. This is a `--print-schedule` result, which starts nothing and touches nothing. |

**No smoke, rehearsal, soak or campaign has been run.** The scenario has never
opened a pipe outside a build. That is deliberate: the current strategy is to
build every scenario before the execution phase begins, so this is *pending
validation*, not a suspected fault — with the honest caveat that a scenario
never executed is the least-verified kind of pending there is.
