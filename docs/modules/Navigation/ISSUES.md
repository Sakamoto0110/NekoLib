# NekoLib.Navigation Confirmed Issues

**Document ID:** NAV-ISSUES

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** confirmed defects in the NekoLib.Navigation family boundary

**Surface:** issues

**Boundary:** navigation

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

## NAV-ISSUE-001

**Status:** confirmed

**Severity:** low

**Affected releases:** 1.0.0 and current source at `e9609fa2a6ed6485e279a2f397da3cb89b46bb26`

**Symptom:** Accessing `NavigationService.Session`, `History`, `CanGoBack`, or `Events` before bootstrap or after shutdown throws an `InvalidOperationException` that tells the consumer to call the internal `NavigationService.UseContext` method.

**Trigger:** Read any of the four mounted-context properties while no context is mounted.

**Evidence:** `src/Navigation/NekoLib.Navigation/NavigationService.cs` routes all four properties through `EnsureContext()`, whose exception text names the internal method; the accepted API manifests expose the properties but not `UseContext`.

**Workaround:** Call `PageNavBootstrap.Start()` before reading mounted-context properties and do not read them after `NavigationService.Shutdown()` until a fresh context is mounted.

**Intended fix:** Align the public error text with `PageNavBootstrap.Start()` and the awaited-shutdown contract without changing the exception type or facade lifecycle.

**Fix release:** none

**Roadmap:** not promoted; scheduling requires explicit admission to [`TODO.md`](../../../TODO.md)
