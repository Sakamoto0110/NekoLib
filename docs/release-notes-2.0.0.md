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

- **Navigation.** Errors for reading an unmounted context or attempting a second
  bootstrap now name the recovery steps. Mount with `PageNavBootstrap.Start()`
  and await `NavigationService.Shutdown()` before starting another context.
- **Diagnostics.** The existing folder-writing default requires a nonblank
  `CrashRootDirectory` to write the text report. `DumpWriter` defaults to null.
  To request a Windows minidump, apply `UseMiniDump()` to the options before
  constructing the handler.
- **Inspection.** Once a provider times out, `CaptureSnapshot` skips the
  remaining callbacks and records a timeout marker for each remaining provider.

See the [coordinated family changelog](../CHANGELOG.md#unreleased) for the
release summary.
