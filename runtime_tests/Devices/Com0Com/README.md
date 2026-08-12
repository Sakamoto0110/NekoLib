# Devices / com0com Serial Parity and Recovery (E3-DEV)

**Kind:** guide

**Lifecycle:** current

**Owner:** Devices

**OS / target:** Windows; `net481` and `net9.0`

**Prerequisites:** com0com virtual pairs `COM9 <-> COM19` and `COM10 <-> COM20`.
The **oracle pass** additionally needs an independent NekoPcbEmulator build with
serial support holding `COM9`/`COM10`. The **automated modes** need those two
ports **free**, because they open them themselves. No other process may hold any
of the four.

**Last verification:**

- **Oracle pass — interactive, 2026-08-01**, unchanged and still valid. See
  [Verification record](#verification-record).
- **E3-DEV automated modes — outcome-first gate complete, 2026-08-12.** With
  NekoPcbEmulator stopped, the compact `net9.0` recovery sweep passed **33/33
  checks, zero failed, zero skipped, exit 0** in 467.2 seconds, with the
  **recovery phase 5/5**: all five peer faults reached their expected terminal
  and were each followed by a clean request. COM19, COM20, COM9 and COM10 were
  reopened and released. Artifact
  `artifacts/validation/phase-e/e3dev-recovery-net9.0-s20260808-20260812T140422862Z`.
  The run is below the historical nominal rehearsal window and records
  `belowSpecifiedWindow: true`. Earlier two-minute probes on both targets remain
  recorded below.

## Two paths, deliberately mutually exclusive

This one executable does two different things, and which one it does is decided
by whether a mode flag is present.

| Invocation | What it is | What it proves |
|---|---|---|
| no mode flag | the original **oracle pass** | protocol parity against an implementation nobody here wrote |
| `--smoke`, `--recovery-rehearsal`, `--soak` | **E3-DEV** | transport behaviour under faults nobody can ask that implementation to produce |

They cannot run at the same time, because both want `COM9` and `COM10`. That is
the accepted cost of a decision taken before any code was written, recorded in
[`TODO.md`](../../../TODO.md).

The reasoning is short. The suite requires faults of "emulator delay, silence,
malformed frame, disconnect, and restart". The emulator can supply none of them:
it is an independent oracle in a separate repository with no reference to
NekoLib, and giving it a control channel would make it an accomplice rather than
an oracle — the same objection that keeps a `TestControl` API out of every
product module. So E3-DEV opens the port the emulator would have held and
answers for itself.

**Neither run replaces the other, and the automated modes are not protocol
evidence.** When both ends of a conversation are written in this project, their
agreement proves that framing was carried intact, not that either half is right.
That claim belongs to the oracle pass alone.

## The oracle pass — unchanged

The emulator owns `COM9` for PCB-A and `COM10` for PCB-B; the scenario opens the
partner ports `COM19` and `COM20`. It verifies:

- `115200/8/N/1`, no handshake, DTR, RTS, and configured timeout snapshots;
- bounded no-data timeouts and cancellation with an infinite port read timeout;
- `ReadLine`, `ReadExact`, and `ReadAll` over a real virtual COM driver;
- close/reopen behavior;
- PCB-A Latin-1 `SYS PING` and `SYS ID` exchanges;
- PCB-B binary PING framing, sequence, response opcode, and
  CRC-16/CCITT-FALSE.

The scenario independently constructs and validates both wire protocols instead
of sharing emulator protocol code. Its code moved from `Program.cs` into
`OracleParity.cs` when E3-DEV was added, with no change to its options, its
console output, its checks, or its exit codes (`0` and `1`).

### Launch

```powershell
dotnet run --project ..\NekoPcbEmulator\src\NekoPcbEmulator.App\NekoPcbEmulator.App.csproj -- --power all --serial --no-main --com-a COM9 --com-b COM10
```

Wait until both boards report serial endpoints. Then, in another terminal:

```powershell
dotnet run --project runtime_tests/Devices/Com0Com/NekoLib.Devices.RuntimeTests.Com0Com/NekoLib.Devices.RuntimeTests.Com0Com.csproj -f net481 -- --pcb-a COM19 --pcb-b COM20
dotnet run --project runtime_tests/Devices/Com0Com/NekoLib.Devices.RuntimeTests.Com0Com/NekoLib.Devices.RuntimeTests.Com0Com.csproj -f net9.0 -- --pcb-a COM19 --pcb-b COM20
```

### Procedure and expected result

1. Confirm the emulator reports `serial://COM9@115200` and
   `serial://COM10@115200`.
2. Run the `net481` command. It must print both PCB stages followed by a single
   `PASS` line and exit with code `0`.
3. Run the `net9.0` command. It must produce the same result.
4. Confirm the emulator traffic view records real PCB-A and PCB-B PING
   requests. Process existence alone is not readiness evidence.

Any missing port, open failure, timeout, unexpected byte, sequence mismatch,
opcode mismatch, or CRC mismatch exits with code `1` and prints the failing
contract.

## E3-DEV — the automated modes

### The owned peer

The scenario opens all four ports: the transport under test takes `COM19` and
`COM20` as before, and a scenario-owned peer takes `COM9` and `COM10`. The peer
speaks both protocols and carries five switches — delay, silence, malformed
frame, disconnect, restart — none of which exists anywhere in
`NekoLib.Devices`. The transport under test only ever sees bytes arriving late,
not at all, in pieces, wrong, or from a port that has gone away.

The peer adds one request the emulator does not have: `SYS ECHO <token>;`,
answered with `OK <token>;`. The token is what makes "the next operation
received its own response" a decidable question at all — without one, a stale
response and a fresh one are the same bytes.

### Ownership

The controller adopts **only the four port names it was given** and starts **no
processes at all**. It never enumerates ports to find something to use;
`SerialPort.GetPortNames()` is read once, to check that the configured names are
installed and to record the machine's port list in `environment.json`.

### Build

```powershell
dotnet build runtime_tests\Devices\Com0Com\NekoLib.Devices.RuntimeTests.Com0Com\NekoLib.Devices.RuntimeTests.Com0Com.csproj
```

### Launch

```powershell
.\runtime_tests\Devices\Com0Com\NekoLib.Devices.RuntimeTests.Com0Com\bin\Debug\net9.0\NekoLib.Devices.RuntimeTests.Com0Com.exe --smoke
```

Options beyond the suite's shared set: `--smoke-duration`, `--pcb-a`, `--pcb-b`,
`--peer-a`, `--peer-b`. The defaults are the documented pairs, so the command
above needs no arguments on this machine.

### Preflight

Before any check runs, the controller requires:

1. the four configured names to be distinct and installed;
2. both peer ports to open — **the failure that means the emulator is still
   running**, reported as such with the remedy;
3. **both pairs to be cross-connected**, proved by a real `SYS PING` exchange on
   one pair and a real binary PING on the other.

That third probe closes something no document had established. The wiring was
recorded from a port list, and a port list says a name exists — not what it is
joined to. Every failure here is exit code `3`: an environment result, never a
product finding.

### What is implemented

All three modes. `--smoke` runs every workload class and then repeats it until
its window closes; `--recovery-rehearsal` warms up, dispatches the seeded
schedule, then runs the matrices again; `--soak` runs the schedule and the cycle
loop together, serialised through one gate so an assertion is never made while a
fault has a peer silent or disconnected.

**`transport`**

- `open-request-close-reopen-cycles` — eight open/request/close/reopen cycles on
  one transport instance;
- `finite-timeout-then-success` — `ReadLine`, `ReadExact` and `ReadAll` each
  return their documented no-data result inside the bound, then an ordinary
  request succeeds;
- `cancellation-then-success` — a pending read observes its token under **both**
  an infinite and a finite configured port timeout, and recovery follows each;
- `endpoint-switching-allow-and-reject` — a switch while open is refused through
  all three entry points (`Open(port)`, `Configure(cfg)`, the `PortName` setter)
  and accepted after `Close`, after which the other pair answers;
- `concurrent-callers-are-serialized` — six concurrent `HardwareEngine`
  operations, each identified by its own sequence byte, none crossing;
- `late-response-does-not-survive-a-reopen` — a response abandoned by a
  timed-out read is gone after the port is reopened;
- `late-response-stays-attributable` — see below.

**`protocol`**

- `pcb-a-text-framing` — `ReadLine` strips the terminator, `ReadExact` preserves
  every byte, `ReadAll` returns the whole reply once the line goes quiet;
- `pcb-b-binary-framing-and-crc` — four exchanges, each validated for sequence,
  opcode and CRC-16/CCITT-FALSE;
- `partial-chunks-inside-the-quiet-period-are-reassembled` — 3-byte pieces 10ms
  apart against a 50ms quiet period arrive whole;
- `a-gap-beyond-the-quiet-period-ends-the-read` — 3-byte pieces 300ms apart
  return a proper prefix, which is the documented contract rather than a defect,
  and the remainder is cleared by a reopen;
- `configuration-parity` — baud, data bits, stop bits, parity, handshake, DTR,
  RTS, newline and both timeout kinds applied and read back, plus one
  configuration opened and used;
- `encoding-parity` — a Latin-1 payload sent as bytes round-trips intact, while
  the same text through `Write(string)` is coerced to ASCII, which is that
  overload's documented behaviour.

**`lifecycle`**

- `dispose-during-an-active-operation` — disposal with a read in flight returns
  inside a bound, ends that read, is idempotent, refuses `Close` and `Open`
  afterwards with `ObjectDisposedException`, and releases the port so a
  replacement transport binds the same endpoint.

**`recovery`** — five faults, each dispatched at its planned monotonic offset,
each with a documented terminal and a post-recovery probe:

| Kind | What it proves |
|---|---|
| `peer-delays-response` | the caller's finite timeout expires inside its bound, and after a reopen the next request gets its own response rather than the late one |
| `peer-falls-silent` | three consecutive no-data results, then the same transport serves an ordinary request |
| `peer-sends-malformed-frame` | the bad bytes reach the caller verbatim rather than being hidden or repaired, the scenario's validator rejects them, and the next well-formed exchange succeeds |
| `peer-disconnects` | an exchange against a departed far end ends quickly instead of hanging, and the **same endpoint** serves again once it returns |
| `peer-restarts` | two close/reopen cycles on the far end, each followed by a working exchange, with no leaked handle |

### One assertion that was deliberately weakened, and why

The suite asks that "no response from a timed-out/cancelled operation [is]
consumed by the next operation". On a serial port that is not a property the
transport can offer: the line is a byte stream with no correlation of its own,
so a late reply is simply still sitting in the driver's buffer. Asserting that
it vanishes would be asserting a contract `SerialCommTransport` does not have,
and the check would fail on correct behaviour.

So the requirement is split in two:

- **`late-response-does-not-survive-a-reopen` asserts the real recovery.** Close,
  reopen, and the next request receives its own token.
- **`late-response-stays-attributable` asserts what holds without one.** Every
  byte the next read receives must belong to one identifiable exchange — its own
  reply, the abandoned one, or both in order, each intact. A mixture would be
  corruption; a prefix of one inside the other would be a framing defect. The
  check records which of the three occurred as a note.

That is the same position E3-OBS took on an abandoned telemetry scope: describe
what the implementation does rather than invent a stronger contract for it.

### A build-time assumption refuted by runtime evidence

The build-first version expected `SerialPort` to reject `RtsEnable` assignments
while the handshake was `RequestToSend` or `RequestToSendXOnXOff`. The first
real run showed the opposite, and a focused public-API probe confirmed the full
four-combination matrix on both targets without opening a port: every snapshot
was accepted and `PortInfo` reported the requested handshake and RTS value.

That was a scenario and documentation defect, not a product limitation. The
check now asserts the observed configuration snapshot. It still makes no claim
that com0com enforces hardware flow control on the wire; the pair is virtual and
the scenario deliberately does not open and write through non-`None` handshake
configurations.

### What a passing run does *not* establish

- **Not protocol evidence.** In these modes both ends were written here.
- **Nothing was added to the module.** Every fault is a switch on the scenario's
  own peer. `NekoLib.Devices` gained no fault-injection or control API, and the
  emulator was neither modified nor given a control channel.
- **Nothing physical.** com0com is a virtual pair. It does not emulate baud,
  framing, line levels or noise, so this says nothing about UART behaviour,
  wiring, USB adapters or electrical conditions. What it does prove is the real
  Windows serial API against a real driver.
- **Nothing about Linux.** Windows only, both targets on one machine and one
  driver.
- **Configuration parity is about fields, not wires.** The round trip in that
  check succeeds with the two ends at different nominal line rates, because on a
  virtual pair the rate reaches nothing.

### The fault schedule

Generated before anything opens and persisted before the first exchange.

```powershell
.\runtime_tests\Devices\Com0Com\NekoLib.Devices.RuntimeTests.Com0Com\bin\Debug\net9.0\NekoLib.Devices.RuntimeTests.Com0Com.exe --recovery-rehearsal --print-schedule
```

Seed `20260808` produces `fnv1a64:7496700bf4b75339` on **both** targets and on
repeated runs. **No COM name appears in any fault target**, deliberately: the
target string is covered by the hash, so naming a port would make the same seed
produce a different plan on a machine whose pairs are numbered differently. The
instance belongs in `environment.json`, which records all four.

### Cleanup and side effects

The scenario opens four COM ports and starts no process. It creates, renames and
removes no com0com pair, and writes nothing outside its own run directory under
`artifacts/validation/phase-e/` and ordinary build output.

Cleanup disposes both peers, reports any transport that was created and never
disposed, and then **reopens and releases all four ports**. That last step is the
assertion, not the housekeeping: a COM port is a machine-wide resource, and a
port that cannot be reopened is a leaked handle. It is the serial equivalent of
E3-PIPE's "the endpoint can be rebound", and a failure is a cleanup problem and
exit code `6`.

Ctrl+C is taken by the scenario rather than left to kill the process, which
matters more here than elsewhere: a killed process leaves four ports held until
the OS reclaims them, and the next run would find them taken.

### Exit codes

`0` pass; `2` usage; `3` prerequisite (a port missing, taken, or a pair not
cross-connected); `4` a check failed; `6` cleanup did not reconcile; `7` an
unexpected failure; `8` interrupted; `9` the incremental check log is
incomplete.

### Procedure and expected result

1. Build. Expected: success on both targets, with no warning from the scenario.
2. `--print-schedule` twice on each target. Expected: the same hash all four
   times; a different `--seed` gives a different hash.
3. Stop the emulator, then `--smoke`. Expected: exit `0`, every check passing,
   and a cleanup block reporting all four ports reopened and released.
4. `--recovery-rehearsal --rehearsal-duration 10m` on one representative
   target — the run that closed the outcome-first gate on 2026-08-12. Expected:
   exit `0`, all five faults reporting `ok`, successful clean requests after
   each recovery, and all four ports reopened/released. The artifact truthfully
   remains below the historical nominal rehearsal window.

## Verification record

| Date | Path | Result |
|---|---|---|
| 2026-08-01 | oracle, interactive | Built both targets with 0 warnings and 0 errors. Both `net481` and `net9.0` runs passed every PCB-A and PCB-B check against NekoPcbEmulator over com0com pairs `COM9 <-> COM19` and `COM10 <-> COM20`. All four ports were reopened successfully after cleanup. |
| 2026-08-01 | oracle, user-observed visual check | The middleware used the serial endpoints successfully and the resulting board behavior was confirmed in the emulator UI. This corroborates the executable protocol checks without replacing them. |
| 2026-08-10 | E3-DEV, build-only | Both targets build with no warnings from the scenario. The 40 warnings in the build output are the pre-existing `NekoLib.Devices` nullable identities and are unchanged from before this work. |
| 2026-08-10 | E3-DEV, automated | **Schedule determinism.** `fnv1a64:7496700bf4b75339` on `net481` and `net9.0` and across repeated runs; seed `99` differs; the 4-hour soak plan matches across targets too. `--print-schedule` opens no port and starts nothing. |
| 2026-08-10 | E3-DEV, automated | **Dispatch and exit codes**, using port names that do not exist so no port is opened: the oracle path still exits `1` on a missing port, the automated preflight exits `3` and names the installed ports, and an unknown option exits `2`. Identical on both targets. |
| 2026-08-10 | E3-DEV, automated | **Environment record**, written by the same port-absent runs: `com0com 3.0.0.0` read from `C:\Program Files (x86)\com0com\com0com.sys`, no setup gaps, and the machine's installed ports recorded as COM1, COM3, COM4, COM9, COM10, COM19, COM20 — the list the 2026-08-10 prerequisite note reported. Identical on both targets. |
| 2026-08-11 | E3-DEV, first `net9.0` two-minute probe | Exit 4 in 127 seconds: 156 passed, 26 failed, 0 skipped over 12 cycles. Both cross-connections were proven. The same two checks failed in every cycle: the slow-chunk check reopened 300 ms before the final line terminator could arrive, and the RTS/CTS check asserted an unexecuted platform assumption that runtime refuted. Cleanup reconciled and all four ports reopened. |
| 2026-08-11 | E3-DEV, corrected two-minute probes | The same command exited 0 on `net9.0` and `net481`: 168/168 checks passed, 0 failed, 0 skipped, 11 cycles in 124 seconds on each target. The peers closed normally, every transport was disposed, and COM19, COM20, COM9 and COM10 were each reopened and released. These are development probes below the 15-minute smoke minimum, not smoke-gate evidence. |
| 2026-08-12 | E3-DEV, compact recovery sweep, `net9.0` | **Outcome-first gate passed, exit 0**, first attempt. `--recovery-rehearsal --rehearsal-duration 10m --seed 20260808`, 467.2 seconds, **33/33 checks passed, 0 failed, 0 skipped** across transport 14, protocol 12, lifecycle 2 and **recovery 5**. NekoPcbEmulator was confirmed stopped and all four ports verified free beforehand; preflight then proved both cross-connections with a real exchange. Each peer fault reached its expected terminal and was followed by a clean request: `peer-delays-response` timed out at 403 ms rather than waiting; `peer-restarts` completed two restarts with the caller's port still working without reopening; `peer-disconnects` gave no data and no exception in 804 ms; `peer-sends-malformed-frame` was rejected on CRC-16/CCITT-FALSE, `0xE52D` against the expected `0x1A2D`; `peer-falls-silent` produced the documented no-data result three times before the same transport served normally. PCB-A text framing and PCB-B binary framing/CRC both passed in each matrix pass. Counters 99 operations / 75 successes / 20 expected failures / 4 cancellations / **0 unexpected failures**. Cleanup closed both peers — COM9 after 60 responses and 3 restarts, COM10 after 25 responses — and **reopened and released COM19, COM20, COM9 and COM10**; `cleanupProblems` and `setupGaps` empty, `stderr.log` empty, no scenario or emulator process remained, and the four ports were independently re-verified free afterwards. Schedule `fnv1a64:ca1bf7c85e9c5f48` persisted before the first exchange; `belowSpecifiedWindow: true`. Artifact `artifacts/validation/phase-e/e3dev-recovery-net9.0-s20260808-20260812T140422862Z`. The recorded `repository.dirty: true` comes solely from the then-uncommitted E3-PIPE scenario fix; nothing under `src/Devices` or this scenario changed. |

The emulator serial changes were still uncommitted during the 2026-08-01 run.
That result proves the tested working-tree combination; repeat it after both
sides have immutable commits before using it as release evidence.

**No full nominal-window E3-DEV smoke, rehearsal, soak or campaign has been
run.** The compact 2026-08-12 sweep closed the outcome-first gate — all five
peer faults, their expected terminals, clean post-recovery requests, artifacts
and four-port release — but it is a ten-minute run that records
`belowSpecifiedWindow: true` and must not be cited as a nominal rehearsal.
Runtime fault coverage exists on `net9.0` only; `net481` has real-COM parity
from its builds, isolated checks and two-minute probes rather than from a fault
sweep. A `net481` runtime repeat, duplicate full windows and a four-hour soak
remain optional.

The limits stated elsewhere in this document are unchanged by that run: it is
real Windows serial API behaviour over a real com0com driver, **not** physical
UART levels, wiring, USB adapters or electrical conditions, and **not** protocol
parity — with both ends written here, their agreement proves framing was carried
intact, not that either half is right. That claim belongs to the oracle pass
alone, which requires NekoPcbEmulator running and therefore cannot execute at
the same time.

## Not covered

- **The scenario is not registered in `E3-ORCH`'s `campaign.json`.** Doing so
  would mean declaring a COM-pair prerequisite and adoption contract that the
  orchestrator has no way to validate today, and asserting it before this
  scenario has run once would be a claim rather than a fact. E3-PIPE is
  unregistered for the same reason.
- **`--smoke-duration` is now duplicated a third time** — E3-OBS, E3-PIPE and
  here. The harness README's own rule says it moves once a second scenario needs
  it, and that threshold passed at E3-PIPE. It was left alone here because
  moving it would edit two finished scenarios, which is outside this task.
- **Handshake behaviour on the wire.** Non-`None` handshakes are applied and
  read back but never opened and written through, because a handshake nobody
  asserts on the far end can block a write for the whole write timeout, and a
  check that can hang is worse than one that proves slightly less.
- **Baud, parity and framing enforcement.** A virtual pair does not implement
  them, so no check asserts them.
