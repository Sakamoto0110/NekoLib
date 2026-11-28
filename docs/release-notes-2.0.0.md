# Upcoming NekoLib 2.0.0 release notes

**Kind:** guide

**Lifecycle:** current

**Subject:** consumer migration to the upcoming coordinated 2.0.0 package family

**Release status:** unreleased

## Upgrade from 1.x

`NekoLib.Data` removes four public `QueryBuilder` methods on both `net481`
and `net9.0`. Their replacements have been available since 1.1.0. They let
callers express comparisons and assignments directly and identify the places
where an application supplies trusted SQL text.

| Removed signature | Migration |
|---|---|
| `InsertInto(string Table, Dictionary<string, object?> Values)` | Call `InsertInto(Table)`, then `Value(column, value)` for each entry. |
| `Update(string Table, Dictionary<string, object?> Values)` | Call `Update(Table)`, then `Set(column, value)` for each entry. Keep the update predicate. |
| `Where(string Condition, params object[] Values)` | Use `Where(column, queryOperator, value)` for comparisons or `WhereTrusted(Condition, Values)` for a trusted SQL template. |
| `Join(string Table, string OnExpression, string Type = "INNER")` | Use `JoinOn` for a column comparison or `JoinTrusted` for a trusted ON expression. Both default to `QueryJoinType.Inner`. |

For example, a two-argument `Join("Stores s", "s.Id = Orders.StoreId")`
becomes `JoinOn("Stores s", "s.Id", "Orders.StoreId")`. To keep the original
trusted expression, use `JoinTrusted("Stores s", "s.Id = Orders.StoreId")`.

The change breaks source and binary compatibility for callers of these methods.
Migrate those calls while on 1.1.0 or later in the 1.x line, then rebuild
every application or library that references a removed member before deploying
with 2.0. Align all NekoLib packages to the same family version, including
`NekoLib.Watchdog.Host` for sidecar deployments. The
[QueryBuilder migration guide](modules/Data/migrations/querybuilder-structured-api.md)
covers dictionary inputs, nulls and join types.

## Other consumer-visible changes

- **Watchdog.** Await `WatchdogController.RequestStopAsync()` for a stop request with a 60-second acceptance deadline. Handle the four [receipt outcomes](modules/Watchdog/REFERENCE.md#control-contract) explicitly.

- **Navigation.** Errors for reading an unmounted context or attempting a second
  bootstrap now name the recovery steps. Mount with `PageNavBootstrap.Start()`
  and await `NavigationService.Shutdown()` before starting another context.
- **Navigation idle timeout.** In user-input handlers for controls outside the
  observed host, call `NavigationService.NotifyUserInteraction()` before
  requesting navigation. This gives the user a full inactivity interval even
  when a previous timer tick was already scheduled. Automatic sign-out now
  closes open popovers and resolves their pending results with `false` before
  presenting Idle. Both fixes passed the focused suite and a package-only
  WinForms probe on both supported targets. [Idle timeout contract](modules/Navigation/REFERENCE.md#idle-system)
- **Navigation prompt cancellation and drafts.** Session sign-out and navigation
  to Idle close open prompts and return the default result for their result type.
  A text prompt returns `null`. Direct `GoIdleAsync()` preserves the session.
  Implement `IPromptDraftState` to prefill the next opening on the same page in
  that session. [Prompt issue](modules/Navigation/ISSUES.md#nav-issue-004)
- **Navigation timeouts.** In 2.0, `UseIdleTimeout(TimeSpan.FromMinutes(5))`
  expresses the same inactivity interval as the 1.x call `UseIdleTimeout(300_000)`.
  Adding `UseSessionTimeout(TimeSpan.FromMinutes(30))` opts in to a
  fixed interval from each successful sign-in. The policy requires an Idle page
  and operates independently of the inactivity timeout. Both bootstrap options
  accept `TimeSpan`, and `UseIdleTimeout(int)` still accepts milliseconds. [Configuration](modules/Navigation/REFERENCE.md#maximum-authenticated-session-lifetime)
- **Diagnostics.** The existing folder-writing default requires a nonblank
  `CrashRootDirectory` to write the text report. `DumpWriter` defaults to null.
  To request a Windows minidump, apply `UseMiniDump()` to the options before
  constructing the handler.
- **Inspection.** Once a provider times out, `CaptureSnapshot` skips the
  remaining callbacks and records a timeout marker for each remaining provider.

See the [coordinated family changelog](../CHANGELOG.md#unreleased) for the
release summary.

## Known issues and qualification limits

Independent review of the local 2.0.0 packages built from `b418aa69184b1006f006f13f114f3951c1597db7` returned **NOT READY FOR PUBLICATION** under the current release criteria. All 1,933 solution tests passed and all 30 packaged API surfaces matched the approved baselines. The following runtime and deployment checks remain open.

### A local Pipes echo exceeded the consumer's reply deadline

A same-user echo request exceeded its three-second response timeout during the WPF `net481` package check. A WinForms `net481` replay using the same package bytes timed out at the same read. A subsequent WPF run succeeded. A caller can therefore receive a timeout even in this local configuration. The measurements establish neither the cause of the delay nor how often it occurs. Follow-up needs timing evidence across request handling and reply delivery.

### Legacy Watchdog shutdown exceeded two test deadlines

On WinForms `net481`, cleanup after a fatal worker failure and automatic stop in same-executable mode each exceeded the 15-second exit deadline. The desktop recovery suite passed 23 of 24 cases and the automatic bootstrap suite passed 143 of 144. Applications that need to know when a process has exited must observe process exit separately. The 60-second deadline on `RequestStopAsync()` covers acceptance of a request. Its acceptance and queue-expiry cases both passed under load. Follow-up needs to locate the delay in the two legacy shutdown paths. [Watchdog control contract](modules/Watchdog/REFERENCE.md#control-contract)

### Clean leaves a published Watchdog.Host in a custom directory

For sidecar deployments on both supported target families, `Clean` removed the helper from the build output and left the previously published helper in an explicitly configured `PublishDir`. Reusing that directory can carry the helper into a later deployment. Packaging from a fresh publish directory avoids carrying over that previous file. The cleanup boundary for custom publish directories remains open under `WDGHOST-005`.

### An Inspection capture exceeded the requested budget

A .NET 9 run measured 212 ms from capture invocation to return with a 200 ms provider budget. The workload passed its three-second test guard. Consumers should allow for a difference between the configured provider budget and the observed end-to-end latency. Closing `INSP-010` requires specifying which interval the budget covers and measuring that interval in the regression.

### Cleanup of a partially written native dump remains unverified

The qualification inspected twelve real dump files. The invalid-destination case failed before native writing began. Whether a partial file is removed when the native writer fails mid-write remains unknown. A consumer collecting dumps after a crash should treat a file's presence alone as insufficient evidence that the dump is complete. Closing `DIAG-011` requires observing a failure after writing begins and checking the resulting file state.
