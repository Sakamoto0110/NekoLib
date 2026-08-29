# NekoLib.Watchdog Findings

**Document ID:** WDG-FINDINGS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** unconfirmed and non-normative observations about NekoLib.Watchdog

**Surface:** findings

**Boundary:** watchdog

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

Everything here is non-normative. A finding becomes a confirmed defect only
after verification, and becomes scheduled work only through explicit promotion
to [`TODO.md`](../../../TODO.md).

The historical leads in [`audits/`](audits/) remain true only for their recorded
baselines. Those reverified against current source during the 2026-08-29 module
review are summarized here; their originals stay unmodified at their recorded
baselines.

| Historical ID | Origin | Current source state |
|---|---|---|
| First-pass H1–H3, M1–M4, M6, M7, M10, L1, L2, L4, L6, L7 | [`audits/initial-audit.md`](audits/initial-audit.md) | Superseded. Every remediation is present in current source: no dead Win32 duplicate, `ForceKillTimeoutMs` honored by the forced path, `MaxLogBytes` rotation with a single backup, crash finalization wired into the monitor loop, `EnableHotkeys` with logged registration failure, centralized and pinned command names, logged spawn and kill failures, buffered application-log forwarding without a feedback loop, and duplicate-start window activation. |
| First-pass M5 (update mechanism) | [`audits/initial-audit.md`](audits/initial-audit.md) | Superseded by decision, not by implementation. The four public update options were removed in F1-WDOG and the internal wire command answers `not_implemented`. There is no update behavior and none is scheduled. |
| First-pass M8 (truncated pipe-name hash) | [`audits/initial-audit.md`](audits/initial-audit.md) | Still current and deliberately retained. Carried forward as `WDG-FINDING-004`. |
| First-pass M9 (silent replay-buffer eviction) | [`audits/initial-audit.md`](audits/initial-audit.md) | Superseded. Eviction increments a dedicated cumulative counter exposed as `historyEvictions`, pinned by `LogHistoryEviction_IncrementsDedicatedCounter`. |
| First-pass L3 (fragile double disposal) | [`audits/initial-audit.md`](audits/initial-audit.md) | Superseded. `Dispose` is exactly `Stop`, all transitions are serialized by one lifecycle lock, and `ConcurrentStopAndDispose_JoinOneTerminalCleanup` pins the behavior. |
| First-pass L5 (relative Host fatal log) | [`audits/initial-audit.md`](audits/initial-audit.md) | Outside this boundary. Closed as WDHOST-03 in the Watchdog Host deployment boundary. |
| WDOG-01 through WDOG-12 | [`audits/public-api-review-2026-08-18.md`](audits/public-api-review-2026-08-18.md) | Superseded. All eight accepted dispositions are present in current source and in both accepted API baselines; the reference documents the resulting contract. |
| IPC-01, IPC-03 | [`../Pipes/audits/ipc-hardening-review-2026-08-08.md`](../Pipes/audits/ipc-hardening-review-2026-08-08.md) | Still current. The Pipes boundary explicitly routed these here as consumer-protocol concerns. Carried forward as `WDG-FINDING-005`. |

The observations that survived that reverification, plus new ones raised by the
same review, are recorded as findings below.

## WDG-FINDING-001

**Status:** open

**Confidence:** medium

