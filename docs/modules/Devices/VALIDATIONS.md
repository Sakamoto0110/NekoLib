# NekoLib.Devices Validation Evidence

**Document ID:** DEV-VALIDATION-EVIDENCE

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** executed validation evidence for the NekoLib.Devices boundary

**Surface:** validation-evidence

**Boundary:** devices

**Authority role:** evidence

**Mutation:** authored

**Indexing:** include

Evidence records what actually ran, against which source, environment, and
targets, with which result and gaps. It never defines behavior; the
[technical reference](REFERENCE.md) owns the contract and
[`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md) owns the evidence
contract.

Records are curated from preserved audit, scenario, and release evidence and are
listed in ascending date order. Every layer below is a separate claim: a build
result is not a runtime result, a loopback socket is not a serial port, and a
virtual COM pair is not physical hardware.

### Requirement coverage

Derived from the records below. `PARTIAL` and `NOT_RUN` are the honest state, not
a backlog: several are conditional and untriggered.

| Requirement | Status | Note |
|---|---|---|
| `DEV-VALREQ-001` build | PASS | Both targets at the current tree; 22 warning occurrences normalizing to 10 pre-existing identities, zero new, zero `CS1591`, both XML assets produced |
| `DEV-VALREQ-002` API compatibility | PASS | Both accepted manifests verified at the current tree without a baseline update |
| `DEV-VALREQ-003` focused regression | PASS | 50 tests per target, re-run at the current tree |
| `DEV-VALREQ-004` package consumer | PASS | Isolated `PackageReference` restores on both targets, including the XML asset |
| `DEV-VALREQ-005` framing fidelity | PASS | Loopback TCP and in-process named pipes, plus com0com text and binary framing |
| `DEV-VALREQ-006` operation boundary | PARTIAL | Both the default and the opt-in are pinned over **loopback TCP** only; the serial equivalent, including the `DiscardInBuffer` half of the remedy, is unverified |
| `DEV-VALREQ-007` cancellation | PASS | Focused suite on both targets; com0com adds a pending serial read under finite and infinite port timeouts on `net9.0` |
| `DEV-VALREQ-008` disconnect and reopen | PARTIAL | Peer disconnect, restart, and four-port release proven on `net9.0` only |
| `DEV-VALREQ-009` resource stability | NOT_RUN | No run drives an unread transport under sustained input; see [`DEV-FINDING-002`](FINDINGS.md) |
| `DEV-VALREQ-010` malformed input | PARTIAL | CRC-rejected malformed frame reached the caller verbatim on `net9.0` only |
| `DEV-VALREQ-011` virtual COM | PARTIAL | Both targets have real-COM parity from the oracle pass and the two-minute probes; the fault sweep is `net9.0` only, and no nominal window has run |
| `DEV-VALREQ-012` physical hardware | NOT_RUN | Untriggered: no physical UART, baud, framing, flow-control, cabling, or electrical claim is made anywhere in this boundary. No physical evidence exists |
| `DEV-VALREQ-013` disposal race | PARTIAL | The com0com lifecycle check covers a serial disposal race on `net9.0`; the focused suite covers disposed-member behaviour but never disposal during an in-flight read, and `net481` has no serial coverage |
| `DEV-VALREQ-014` encoding | PARTIAL | Binary round-trip and Latin-1-as-bytes proven, with `Write(string)` observably coerced to ASCII, on `net9.0` only. **No multi-byte encoding is exercised anywhere**, so nothing here supports a UTF-8 claim |
| `DEV-VALREQ-015` no implicit security surface | PASS | Both accepted manifests contain no credential, certificate, or ACL member |

## DEV-VALEVID-20260801-001

**Requirement IDs:** DEV-VALREQ-011

**Version:** pre-release

**Commit:** not recorded

**Tree state:** dirty — the NekoPcbEmulator serial changes were uncommitted in the separate emulator repository

**Environment:** Windows; com0com 3.0.0.0 virtual pairs `COM9 <-> COM19` and `COM10 <-> COM20`; NekoPcbEmulator holding `COM9`/`COM10`

**Targets:** `net481` and `net9.0`

**Command or scenario:** [`runtime_tests/Devices/Com0Com`](../../../runtime_tests/Devices/Com0Com/README.md) oracle pass, `--pcb-a COM19 --pcb-b COM20`

**Execution:** manual

**Evidence level:** interactive

**Result:** PASS

**Artifacts:** console `PASS` and exit code `0` on both targets; the run is recorded in the scenario's verification table

**Gaps:** This is the module's only **protocol-parity** evidence, because it is the only run where the far end was written by someone else. It proves nothing physical: com0com is a virtual pair with no baud, parity, framing, or line levels. The emulator side was uncommitted, so the result proves the tested working-tree combination rather than a reproducible pair of commits.

**Supersedes:** none

## DEV-VALEVID-20260811-001

**Requirement IDs:** DEV-VALREQ-005, DEV-VALREQ-007, DEV-VALREQ-008, DEV-VALREQ-011, DEV-VALREQ-013, DEV-VALREQ-014

**Version:** pre-release

**Commit:** not recorded

**Tree state:** not recorded

**Environment:** Windows; com0com virtual pairs with all four ports owned by the scenario; NekoPcbEmulator stopped

**Targets:** `net481` and `net9.0`

**Command or scenario:** com0com E3-DEV automated mode, corrected two-minute probes

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** exit `0` on both targets, 168/168 checks, 0 failed, 0 skipped, 11 cycles in 124 seconds each; all four ports reopened and released

**Gaps:** Development probes below the scenario's 15-minute smoke minimum — explicitly **not** smoke-gate evidence. Not protocol evidence: both ends were written in this repository, so their agreement proves framing was carried intact, not that either half is correct. No physical hardware.

**Supersedes:** none

## DEV-VALEVID-20260812-001

**Requirement IDs:** DEV-VALREQ-008, DEV-VALREQ-010, DEV-VALREQ-011, DEV-VALREQ-013

**Version:** pre-release

**Commit:** not recorded

**Tree state:** dirty — `repository.dirty: true` from an unrelated uncommitted E3-PIPE scenario fix; nothing under `src/Devices` or this scenario changed

**Environment:** Windows; com0com virtual pairs, all four ports verified free beforehand; NekoPcbEmulator confirmed stopped

**Targets:** `net9.0` only

**Command or scenario:** `--recovery-rehearsal --rehearsal-duration 10m --seed 20260808`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `artifacts/validation/phase-e/e3dev-recovery-net9.0-s20260808-20260812T140422862Z`; exit `0` in 467.2 s; 33/33 checks across transport 14, protocol 12, lifecycle 2, recovery 5; 99 operations, 75 successes, 20 expected failures, 4 cancellations, 0 unexpected failures; all four ports reopened and released

**Gaps:** `net9.0` only — `net481` has no fault-sweep evidence. Records `belowSpecifiedWindow: true`, so it is not a nominal rehearsal and no full smoke, rehearsal, soak, or campaign window has been run. Not protocol evidence. Handshake behaviour on the wire, and baud/parity/framing enforcement, are deliberately unasserted because a virtual pair does not implement them.

**Supersedes:** none

## DEV-VALEVID-20260818-001

**Requirement IDs:** DEV-VALREQ-001, DEV-VALREQ-002, DEV-VALREQ-003, DEV-VALREQ-005, DEV-VALREQ-006, DEV-VALREQ-007

**Version:** pre-release F1-DEV candidate

**Commit:** `63bb26976bc9253a744eb37d93ef4e9a5382aa3b`

**Tree state:** not recorded

**Environment:** Windows

**Targets:** `net481` and `net9.0`

**Command or scenario:** `dotnet build src/Devices/NekoLib.Devices/NekoLib.Devices.csproj -t:Rebuild`; `dotnet test tests/NekoLib.Devices.Tests/Unit/NekoLib.Devices.Tests.Unit.csproj`; `eng/verify-public-api.ps1 -PackageId NekoLib.Devices`; `eng/verify-docs.ps1`; `git diff --check`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** rebuild went from 40 to 22 warnings with zero errors and no new warning identity; 50 tests passed per target with zero failures and zero skips; the API diff was exactly the accepted delta, then updated and re-verified; documentation and diff checks passed

**Gaps:** No serial port was opened. The com0com scenario was **built only, not launched**, so it is build evidence for that scenario and nothing more. No package was produced and no `PackageReference` consumer probe was run for this record. The full solution was not rebuilt or tested. `DEV-VALREQ-013` was not satisfied: no test disposes a transport during an in-flight serial read.

**Supersedes:** none

## DEV-VALEVID-20260818-002

**Requirement IDs:** DEV-VALREQ-004

**Version:** `1.0.0-local.20`

**Commit:** `63785cc8bb801f1d4a90ade6cffb7f0b42c6bc1b` recorded in the package; the Devices implementation landed in `63bb26976bc9253a744eb37d93ef4e9a5382aa3b`

**Tree state:** clean — `eng/pack-local.ps1` requires it

**Environment:** Windows; local package feed

**Targets:** `net481` and `net9.0`

**Command or scenario:** coordinated `eng\pack-local.ps1` campaign with its `PackageReference`-only consumer, multitarget, package, deployment, publish, and clean probes

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** 1,538/1,538 solution tests passed; rebuild produced 464 warnings, zero errors, and no new warning identity; `NekoLib.Devices.1.0.0-local.20.nupkg` contains `lib/net481` and `lib/net9.0` assemblies and declares `Microsoft.Bcl.AsyncInterfaces 10.0.1` for .NET Framework 4.8.1 and `System.IO.Ports 9.0.0` for .NET 9; SHA-256 `659A076F85B11038A9C988E9ECE863BC814422166A9E4CD84768763DE0011CC6`

**Gaps:** Package and consumer-restore evidence only. No serial port was opened and com0com was not launched, so nothing here is serial, UART, or electrical evidence. This candidate predates packaged XML documentation.

**Supersedes:** none

## DEV-VALEVID-20260821-001

**Requirement IDs:** DEV-VALREQ-004

**Version:** `1.0.0`

**Commit:** `db63529cafce11690a18a595e4abc6c0610b9b8e`

**Tree state:** clean

**Environment:** Windows; local package feed

**Targets:** `net481` and `net9.0`

**Command or scenario:** `eng\pack-local.ps1 -PackageVersion 1.0.0`, without `-SkipTests` or `-AllowDirty`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `NekoLib.Devices.1.0.0.nupkg`, SHA-256 `52CEAEDE96933EF0C9D63F9117B68B659534ACB020445D9A19F072CFF93A17A3`; full provenance in [`docs/stable-release-1.0.0.md`](../../stable-release-1.0.0.md)

**Gaps:** Package provenance for the stable baseline. It claims no runtime, serial, hardware, or interactive coverage of its own, and predates packaged XML documentation.

**Supersedes:** DEV-VALEVID-20260818-002

## DEV-VALEVID-20260828-001

**Requirement IDs:** DEV-VALREQ-001, DEV-VALREQ-002, DEV-VALREQ-003, DEV-VALREQ-004

**Version:** `1.1.0-local.8`

**Commit:** `d6f2efdbe99f4a827293cdf4e8ed27c4096d134a`

**Tree state:** clean

**Environment:** Windows; local package feed and isolated NuGet global-packages cache

**Targets:** `net481` and `net9.0`

**Command or scenario:** `eng\pack-local.ps1 -PackageVersion 1.1.0-local.8`, then `eng\verify-docs.ps1`, `eng\verify-skills.ps1`, `eng\verify-public-api.ps1 -NoBuild`, and `git diff --check`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** Release solution build with 202 known warnings and zero errors; 1,787 solution tests passed with zero failures and zero skips; every managed package carried both target assemblies and both matching XML files; isolated `PackageReference` restores extracted the same pairs from the packages rather than from build output; all 30 accepted managed-library baselines verified without a baseline update. Devices-specific: zero `CS1591`, and 11 pre-existing nullable warning identities emitted once per target with no new identity.

**Gaps:** The family aggregate manifest hash `b56451a1ee8eb7ef4de0d32de143f9488b09f00b25fc300572bcbeb2ee34e9f2` covers all 31 published artifacts and is **not** a `NekoLib.Devices` package hash. This is a documentation, packaging, and consumer-delivery claim only: no interactive UI coverage, no soak, no external provider validation, no serial port, no public NuGet.org release, and no Git push.

**Supersedes:** none

## DEV-VALEVID-20260829-001

**Requirement IDs:** DEV-VALREQ-001, DEV-VALREQ-002, DEV-VALREQ-015

**Version:** unreleased working tree

**Commit:** `84970a2ec8db25bb23a8e397d4f1325b0089de8c`

**Tree state:** dirty — carries this module documentation review's own uncommitted changes, including four corrected public XML comments

**Environment:** Windows 11

**Targets:** `net481` and `net9.0`

**Command or scenario:** `dotnet build src\Devices\NekoLib.Devices\NekoLib.Devices.csproj -c Release -t:Rebuild`; `eng\verify-public-api.ps1 -PackageId NekoLib.Devices -NoBuild`; `eng\verify-docs.ps1 -BuildLogPath <rebuild log>`; `eng\verify-skills.ps1`; `git diff --check`

**Execution:** automated

**Evidence level:** build-only

**Result:** PASS

**Artifacts:** Release rebuild succeeded with 22 warning occurrences — 11 per target — and zero errors. Those normalize to **10 unique warning identities, every one already present in `eng/warning-baseline.txt`, and zero new identities**. All are pre-existing nullable diagnostics: eight `CS8618` public-field identities in `ControllerModel.cs`, plus `CS8600` and `CS8625` in `ProtocolRaw.cs`. The rebuild emitted **zero `CS1591` and zero XML-comment diagnostics** in the `CS1570`–`CS1591` range; both `NekoLib.Devices.xml` files were produced beside their assemblies and are byte-identical, and every `see cref` in the corrected comments resolved. Both accepted API manifests verified with no baseline update, confirming the comment corrections changed no compiled metadata and that neither manifest carries a credential, certificate, or ACL member. Documentation, skill-registry, and diff-hygiene checks passed.

**Gaps:** Build and static-verification layer only. Nothing here executes the module: no test run, no package, no serial port, no com0com launch, and no physical hardware. The verifier noted 165 baseline identities not emitted, which is expected because only the Devices project was rebuilt. Of the 17 Devices identities the baseline carries, **seven are stale** — they name `HardwareProtocol.Template`, the non-nullable `Log` properties, and other sites that F1-DEV removed or annotated, and current source no longer produces them. The baseline predates that work; refreshing it is an intentional maintenance operation and was deliberately not performed here.

**Supersedes:** none

## DEV-VALEVID-20260829-002

**Requirement IDs:** DEV-VALREQ-003, DEV-VALREQ-005, DEV-VALREQ-006, DEV-VALREQ-007

**Version:** unreleased working tree

**Commit:** `84970a2ec8db25bb23a8e397d4f1325b0089de8c`

**Tree state:** dirty — as above

**Environment:** Windows 11; real loopback `TcpListener` and in-process named pipes

**Targets:** `net481` and `net9.0`

**Command or scenario:** `dotnet test tests\NekoLib.Devices.Tests\Unit\NekoLib.Devices.Tests.Unit.csproj`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** 50 passed, 0 failed, 0 skipped on `net9.0`; 50 passed, 0 failed, 0 skipped on `net481`. This includes the two operation-boundary regressions that drive a real loopback peer answering 600 ms after a 200 ms budget, asserting both the unchanged default and the opt-in remedy.

**Gaps:** The suite is in-process fakes plus loopback sockets and pipes. **No serial port was opened**, so it is not serial, UART, or electrical evidence, and it does not exercise the `DiscardInBuffer` half of the `CloseTransportOnNoResponse` remedy. `DEV-VALREQ-013` remains unsatisfied by it: no test disposes a transport during an in-flight read. No package was produced and no `PackageReference` consumer probe was run for this record.

**Supersedes:** none
