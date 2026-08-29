# NekoLib.Watchdog.Host Findings

**Document ID:** WDGHOST-FINDINGS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** unconfirmed and non-normative observations about NekoLib.Watchdog.Host

**Surface:** findings

**Boundary:** watchdog.host

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

Everything here is non-normative. A finding becomes a confirmed defect only
after verification, and becomes scheduled work only through explicit promotion
to [`TODO.md`](../../../TODO.md).

The historical leads in [`audits/`](audits/) remain true only for their recorded
baseline. Those reverified against current source during the 2026-08-29 module
review are summarized here; the original stays unmodified at its recorded
baseline.

| Historical ID | Origin | Current source state |
|---|---|---|
| WDHOST-01 (unversioned protocol) | [`audits/contract-review-2026-08-20.md`](audits/contract-review-2026-08-20.md) | Superseded. The parser requires `--protocol-version`, rejects an unsupported value with `NotSupportedException`, and the runtime answers `protocol_version` with `1`; bootstrap reports a version mismatch instead of a timeout. |
| WDHOST-02 (transitive deployment) | [`audits/contract-review-2026-08-20.md`](audits/contract-review-2026-08-20.md) | Superseded. The project packs only `build/NekoLib.Watchdog.Host.targets`, the import guard is gone, and a package probe asserts that a wrapper-only consumer receives no sidecar. |
| WDHOST-03 (relative unbounded fatal log) | [`audits/contract-review-2026-08-20.md`](audits/contract-review-2026-08-20.md) | Superseded in placement and bounding. The log is per-user, UTC, process-tagged, and 256 KiB-bounded with one backup. One rotation branch survives as `WDGHOST-FINDING-003`. |
| WDHOST-04 (unvalidated working directory) | [`audits/contract-review-2026-08-20.md`](audits/contract-review-2026-08-20.md) | Superseded. An explicit `--workdir` must exist and be a directory; a file is rejected separately from a missing directory, and both are covered by focused tests. |
| WDHOST-05 (no current contract owner) | [`audits/contract-review-2026-08-20.md`](audits/contract-review-2026-08-20.md) | Superseded, and re-homed. The dedicated reference the review asked for now lives at [`REFERENCE.md`](REFERENCE.md) as this boundary's normative contract. No empty compiled-API manifest was created, and the [manifest](MANIFEST.md) records that absence as deliberate. |
| WDHOST-06 (incomplete release evidence) | [`audits/contract-review-2026-08-20.md`](audits/contract-review-2026-08-20.md) | Superseded. The package campaign now covers exact layout, direct ownership, byte identity, architecture, selection, unsupported RIDs, lifecycle, protocol mismatch, and real package-backed startup and shutdown. |
| First-pass L5 (relative Host fatal log) | [`../Watchdog/audits/initial-audit.md`](../Watchdog/audits/initial-audit.md) | Superseded by WDHOST-03 above. |

The observations raised by the same review are recorded as findings below.

## WDGHOST-FINDING-001

**Status:** confirmed

**Confidence:** high

**Observation:** Payload selection maps the consumer's framework *identifier* to a payload without ever checking its *version*. Any `.NETFramework` consumer receives the `net481` payload and any `.NETCoreApp` consumer receives the `net9.0-windows` payload, so a `net472` or `net8.0-windows` application builds and deploys successfully and then fails when the Host is launched, not when it is built.

**Evidence:** `src/Watchdog/NekoLib.Watchdog.Host/NekoLib.Watchdog.Host.Package.targets` sets `_NekoLibWatchdogHostTfm` from `'$(TargetFrameworkIdentifier)' == '.NETFramework'` and `'$(TargetFrameworkIdentifier)' == '.NETCoreApp'` with no version comparison. The validation target's only framework error fires when `_NekoLibWatchdogHostTfm` is empty, and its message — "supports .NET Framework 4.8.1 and .NET 9 Windows consumers only" — describes a constraint the condition does not enforce. Every package consumer probe targets `net481` or `net9.0-windows`, so no probe exercises a mismatched version.

**Hypothesis:** The practical exposure is a late, confusing failure rather than a wrong payload: a `net472` application receives a 4.8.1-targeted managed executable, and a `net8.0-windows` application receives an apphost that demands the .NET 9 runtime. Both surface as a Host that exits immediately, which bootstrap reports as an early exit pointing at the fatal log. A build-time rejection would be far cheaper to diagnose.

**Disposition:** Record only. Adding a version comparison changes which consumers can build and is a deployment-contract change requiring an accepted decision. `WDGHOST-VALREQ-010` states the evidence that a support claim for other framework versions would need.