**Observation:** The per-target single-instance guard is created in the Windows global kernel-object namespace. Windows documents creation of a `Global\` object as requiring `SeCreateGlobalPrivilege`, which a standard interactive user token does not carry by default. If that privilege is absent, the constructor throws and `Start` fails, so supervision cannot begin at all.

**Evidence:** `src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs:153` builds `Global\NekoLib.Watchdog::<pipe-name>` and `WatchdogRuntime.cs:154` constructs the `Semaphore` inside the `Start` try block, whose catch performs terminal cleanup and rethrows. `WatchdogAttachTests.Start_SecondRuntimeForSameTarget_RespectsSingleton` exercises the guard, but only on the developer account that ran it. Neither the focused suite nor the deployed-Host scenario has been executed under a non-privileged, non-elevated, or cross-session account.

**Hypothesis:** The product class named in [`ROADMAP.md`](../../../ROADMAP.md) — unattended PDV and DM terminals — is exactly where an application is likely to run as a restricted interactive user. If the privilege is genuinely required, those deployments would fail at `Start` rather than degrade. The counter-hypothesis is that the interactive token on the supported Windows configurations does carry it, in which case there is nothing to fix and the current evidence simply never demonstrated the boundary.

**Disposition:** Record only. Switching to the session-local namespace would change the documented cross-session duplicate guard and requires an accepted decision. Reproduce it by starting a runtime under a restricted standard-user token before proposing any change; the outcome also determines whether `WDG-VALREQ-011` can be satisfied or must be reclassified.

**Outcome link:** [`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md)

## WDG-FINDING-002

**Status:** confirmed

**Confidence:** high

**Observation:** `NoWarn 1591` is declared for the `net481` target only. Because documentation generation is enabled for every managed library, a public member added without an XML comment would produce `CS1591` on `net9.0-windows` and be silent on `net481`. The documentation gate is therefore enforced by exactly one of the two target compilations.

**Evidence:** `src/Watchdog/NekoLib.Watchdog/NekoLib.Watchdog.csproj` sets `NoWarn 1591` inside the `net481` property group only; `Directory.Build.props` sets `GenerateDocumentationFile` for every `src/NekoLib.*` project except the Host. The same asymmetry exists in `NekoLib.Core` and `NekoLib.Diagnostics.Windows`, so it is a family-wide leftover rather than a Watchdog decision. It is currently inert: the completed documentation gate reports zero `CS1591` for this boundary.

**Hypothesis:** The suppression predates `NEKOMKT-F009`, when it was genuinely inert because no XML was generated. It now silently narrows the gate. The practical exposure is limited, because a target-neutral member is still caught by the modern compilation; a member declared only under `NETFRAMEWORK` would not be.

**Disposition:** Record only. Removing the suppression is a project-file change across at least three projects and belongs to a family-wide decision rather than a documentation review. `WDG-VALREQ-001` states the acceptance criterion in terms of both targets so the gap is visible in the evidence contract.

**Outcome link:** [`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md)

## WDG-FINDING-003

**Status:** confirmed

**Confidence:** high

**Observation:** The wire-name regression pins four of the seven internal command constants. `log_write`, `log_write_batch`, and `update` have no pinned literal, so renaming one of those constants would compile, would keep the public six intact, and would silently break application log forwarding or the coordinated Host protocol answer.

**Evidence:** `tests/NekoLib.Watchdog.Tests/Unit/WatchdogCommandsTests.cs` pins `ping`, `status`, `pause`, `resume`, `restart`, and `stop` publicly and `log_history`, `exception_notify`, `protocol_version`, and `attach_status` internally. `src/Watchdog/NekoLib.Watchdog/WatchdogCommands.cs` declares seven internal constants. The forwarding tests exercise the batch path end to end through the runtime, so a rename would be caught there, but no test asserts the literal itself; `update` has no coverage at all beyond its handler registration.

**Hypothesis:** The gap is small for `log_write` and `log_write_batch`, which the forwarding regressions would fail on. `update` is the real exposure: its only purpose is to answer a coordinated Host deterministically, and nothing asserts either its name or its `not_implemented` code.

**Disposition:** Record only. Extending the existing theory is a test change, which this documentation review is not authorized to make. Recorded as an evidence gap under `WDG-VALREQ-004`.

**Outcome link:** [`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md)

## WDG-FINDING-004

**Status:** confirmed

**Confidence:** high

**Observation:** The control-pipe identity is the lowercase full target path hashed with SHA-1 and truncated to 16 hexadecimal characters. Two distinct target paths can in principle collide onto one pipe name and one single-instance semaphore.

