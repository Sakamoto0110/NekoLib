# NekoLib.Watchdog History

**Document ID:** WDG-HISTORY

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** factual chronological history of the NekoLib.Watchdog boundary

**Surface:** history

**Boundary:** watchdog

**Authority role:** evidence

**Mutation:** append-only

**Indexing:** include

Entries are appended in ascending date order and are not rewritten to match
later architecture. Each entry links preserved evidence rather than restating
it.

## 2026-06-04 — WDG-HISTORY-001 — First-pass audit and remediation

**Release:** none

- The first Watchdog review established the supervisor model, the client facade,
  the RPC command surface, and the crash-bundle flow, and it produced a
  remediation log that closed most of its own findings in the same day: the dead
  Win32 duplicate was removed, `ForceKillTimeoutMs` was wired into the kill
  sequence, `MaxLogBytes` gained single-backup rotation, the crash bundler was
  wired into the monitor loop, hotkeys became configurable, RPC command names
  were centralized and pinned by a test, and spawn and kill failures started
  logging instead of terminating the monitor thread.
- Four items were left open at that baseline: update orchestration, the
  truncated pipe-name hash, silent replay-buffer eviction, and the Host's
  relative fatal-log path.

**Evidence:** [`audits/initial-audit.md`](audits/initial-audit.md)

## 2026-07-27 — WDG-HISTORY-002 — Self-bootstrap and initial-process attach

**Release:** none

- An application gained the ability to start the deployed Host itself, hand off
  its own PID with a one-time correlation token, and let the existing restart
  path supervise later generations. Bootstrap treats its handshake timeout as
  one total budget across preflight, launch, and cancellable handshake I/O,
  serializes concurrent in-process calls, and terminates an unconfirmed Host it
  started.
- Existing-host detection was tightened to require an `attach_status` identity
  for the current PID rather than accepting a bare `ping`.

**Evidence:** [`audits/initial-audit.md`](audits/initial-audit.md)

## 2026-08-08 — WDG-HISTORY-003 — IPC hardening review recorded the control-authorization boundary

**Release:** none

- A code-first review of the transport and the Watchdog RPC/event boundary
  recorded that Watchdog control is dispatched by command name with no caller
  authorization, and that the deterministic pipe name permits same-user endpoint
  squatting. Both were classified as consumer-protocol concerns of the
  cooperative same-user model rather than generic transport defects.
- The accepted disposition gave Pipes an explicit `CurrentUserOnly` access
  policy on both targets, which Watchdog selects for its RPC and event
  endpoints.

**Evidence:** [`../Pipes/audits/ipc-hardening-review-2026-08-08.md`](../Pipes/audits/ipc-hardening-review-2026-08-08.md), [`../../history/phase-e-confidence-stabilization-2026-08-12.md`](../../history/phase-e-confidence-stabilization-2026-08-12.md)

## 2026-08-11 — WDG-HISTORY-004 — Deployed-Host crash and recovery gate, and the attach exit-code fix

**Release:** none

- The unattended deployed-sidecar scenario found a first-generation-only defect:
  an initially attached application that exited normally was replaced correctly,
  but public status reported a null `lastExitCode` because the process obtained
  by ID could no longer reopen its native handle after exit. The attach path now
  materializes the handle while the process is alive, and a focused regression
  discards the launcher handle, exits the attached process with a known code,
  and asserts public status after restart.
- The scenario then closed its outcome-first gate: dual-target source-layout
  probes passed 20/20 with all six faults and complete cleanup, and one
  package-backed `net9.0-windows` run against immutable `1.0.0-local.10` matched
  the deployed Host bytes to an exact package payload entry. The compact run
  records itself as below the nominal smoke window.

**Evidence:** [`../../../runtime_tests/Watchdog/CrashRecovery/README.md`](../../../runtime_tests/Watchdog/CrashRecovery/README.md)

## 2026-08-18 — WDG-HISTORY-005 — F1-WDOG public API finalized

**Release:** none

- The compiled-surface review produced twelve findings and eight accepted
  dispositions, all implemented: construction-time immutable option capture that
  no longer mutates the caller, one terminal race-safe start/wait/stop/dispose
  lifecycle, shutdown that interrupts the crash-loop cooldown and drains owned
  workers, an explicitly resolved system `taskkill.exe`, observable
  acknowledgements from the four mutating controller operations, nullable
  serializer-neutral `LogEvent.MetaJson`, `EnableHotkeys`, and separated
  cumulative loss counters.
