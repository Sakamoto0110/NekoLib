# Pipes / long-running and recovery (E3-PIPE)

**Kind:** guide

**Lifecycle:** current

**Owner:** `NekoLib.Pipes`

**OS / target:** Windows, `net481` and `net9.0`

**Prerequisites:** none beyond the .NET SDK. No container, no service, no
hardware. The scenario allocates its own named pipe per run.

**Last verification:** 2026-08-11 — **automated runtime, two successful
2-minute development probes.** See [Verification record](#verification-record).
Both `net9.0` and `net481` completed four atomic cycles in 133 seconds with
75/75 checks passing, zero skipped, exit 0 and complete cleanup. The runs are
deliberately below the historical nominal smoke window. One compact
representative recovery sweep remains to execute all six scheduled faults;
duplicate full windows and a four-hour soak are not outcome-first gates.

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

All three modes. `--smoke` runs every workload class and then repeats it under
the client children's traffic until its window closes; `--recovery-rehearsal`
warms up, dispatches the seeded schedule, then runs the matrices again;
`--soak` runs the schedule and the cycle loop together, serialised through one
gate so an assertion is never made while a fault has the server away.

Client children are started for the two fault-bearing modes and not for smoke:
smoke's value is its assertions, and three processes of background traffic would
make every count in them approximate for no gain. Their own results become one
check of their own, so their work is part of the verdict rather than decoration.


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
  cycles;
- `token-cancellation` — a caller that withdraws mid-request, which is a
  different thing from a deadline expiring, and a clean next response;
- `client-children-correlation` — the client processes' own totals, asserting
  that no response ever reached the wrong process.

**`events`**
- `ordered-delivery` — a subscriber receives published events in publication
  order, waited for boundedly rather than slept at;
- `subscriber-churn` — 10 connect/disconnect cycles, after which the hub still
  serves;
- `overflow-drop-newest` and `overflow-disconnect-subscriber` — **both**
  supported bounded-queue policies, each against a subscriber that connects and
  never reads, asserting the truthful dropped count and the policy's own
  outcome: still connected, or disconnected.

  The dropped count is `EventMetrics.Failed`. That is not a guess: on overflow
  the hub completes the delivery as unsuccessful, so a failed delivery *is* a
  dropped event.

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
  is then bound again and served on;
- `server-initiated-disconnect` — the server goes away and the client learns by
  failing rather than by hanging.

**`recovery`** — six faults, each dispatched at its planned monotonic offset,
each with a documented terminal and a post-recovery probe:

| Kind | What it proves |
|---|---|
| `kill-server-process` | an in-flight request fails rather than hangs, the dead process releases the endpoint, a replacement binds it and answers |
| `kill-client-process` | the server sheds a client that died abruptly and keeps answering |
| `raw-peer-closes-mid-frame` | a peer that lies about a frame length does not disturb ordinary requests |
| `handler-delay-forces-timeout` | the timeout lands inside its bound and the next response is its own, not the abandoned one |
| `slow-subscriber-overflows-queue` | a subscriber that never drains blocks neither publishing nor ordinary requests |
| `dispose-while-requests-admitted` | disposal with a connection, a request and a subscriber all live does not throw and releases the name |

Faults that need a server to themselves use a private endpoint, so the shared
server the rest of the schedule depends on is never taken down by one of them.

## What is not covered

Every mode and every fault kind is implemented. These specified items are not:

- **metric stability under sustained traffic** is sampled but not asserted. The
  request, event, error and connection counters are recorded in `samples.csv`
  every cycle, and nothing yet fails a run for an implausible trend, because no
  baseline exists to derive one from. That is the same position E3-OBS took on
  memory, and for the same reason.
- **`MaxClients` saturation** — behaviour when more clients connect than the
  server admits.
- **event delivery under a server restart** — subscribers reconnect via
  `PipeEventClient.AutoReconnect`, which no check exercises across a kill.

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
4. For the remaining outcome-first gate, run
   `--recovery-rehearsal --rehearsal-duration 10m` on one representative
   target. Expected: exit `0`, all six faults reporting `ok`, and the client
   children's totals showing failures during the fault windows and successes
   outside them. The artifact will truthfully remain below the historical
   nominal rehearsal window.

## Verification record

| Date | Target | Result |
|---|---|---|
| 2026-08-10 | both | **Build-only.** Both targets build with no warnings. |
| 2026-08-10 | both | **Schedule determinism, automated.** `fnv1a64:42db44086ce556a2` on `net481` and `net9.0` and across repeated runs; seed 99 differs. This is a `--print-schedule` result, which starts nothing and touches nothing. Reproduced on `net9.0` on 2026-08-11. |
| 2026-08-11 | `net9.0` | **First execution, automated runtime. `--smoke --smoke-duration 2m`, exit 4.** A probe deliberately below the specified smoke window, and correctly flagged `belowSpecifiedWindow`, so it is not smoke evidence. 21 of 30 checks passed. **Every `request` check and every `protocol` check passed in both cycles**, across real processes on a real pipe. The nine failures were the four event-hub checks and `dispose-and-rebind-a-private-endpoint`. Cleanup was truthful: server child exited 0, endpoint released, no process or pipe left behind. Artifacts at `artifacts/validation/phase-e/e3pipe-smoke-net9.0-s20260808-20260811T013626269Z`. |
| 2026-08-11 | `net9.0` | **Re-run after the scenario fix below, exit 4, 23 of 30 passed.** `dispose-and-rebind-a-private-endpoint` now passes in both cycles at 237 ms. The seven remaining failures are all event-hub checks and are the confirmed product defect below, not a scenario problem. Artifacts at `artifacts/validation/phase-e/e3pipe-smoke-net9.0-s20260808-20260811T021739181Z`. |
| 2026-08-11 | `net9.0` | **First run after the product fix, exit 4.** Subscriber churn and rebind passed in every cycle, proving `PIPE-EVENTHUB-SLOTS` closed. The eight remaining failures were both overflow checks in four cycles; investigation confirmed the separate scenario endpoint defect below. 52 passed, 8 failed in 126 seconds, with complete cleanup. Artifacts at `artifacts/validation/phase-e/e3pipe-smoke-net9.0-s20260808-20260811T025626189Z`. |
| 2026-08-11 | `net9.0` | **Development probe passed, exit 0.** Four cycles, 75 passed, 0 failed, 0 skipped in 133 seconds. Server child exit 0, endpoint released, no scenario process or pipe remained. Artifacts at `artifacts/validation/phase-e/e3pipe-smoke-net9.0-s20260808-20260811T025942081Z`. |
| 2026-08-11 | `net481` | **Development probe passed, exit 0.** Four cycles, 75 passed, 0 failed, 0 skipped in 133 seconds. Server child exit 0, endpoint released, no scenario process or pipe remained. Artifacts at `artifacts/validation/phase-e/e3pipe-smoke-net481-s20260808-20260811T030208668Z`. |

**The first execution found a product defect**, which is why the caveat below
was worth writing before it ran.

### `PIPE-EVENTHUB-SLOTS` — product defect fixed 2026-08-11

`PipeEventHub` retains a subscriber slot after that subscriber disconnects,
unless an event is published afterwards. The hub therefore stops accepting
subscribers after `MaxEventSubscribers` lifetime connections and does not
recover on its own.

Confirmed on 2026-08-11 by a minimal reproduction that uses only the public API.
With `MaxEventSubscribers = 8`, subscribers 1 to 8 connected immediately and
subscriber 9 never connected — 20.0 s on `net9.0`, 21.6 s on `net481` — with
`AutoReconnect` left at its default and retrying throughout, and a 750 ms settle
after each disconnect, longer than the hub's own 500 ms poll. Publishing one
event then freed the slots immediately, which identifies the mechanism: the
keep-alive loop polls `pipe.IsConnected`, and on an outbound pipe that only
turns false once a write is attempted.

That is what the first run's four event checks reported. `subscriber-churn` connected 7
of 10 in the first cycle — 1 already taken by `ordered-delivery`, so 8 in total,
exactly the cap — and 0 in the second, after which nothing can subscribe again.

An authorized local design spike proved the server can create the event pipe as
`PipeDirection.InOut` while the existing `In`-only `PipeEventClient` remains
unchanged. Event delivery stayed intact in both access-policy modes on both
targets, a server read stayed pending while the client was live, and it returned
EOF when the client closed. `PipeEventHub` now uses that read as its liveness
signal, discards any subscriber input and retains `DrainSubscriber` as the only
event writer. The former 500 ms `IsConnected` polling loop is gone. No public
API, client, framing, access-policy semantics, metrics or overflow contract
changed.

The alternative native zero-byte `WriteFile` design was rejected rather than
implemented. It passed on `net481` but caused a latent process-fatal
`0xC0000005` in the `net9.0` runtime's IOCP callback. Focused product regressions
now cover `MaxEventSubscribers + 1` sequential quiet disconnects in both access
policies and a duplex subscriber writing bytes that the hub discards without
corrupting event delivery. The original public-API reproduction also now exits
0 with all 9 subscribers connecting: 6.9 seconds on `net9.0` and 10.3 seconds on
`net481`, with the base and event pipe names released after each run.

### `PIPE-REBIND-RACE` — scenario defect, fixed 2026-08-11

`dispose-and-rebind-a-private-endpoint` asserted `Endpoint.IsBound` on the line
immediately after `server.Start()`. `PipeServer.Start()` hands its accept loop to
the thread pool, so the name appears shortly after `Start()` returns rather than
during it, and the check was losing a race it should never have been running —
it failed at 2 ms. It now waits boundedly for the bind, in the same style the
same check already used when waiting for the name to be released. The re-run
above confirms it passes at 237 ms, which is also a measurement of how late the
bind really is. `PipeServer.Start()`'s contract was not changed.

### `PIPE-OVERFLOW-ENDPOINT` — scenario defect, fixed 2026-08-11

The two private overflow checks created `PipeEventHub` with a base endpoint but
connected their raw subscriber to that base instead of its `.events` companion.
The subscriber therefore never reached the hub; both checks expired in their
five-second connect bound. They now use `Endpoint.EventsFor(endpoint)`, the same
canonical resolver as the rest of the scenario. The final probes prove both
policies: `DropNewest` keeps the slow subscriber while counting failed
deliveries, and `DisconnectSubscriber` removes it after overflow.

**No full nominal-window smoke, rehearsal, soak or campaign has been run.** The
successful short probes remain development evidence. Under outcome-first
acceptance, the only missing runtime outcome is one representative compact
recovery sweep that proves all six faults, expected terminals, post-recovery
probes, artifacts, and cleanup. The source was run from a dirty working tree
based on `d515137`; the assembly informational version in the artifacts does
not attest the uncommitted product diff. No package was created.

The caveat that motivated running a short probe first, kept because it proved
correct: **a scenario that has never executed is the least-verified thing in this
repository.** E3-OBS's first sustained run found two defects in its own
assertions within fifteen minutes; this one is more complex — three processes, a
killed server, a rebound endpoint — and its first execution found one defect of
its own and one in the module it tests.