**Outcome link:** [`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md)

## WDGHOST-FINDING-002

**Status:** confirmed

**Confidence:** high

**Observation:** The two unsupported-target rejections honor `NekoLibWatchdogHostRid` differently. An unrecognized `RuntimeIdentifier` is only rejected while `NekoLibWatchdogHostRid` is unset, so setting it explicitly suppresses that rejection; the ARM64 platform rejection fires regardless. A modern consumer publishing with `RuntimeIdentifier=linux-x64` and `NekoLibWatchdogHostRid=win-x64` therefore builds successfully and receives a Windows x64 apphost.

**Evidence:** In `NekoLib.Watchdog.Host.Package.targets`, `_NekoLibWatchdogHostUnsupportedRid` takes the `RuntimeIdentifier` only under `'$(NekoLibWatchdogHostRid)' == ''`, while the ARM64 assignment is conditioned only on the value still being empty. The probe campaign exercises `NekoLibWatchdogHostRid=win-arm64`, which is caught by the separate resolved-RID error, but no probe combines an explicit supported RID with an unsupported `RuntimeIdentifier`.

**Hypothesis:** The override was almost certainly meant as an escape hatch for a RID the pattern match does not recognize but whose payload is still correct — for example a versioned `win10-x64`. Read that way the behavior is intentional; read as a guard it has a hole. The two rejection paths disagreeing about whether the property is an override is the part that looks unintended.

**Disposition:** Record only, and documented in [`REFERENCE.md`](REFERENCE.md) so the asymmetry is at least stated. Changing either condition changes which builds succeed and requires an accepted decision.

**Outcome link:** [`REFERENCE.md`](REFERENCE.md)

## WDGHOST-FINDING-003

**Status:** confirmed

**Confidence:** medium

**Observation:** One fatal-log rotation branch destroys all retained evidence. Rotation deletes any existing `.1` backup first, and only then decides what to do with the active file; when the active file is already larger than the bound it is deleted outright rather than rotated. That call therefore removes both the backup and the active file and leaves only the entry about to be written.

**Evidence:** `src/Watchdog/NekoLib.Watchdog.Host/HostFatalLog.cs` `RotateIfRequired` deletes `path + ".1"` before the `currentBytes <= MaxBytes` test, moves the active file to the backup on the true branch, and calls `File.Delete(path)` on the false branch. `HostFatalLogTests` covers the ordinary rotation and the oversized-entry bound but not an already-oversized active file, so the branch is untested as well as lossy.

**Hypothesis:** Entry bounding makes the branch hard to reach from this code alone, because a single entry cannot exceed the file bound. It becomes reachable when something else grew the file — a different Host version, a hand-edited file, or a copy from another machine. The behavior is defensible as "prefer a bounded file over unbounded evidence", but deleting the backup before knowing which branch will run is what makes the loss total rather than partial.

**Disposition:** Record only, and documented in [`REFERENCE.md`](REFERENCE.md) so operators know the log can reset itself. Reordering the delete would change fail-soft evidence behavior and needs an accepted decision plus a regression for the branch.

**Outcome link:** [`REFERENCE.md`](REFERENCE.md)

## WDGHOST-FINDING-004

**Status:** confirmed

**Confidence:** medium

**Observation:** The documented exit-code contract has no direct automated assertion. `Program.Main` is the only place that maps an orderly runtime exit to `0` and any exception to `1`, and no test invokes it. The focused suite reaches the parser and the fatal log directly, and the package probes observe process behavior — startup, ping, status, stop — without asserting either exit code.

**Evidence:** `src/Watchdog/NekoLib.Watchdog.Host/Program.cs` contains the whole contract in one `try`/`catch`. `tests/NekoLib.Watchdog.Tests/Unit/` contains `HostArgumentParserTests` and `HostFatalLogTests` but no test that runs `Main`. `tests/NekoLib.PackageConsumers/WatchdogHostProtocol/Program.cs` drives the packaged Host over its control pipe and checks its own exit codes, not the Host's.

**Hypothesis:** The mapping is three lines and unlikely to be wrong today, so this is an evidence gap rather than a suspected defect. It matters because the exit code is the only signal an installer, service wrapper, or scheduled task would have, and it is the documented interface for exactly those consumers.

**Disposition:** Record only. Closing it is a test change, which this documentation review is not authorized to make. Recorded as the evidence gap under `WDGHOST-VALREQ-009`.

**Outcome link:** [`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md)