**Evidence:** `src/Watchdog/NekoLib.Watchdog/WatchdogController.cs` computes the SHA-1 of the lowercase full path and takes the first 16 hex characters. `WatchdogOptionsTests.Capture_DifferentTargets_ProduceDifferentPipeNames` asserts distinctness for two specific paths, which demonstrates the derivation rather than the absence of collisions. Raised as M8 in [`audits/initial-audit.md`](audits/initial-audit.md) and reverified as still current.

**Hypothesis:** For the supported small set of local target executables the collision risk is remote, and 64 bits of a cryptographic digest is generous for that population. Lengthening the hash would change controller, runtime, Host, and scenario identity simultaneously with no security benefit, because the name is not a secret.

**Disposition:** Accepted and documented in [`REFERENCE.md`](REFERENCE.md). The F1-WDOG review explicitly rejected treating the hash as a security boundary or lengthening it. Reopening requires an observed collision or an accepted identity redesign.

**Outcome link:** [`REFERENCE.md`](REFERENCE.md)

## WDG-FINDING-005

**Status:** confirmed

**Confidence:** high

**Observation:** Watchdog control is dispatched by command name with no caller authorization, and the endpoint name is deterministic. Any same-user process that can open the control pipe can pause supervision, restart or stop the target, inject crash and log records, and read status and replay history; a same-user process can also compute and squat the endpoint name before the runtime binds it.

**Evidence:** `src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs` maps every handler by name and applies `pause`, `resume`, and `restart` before replying; the `stop` handler queues terminal shutdown. The endpoint uses `PipeAccessPolicy.CurrentUserOnly`, which the [Pipes reference](../Pipes/REFERENCE.md) documents as no defense against a hostile same-user process. Originally raised as IPC-01 and IPC-03 in [`../Pipes/audits/ipc-hardening-review-2026-08-08.md`](../Pipes/audits/ipc-hardening-review-2026-08-08.md) and routed to this boundary by [`../Pipes/FINDINGS.md`](../Pipes/FINDINGS.md).

**Hypothesis:** Under the accepted cooperative same-user trust model this is a stated boundary rather than a defect: a process running as that user could terminate the target directly without involving Watchdog at all. It becomes a defect only if the threat model admits a hostile same-user process, which would also require revisiting the attach token, the deterministic name, and the pipe policy together.

**Disposition:** Accepted and stated in the [security boundary](REFERENCE.md) section. The F1-WDOG review explicitly rejected adding hostile-same-user authentication, replay protection, credentials, or remote administration. `WDG-VALREQ-010` records the corresponding evidence as `NOT_APPLICABLE` by decision rather than by omission.

**Outcome link:** [`REFERENCE.md`](REFERENCE.md)

## WDG-FINDING-006

**Status:** confirmed

**Confidence:** low

**Observation:** `WatchdogController` resolves the current executable's pipe name in a static field initializer that throws `InvalidOperationException` when the main module path is unavailable. A failure there surfaces as a cached `TypeInitializationException`, and because the class carries no explicit static constructor the runtime may initialize it on an access to any member — including `ResolvePipeNameForTarget`, which does not need the resolved value.

**Evidence:** `src/Watchdog/NekoLib.Watchdog/WatchdogController.cs` declares `private static readonly string _pipeName = ResolvePipeName();`, and `ResolvePipeName` throws when `Process.GetCurrentProcess().MainModule?.FileName` is null. `WatchdogRuntimeOptions.Capture` calls `WatchdogController.ResolvePipeNameForTarget`, so an advanced supervisor that never controls its own executable still routes through this type.

**Hypothesis:** Resolving the current process's own main module is reliable in the supported configurations, so this is a latent robustness question rather than an observed failure. Its practical effect would be to make an unrelated, target-parameterized helper unusable in a process where current-executable identity cannot be resolved.

**Disposition:** Record only. No failure has been observed and no test covers it. Making the resolution lazy would change initialization timing and belongs to an accepted API or behavior decision, not to a documentation review.

**Outcome link:** none