- The candidate surface was reduced: `WatchdogOptions.PipeName`, public
  `Normalize`, the four update placeholders, the obsolete raw log server, and
  five implementation helpers left the public API, and `Stop(bool)` became
  `Stop()`. Both accepted API baselines were updated as part of that acceptance.

**Evidence:** [`audits/public-api-review-2026-08-18.md`](audits/public-api-review-2026-08-18.md), [`migrations/f1.md`](migrations/f1.md), [`../../history/phase-f1-public-api-release-stability-2026-08-21.md`](../../history/phase-f1-public-api-release-stability-2026-08-21.md)

## 2026-08-20 — WDG-HISTORY-006 — Protocol v1 reached the library bootstrap

**Release:** none

- The Host contract review established internal protocol v1. The library side of
  that decision landed here: bootstrap emits the required launch version, checks
  `protocol_version` before accepting an attachment, distinguishes an
  incompatible Host from a timeout, and expects the versioned
  `attached:v1:<pid>:<token>` identity. The runtime answers `protocol_version`
  with `1`.
- The library's internal `update` command kept its explicit `not_implemented`
  response so a coordinated pair answers deterministically.

**Evidence:** [`../WatchdogHost/audits/contract-review-2026-08-20.md`](../WatchdogHost/audits/contract-review-2026-08-20.md)

## 2026-08-21 — WDG-HISTORY-007 — First stable family baseline

**Release:** 1.0.0

- `NekoLib.Watchdog` 1.0.0 entered the first stable coordinated family baseline.
  Its two accepted compiled API manifests became stable baselines under the
  public API and release policy, and the package was published with the family.

**Evidence:** [`../../stable-release-1.0.0.md`](../../stable-release-1.0.0.md)

## 2026-08-27 — WDG-HISTORY-008 — Managed XML documentation completed

**Release:** none

- Watchdog was part of the Tail family documentation gate. Its planning-baseline
  `CS1591` diagnostics went to zero with no malformed or unresolved XML-comment
  warning, existing comments were judged against current source rather than
  accepted from presence, and both accepted manifests verified without a
  baseline update.
- The focused Watchdog suite passed 106 tests per target. One modern-target
  process-marker failure appeared while all suites ran concurrently and did not
  reproduce when rerun sequentially; no Watchdog change was made for it.

**Evidence:** [`../../audit/public-api-documentation-extensibility-review-2026-08-27.md`](../../audit/public-api-documentation-extensibility-review-2026-08-27.md)

## 2026-08-28 — WDG-HISTORY-009 — XML documentation delivered in packages

**Release:** none

- The coordinated `NEKOMKT-F009` package gate closed. Candidate `1.1.0-local.8`
  carried both `NekoLib.Watchdog` target assemblies with their matching XML
  files, a permanent package-content guard was added to the canonical pack flow,
  and isolated `PackageReference` consumers received the same pairs from the
  package rather than from repository build output.

**Evidence:** [`../../audit/public-api-documentation-extensibility-review-2026-08-27.md`](../../audit/public-api-documentation-extensibility-review-2026-08-27.md)

## 2026-08-29 — WDG-HISTORY-010 — Module-first documentation migration

**Release:** none

- The boundary moved to `docs/modules/Watchdog/`. The source-adjacent reference
  became the canonical `REFERENCE.md` with a pointer-only portal left in the
  source tree, and the F1 migration guide and both audits moved under the module
  with their bodies, baselines, and original paths preserved.
- The full review reconciled the reference against current source and recorded
  the derived evidence contract, the curated executed evidence, and four
  non-normative findings. Six public XML comments were corrected where they
  under-described current behavior: the conditional `LogPath` default, the
  non-positive `HeartbeatIntervalMs` disable and its poll-boundary granularity,
  the skipped `WatchdogPipeLogSink` element in `LogSinks`, the `.events` sibling
  of `WatchdogRuntime.PipeName`, the info-severity and dropped-exception
  behavior of forwarded `NotifyLog` entries, and the incompatible-protocol and
  early-exit failures of `EnsureStarted`. No behavior, signature, baseline,
  target, or dependency changed.

**Evidence:** [`MANIFEST.md`](MANIFEST.md), [`REFERENCE.md`](REFERENCE.md), [`FINDINGS.md`](FINDINGS.md), [`VALIDATIONS.md`](VALIDATIONS.md)
