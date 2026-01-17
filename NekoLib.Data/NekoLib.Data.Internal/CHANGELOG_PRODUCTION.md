# NekoDatabaseGatewayModern - Production Hardening Patch (v2)

## Key changes
- **Dynamic mode switch**: IL / Expando / Disabled via `DatabaseGatewayOptions.DynamicMode`.
- **AOT guard**: IL mode is blocked when dynamic code is not supported (AOT). Fallback to Expando or fail-fast.
- **Schema cap**: hard cap on IL schemas (`MaxDynamicSchemas`) with deterministic fail/fallback.
- **.NET Framework 4.8.1 guards**: `IAsyncEnumerable` APIs compiled only on `NET6_0_OR_GREATER`. Core APIs remain available on net481.
- **OleDb parameters**: stable numeric ordering for `@pN` to avoid positional mismatches.
- **Exception safety**: query execution now rethrows after raising error events; cancellation is never swallowed.
- **Event leak prevention**: context dispose clears event handlers (optional via options).
- **Trimming notes**: public generic APIs have trimming-friendly annotations behind `#if NET6_0_OR_GREATER`.

## Defaults (recommended for kiosks)
- `DynamicMode = Expando`
- `MaxDynamicSchemas = 64`
- `FailOnDynamicSchemaLimit = true`

## Notes
- IL dynamic types generated via `Reflection.Emit` are not unloadable; capping prevents unbounded growth.
