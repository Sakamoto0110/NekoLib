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

**Status:** fixed

**Severity:** low

**Affected releases:** observed in 1.0.0 and the 1.4.0 line

**Symptom:** Accessing `NavigationService.Session`, `History`, `CanGoBack`, or `Events` before bootstrap or after shutdown raised an `InvalidOperationException` that directed the consumer to the internal `NavigationService.UseContext` method.

**Trigger:** Read any of the four mounted-context properties while no context is mounted.

**Evidence:** `ContextProperties_BeforeStartAndAfterShutdown_ReportPublicBootstrapGuidance` exercises all four properties on both sides of a public bootstrap lifecycle.

**Workaround:** Call `PageNavBootstrap.Start()` before reading mounted-context properties and do not read them after `NavigationService.Shutdown()` until a fresh context is mounted.

**Intended fix:** Align the public error text with `PageNavBootstrap.Start()` and the awaited-shutdown contract without changing the exception type or facade lifecycle.

**Fix release:** planned for 2.0.0, unreleased

**Roadmap:** tracked in the [family changelog](../../../CHANGELOG.md#unreleased).
