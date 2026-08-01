# Devices / com0com Serial Parity

**Kind:** guide

**Lifecycle:** current

**Owner:** Devices

**OS / target:** Windows; `net481` and `net9.0`

**Prerequisites:** com0com virtual pairs `COM9 <-> COM19` and `COM10 <-> COM20`; an independent NekoPcbEmulator build with serial support; no other process holding those ports

**Last verification:** 2026-08-01; interactive pass on `net481` and `net9.0`
from a NekoLib working tree based on `628442a`; the independent emulator serial
worktree was based on `9c9528f`; middleware behavior was also confirmed
visually by the user

## Purpose

Validate `SerialCommTransport`, `HardwareEngine`, and `ProtocolRaw` against an
external byte-stream and protocol oracle. The emulator owns `COM9` for PCB-A
and `COM10` for PCB-B; this scenario opens the partner ports `COM19` and
`COM20`.

The executable verifies:

- `115200/8/N/1`, no handshake, DTR, RTS, and configured timeout snapshots;
- bounded no-data timeouts and cancellation with an infinite port read timeout;
- `ReadLine`, `ReadExact`, and `ReadAll` over a real virtual COM driver;
- close/reopen behavior;
- PCB-A Latin-1 `SYS PING` and `SYS ID` exchanges;
- PCB-B binary PING framing, sequence, response opcode, and
  CRC-16/CCITT-FALSE.

The oracle is intentionally outside NekoLib. The scenario independently
constructs and validates the two wire protocols instead of sharing emulator
protocol code.

## Build

From the NekoLib repository root:

```powershell
dotnet build runtime_tests/Devices/Com0Com/NekoLib.Devices.RuntimeTests.Com0Com/NekoLib.Devices.RuntimeTests.Com0Com.csproj
```

## Launch

First launch the sibling emulator on its ends of the pairs:

```powershell
dotnet run --project ..\NekoPcbEmulator\src\NekoPcbEmulator.App\NekoPcbEmulator.App.csproj -- --power all --serial --no-main --com-a COM9 --com-b COM10
```

Wait until both boards report serial endpoints. Then, in another terminal, run
the NekoLib scenario for each target:

```powershell
dotnet run --project runtime_tests/Devices/Com0Com/NekoLib.Devices.RuntimeTests.Com0Com/NekoLib.Devices.RuntimeTests.Com0Com.csproj -f net481 -- --pcb-a COM19 --pcb-b COM20
dotnet run --project runtime_tests/Devices/Com0Com/NekoLib.Devices.RuntimeTests.Com0Com/NekoLib.Devices.RuntimeTests.Com0Com.csproj -f net9.0 -- --pcb-a COM19 --pcb-b COM20
```

## Procedure and expected result

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

## Cleanup and side effects

The scenario closes both client ports even when a check fails. Stop the
NekoPcbEmulator process after the run to release `COM9` and `COM10`. The
scenario does not create, rename, or remove com0com pairs and writes no data
outside ordinary build outputs.

## Verification record

- **2026-08-01 / interactive:** built both targets with 0 warnings and 0
  errors. Both `net481` and `net9.0` runs passed every PCB-A and PCB-B check
  against NekoPcbEmulator over com0com pairs `COM9 <-> COM19` and
  `COM10 <-> COM20`. All four ports were reopened successfully after cleanup.
- **2026-08-01 / user-observed visual check:** the middleware used the serial
  endpoints successfully and the resulting board behavior was confirmed in the
  emulator UI. This corroborates the executable protocol checks without
  replacing them.
- The emulator serial changes were still uncommitted during this run. The
  result proves the tested working-tree combination; repeat after both sides
  have immutable commits before using it as release evidence.
