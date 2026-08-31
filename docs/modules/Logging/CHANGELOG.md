# NekoLib.Logging Changelog

**Document ID:** LOG-CHANGELOG

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** consumer-visible evolution of the NekoLib.Logging boundary

**Surface:** changelog

**Boundary:** logging

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

The [coordinated family changelog](../../../CHANGELOG.md) remains the release
summary. This file records Logging-specific consumer impact without duplicating
package hashes or release provenance.

## 1.1.0

**Packages:** `NekoLib.Logging`

**Compatibility class:** behavioral

**Consumer impact:** Budget exhaustion now stops admission of later sink flushes immediately. Compiled signatures are unchanged, the returned value is unchanged, and XML comments now state the directory-creation behavior of the rolling file sink.

**Migration:** none

- `Flush(timeout)` distinguishes a sink that *exhausted the budget* from one
  that *failed*, and returns as soon as one is exhausted. Previously both
  outcomes were reported identically, so admission was stopped only by the next
  iteration's remaining-budget check; a wait that expired a fraction of a
  millisecond early left that check satisfied and admitted one more sink after
  the budget was already spent. The result was `false` before and after — what
  changed is that no further sink `Flush()` is started and abandoned once the
  bound is reached.
- `RollingFileLogSink.Write` documents that it creates the target directory
  when it is missing, which construction does not do.
- Immutable `1.1.0-local.9` is the qualifying package evidence for both the
  behavioral correction and the final XML documentation.

## 1.0.0

**Packages:** `NekoLib.Logging`

**Compatibility class:** behavioral

**Consumer impact:** The pre-stable candidate was corrected before the first stable contract. No public type, member, signature, nullability annotation, default value, namespace, target, or dependency changed, both accepted API manifests are unchanged, and no source change is required to keep compiling.

**Migration:** `docs/modules/Logging/migrations/f1.md`

- `DebugLogSink` writes through `Trace.WriteLine` instead of the
  `[Conditional("DEBUG")]` `Debug.WriteLine`. Every shipped package is built in
  Release, so the previous call was removed from the assembly and the sink
  discarded every entry. An application that composed the sink and saw nothing
  now sees output.
- `DebugLogSink.Write(null)` throws `ArgumentNullException`, matching
  `RollingFileLogSink` and the non-null `ILogSink.Write` annotation.
- `Logger.Flush` isolates a thrown sink failure and continues while budget
  remains; budget exhaustion still stops further admission.
- `Logger.Flush` observes the fault of a sink that outlived its budget, so a
  slow sink is no longer surfaced through `TaskScheduler.UnobservedTaskException`
  and recorded as a process crash by `NekoLib.Diagnostics`.
- `Logger.Flush` is inert after completed disposal instead of flushing already
  disposed sinks, and a concurrent bounded flush waits for the final disposal
  flush or returns `false` when its budget expires.
- `Logger` copies the supplied sink array at construction, so a caller that
  mutates its own array can no longer re-target a live pipeline.
