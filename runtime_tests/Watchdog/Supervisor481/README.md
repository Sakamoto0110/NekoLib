# Watchdog Supervisor481 Scenario

**Kind:** guide

**Lifecycle:** current

**Owner:** Watchdog supervision and Pipes control channel

**OS / target:** Windows, `net481`

**Prerequisites:** .NET Framework 4.8.1 developer/runtime support, interactive
desktop session, permission to start and terminate the selected target process

**Last verification:** build-only on 2026-08-01 against repository baseline
`c473966` plus the Phase C promotion working tree; interactive procedure not
rerun

## Purpose

Host `WatchdogRuntime` in a WinForms dashboard, supervise a harmless executable,
and drive the runtime through real named-pipe RPC and event channels. This
validates process restart behavior and the existing Watchdog/Pipes boundary; it
does not validate the separately deployed `NekoLib.Watchdog.Host` package.

## Build

```powershell
dotnet build runtime_tests/Watchdog/Supervisor481/NekoLib.Watchdog.RuntimeTests.Supervisor481/NekoLib.Watchdog.RuntimeTests.Supervisor481.csproj
```

## Launch

```powershell
dotnet run --project runtime_tests/Watchdog/Supervisor481/NekoLib.Watchdog.RuntimeTests.Supervisor481/NekoLib.Watchdog.RuntimeTests.Supervisor481.csproj
```

## Procedure and expected result

1. Keep the default `notepad.exe` target or select another disposable target,
   then press Start. The dashboard must report a running Watchdog and child PID.
2. Invoke Ping and Status. Each RPC must return a successful response.
3. Close the supervised child. Watchdog must emit state/log events and start a
   replacement process.
4. Press Pause, close the child, and confirm it is not restarted. Press Resume
   and confirm supervision resumes.
5. Press Restart and confirm a new child PID appears.
6. Press Stop and close the dashboard. No supervised process or foreground
   monitor thread should remain owned by the scenario.

## Cleanup and side effects

The scenario starts and may terminate the selected executable and registers the
Watchdog's configured global hotkeys while running. Use only a disposable
target. Stop the runtime before closing; if a child remains, close it manually.
File logging is disabled by the scenario. `bin/` and `obj/` are disposable.

## Verification record

- 2026-08-01 / baseline `c473966` plus the Phase C promotion working tree:
  project built successfully on Windows for `net481`; interactive process/RPC
  behavior was not claimed.
