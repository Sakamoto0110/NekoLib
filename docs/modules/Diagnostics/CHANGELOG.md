# NekoLib.Diagnostics Changelog

**Document ID:** DIAG-CHANGELOG

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** consumer-visible evolution of the NekoLib.Diagnostics family

**Surface:** changelog

**Boundary:** diagnostics

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

The [coordinated family changelog](../../../CHANGELOG.md) remains the release
summary. This file records Diagnostics-specific consumer impact without
duplicating package hashes or release provenance.

## 1.1.0

**Packages:** `NekoLib.Diagnostics`, `NekoLib.Diagnostics.Windows`

**Compatibility class:** documentation-only

**Consumer impact:** XML comments now state the existing dump, redaction, notifier, extra-line, and late-writer boundaries more precisely; compiled signatures and runtime behavior are unchanged.

**Migration:** none

- `CrashDumpLevel` is described as a non-cumulative request whose native flag
  mapping belongs to the Windows adapter.
- `Redact` explicitly excludes native dump bytes.
- `ExternalNotifier` is documented as running after the artifact stage even
  when folder writing is disabled or fails.
- `ExtraLines` is count-unbounded but time-bounded, redacted, and line-truncated.
- A timed-out custom writer can finish late; event-time `DumpWritten` remains
  the outcome authority.
- Immutable `1.1.0-local.9` is the qualifying package evidence for these XML
  corrections and the stable release.

## 1.0.0

**Packages:** `NekoLib.Diagnostics`, `NekoLib.Diagnostics.Windows`

**Compatibility class:** mixed

**Consumer impact:** The pre-stable candidate surface was corrected before the first stable contract; consumers upgrading from an earlier candidate may require source changes and recompilation.

**Migration:** `docs/modules/Diagnostics/migrations/f1.md`

- `CrashHandlerOptions.NotifyWatchdog` was removed in favor of the unconditional,
  application-owned `ExternalNotifier` callback.
- Options became construction-time snapshots, `Dispose()` became terminal, and
  the last disposed handler releases shared exception hooks.
- `CrashBundleFailed` made artifact loss observable; local evidence caps,
  per-record isolation, contributor settle margins, and tail-name collision
  handling were added.
- Diagnostics.Windows made WinForms hook dispatch idempotent, preserved explicit
  startup, removed failed dump artifacts, merged process error-mode flags, and
  documented exact non-cumulative minidump mapping.
