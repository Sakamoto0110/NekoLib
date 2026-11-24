# NekoLib 1.4.0 public qualification completion

**Kind:** reference

**Lifecycle:** current

**Subject:** completed signed-package recovery matrix after the owner-authorized continuation

**Date:** 2026-11-24

**Package source:** `86340ab27ade1322225a3ae2005b6a32c59ab005` (`v1.4.0`)

**Qualification fixture:** `a5ee0b2f03b944f9ae1a13b785d57360384d105b`

The public matrix is complete: **168 distinct passed cases**, comprising six
retained WinForms net481 recovery cases and **162 newly passed cases**. The
earlier public run also completed all four native module consumers. The owner
authorized continuation after reboot with qualification windows at `(2700, 100)`.
The fixture positions its own native windows and records/asserts their rectangles;
no computer-use tool, pointer movement or keyboard automation was used.

| UI / target | Same executable | Optional sidecar | Entry opt-out | Legacy recovery | Total |
|---|---|---|---|---|---|
| WinForms net481 | 22/22 | 13/13 | 1/1 | 6/6 retained | 42/42 |
| WinForms net9.0-windows | 22/22 | 13/13 | 1/1 | 6/6 | 42/42 |
| WPF net481 | 22/22 | 13/13 | 1/1 | 6/6 | 42/42 |
| WPF net9.0-windows | 22/22 | 13/13 | 1/1 | 6/6 | 42/42 |

The UI portion covers all 56 combinations of two UIs, two frameworks, two
hosting modes and seven conditions. Sixteen sustained-hang cases recover with
a different PID and responsive replacement UI. Forty negative cases cover
separated stalls, startup grace, modal loops, pause/resume and detection disable
without replacing the original process. Retained controls, fatal/recoverable
exceptions, argument preservation, one primary window, supervisor continuity
and owned process/endpoint cleanup also pass. WDG-VALREQ-015 now has its public
native repeat at this exact version and observed scope.

The continuation uses a new private feed/output/intermediate/cache directory.
All 16 restored main packages match the previously downloaded, independently
signature-verified public payloads by SHA-256; source metadata confirms the
private feed. No package was rebuilt or republished for placement changes.
The six historical records remain byte-for-byte unchanged. The fixture changes
only qualification placement, resumption, evidence paths and exception diagnostics.
The first 39 continuation passes use fixture
`e5274c4fd0c3c5f9d73d835c094695f99233e9be`, the next 51 use `cace7ca`, and the
remaining 72 WPF cases use the final fixture above. Passed identities are retained
across these qualification-only corrections.
The first continuation stopped because a Framework evidence file path reached
261 characters. A diagnostic retry captured DirectoryNotFoundException at that
write. Compact GUID-only scenario directories corrected the harness path issue,
and WinForms fixture exceptions now go to stderr instead of an unattended native
exception dialog. Both failed attempts are retained. A subsequent WPF compilation
exposed the placement helper's project-name condition in generated XAML temporary
projects. Its shared compile item was made explicit, as with the pre-existing
shared probe; that failed build is also retained. No Watchdog package code,
timeout, behavioral assertion or previously passed case was changed for these fixes.

Initial public session: `a8e17a048cd64544a9342ec27e9a7d5a`.
Continuation session: `public-resume-c44f993be59a4c6c837ebd840f464439`.
Native rectangle receipts in this continuation: **212**.
The driver reported **0** surviving owned processes
to clean up at completion. Per-case checks also require process and endpoint cleanup.

The continuation command was `pwsh -NoProfile -File
artifacts/watchdog-ui-hang/resume-public-matrix.ps1`, followed by `-Resume`
after the recorded harness correction, from the qualification
worktree. That exact script, all case results, bounded per-case files, placement
receipts, restored-package identities and completion summary are preserved in
the [public completion archive](https://github.com/Sakamoto0110/NekoLib/releases/download/v1.4.0/watchdog-ui-hang-public-qualification-1.4.0.zip).

The [initial evidence archive](https://github.com/Sakamoto0110/NekoLib/releases/download/v1.4.0/watchdog-ui-hang-evidence-1.4.0.zip)
and initially attached `stable-release-1.4.0.md` remain unchanged historical
snapshots of the paused public phase. This supplement completes their public
execution scope; the [current release record](stable-release-1.4.0.md) incorporates
the continuation. The source/candidate/stable gates, package bytes and
[trusted publication](https://github.com/Sakamoto0110/NekoLib/actions/runs/45455772938)
remain unchanged. Earlier negative development and intermittent WPF focus
evidence is retained; no assertion was waived and no Navigation fix is claimed.

Coverage remains x64 native window-message responsiveness on the stated
frameworks. It does not certify all dispatchers/workers, business-operation
progress, missing-window targets, pre-activation startup, cross-user/elevation,
restricted-token, mixed-runtime or x86 desktop supervision, human visual
acceptance, nominal-duration soak, live SQL/ACE/hardware, or a final demo.
The [accepted contract](watchdog-ui-hang-detection.md) owns these boundaries.
