# Scenario Name

**Kind:** guide

**Lifecycle:** current

**Owner:** module or capability

**OS / target:** required OS and target framework

**Prerequisites:** tools, hardware, services, privileges, and local data

**Last verification:** date, commit, and whether it was build-only or interactive

## Purpose

State the behavior and boundary this executable validates.

## Build

```powershell
dotnet build <project-path>
```

## Launch

Name the exact executable or `dotnet run --project <project-path>` command.

## Procedure and expected result

1. Describe one observable action and its expected result.

## Cleanup and side effects

List processes, files, ports, devices, credentials, or state the scenario may
create or modify, and how to restore the environment.

## Verification record

Record only runs that were actually performed. Build compatibility and manual
behavior are separate results.
