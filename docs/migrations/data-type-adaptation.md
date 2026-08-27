# Data Type-Adaptation Migration

**Kind:** guide

**Lifecycle:** current

**Subject:** adopting explicit input promotion, provider representation decay,
schema discovery, and sanitized adaptation reporting

**Affected package:** `NekoLib.Data`

**Affected release:** unreleased compatible minor after `1.0.0`

**Indexing:** include

The type-adaptation surface is additive. Existing QueryBuilder values continue
to bind with their supplied CLR types under the default
`TypePromotionPolicy.ExplicitOnly`; no automatic promotion is enabled merely by
upgrading the package.

## Explicit promotion

Declare the exact rule at the fluent value or predicate that owns the input:

```csharp
var command = new QueryBuilder()
    .Update("Inventory")
    .Set("Quantity", "54", parameter =>
        parameter.AllowPromotion(TypePromotions.StringToInt32))
    .Where("Id", QueryOperator.Equal, id);
```

The rule identifies source and target CLR types, converter strategy, optional
format/culture, and loss classification. The built-in string rules use exact
invariant parsers for integer, decimal, double, Boolean, `Guid`, `DateTime`, and
`DateTimeOffset` targets. Custom rules use `TypePromotionRule`; converters run
locally and any exception is replaced by sanitized structural evidence.

The promotion modes are:

| Mode | Behavior |
|---|---|
| `Disabled` | Reject every promotion, including a rule attached to the value. |
| `ExplicitOnly` | Default. Require the exact rule on the logical value. |
| `SchemaValidated` | Also allow a registered lossless rule when known-provider schema proves the field target. |

`parameter.As<T>()` declares a semantic target but does not authorize a
conversion under `ExplicitOnly`. Pair it with `AllowPromotion(...)`, or select
`SchemaValidated` deliberately at the gateway options boundary.

## Schema discovery

`SchemaDiscoveryMode.Lazy` loads only the selected structured field metadata on
first schema-dependent use and caches it for the gateway lifetime. Concurrent
first uses share one load. Discovery must happen before a session transaction
begins; preload transaction-dependent fields when using long-lived sessions.

```csharp
var options = new DatabaseGatewayOptions
{
    TypePromotionPolicy = TypePromotionPolicy.SchemaValidated,
    SchemaDiscoveryMode = SchemaDiscoveryMode.Preload
};

await gateway.PreloadSchemaAsync(
    "dbo.Inventory",
    new[] { "Quantity" },
    cancellationToken);
```

Use `RefreshSchemaAsync` for selected fields after a migration, or
`ClearSchemaCache` when the complete gateway-local cache is stale. `Disabled`
prevents automatic lookup. Raw SQL is never parsed for schema; retain
`DbParameterSpec` or provider-specific application code when a raw command
needs an explicit physical binding.

SQL Server and Access/OleDb use ADO.NET schema collections. SQLite uses
`PRAGMA table_info` after its provider rejects `GetSchema`. Automatic
schema-based promotion is unavailable for an unknown provider profile; exact
binding and explicit rules remain available.

## Decay and loss

Decay is a fallback from the preferred semantic representation after a known
provider profile rejects it. It is not a database-error retry. The command is
adapted completely before its single dispatch.

`TypeDecayPolicy.AllowFallback` is the default. Set `Strict` to reject the
first incompatible preferred representation. Lossless registered fallbacks may
run automatically when schema confirms their target. Potentially lossy rules
require both a rule on the logical value and the gateway opt-in:

```csharp
var options = new DatabaseGatewayOptions
{
    TypeLossPolicy = TypeLossPolicy.AllowExplicitAndReport
};

var command = new QueryBuilder()
    .InsertInto("Events")
    .Value("OccurredAt", occurredAt, parameter =>
        parameter.AllowDecay(TypeDecays.DateTimeOffsetToUtcDateTime));
```

This specific rule loses the original offset and is always reported. Prefer
the lossless round-trip string rule when the provider column is textual.

Decay candidates are ordered. A rejected native representation may fall
through to a formatter-backed string rule without dispatching an unsuccessful
database command:

```csharp
var formatter = TypeDecays.CreateDateTimeOffsetToString(
    "yyyy/MM/dd HH:mm:ss:fff",
    CultureInfo.InvariantCulture);

var command = new QueryBuilder()
    .InsertInto("Events")
    .Value("OccurredAt", occurredAt, parameter => parameter
        .AllowDecay(TypeDecays.DateTimeOffsetToUtcDateTime)
        .AllowDecayFallback(formatter));
```

The example format is illustrative, not canonical. In .NET custom date/time
syntax, `MM` is month and `mm` is minute. Because this formatter omits the
original offset and part of the available precision, the factory classifies it
as `PotentiallyLossy` by default; it therefore requires
`AllowExplicitAndReport`. Use the built-in round-trip `"O"` rule when the text
must preserve the full temporal representation.

## Observing adaptation safely

Subscribe on the concrete gateway instance:

```csharp
gateway.OnTypeAdaptation += adaptation =>
{
    metrics.Record(
        adaptation.Kind,
        adaptation.ReasonCode,
        adaptation.Loss,
        adaptation.StrategyId,
        adaptation.Format,
        adaptation.CultureName);
};
```

The synchronous hook is notification only. Throwing from a subscriber neither
changes the database outcome nor prevents later subscribers from running. One
logical adaptation raises one event even when a positional binder creates
several physical parameters. Event data is deliberately value-free: do not
expect SQL, values, parameter collections, connection strings, credentials, or
raw inner exceptions. `Attempts` records rejected candidates in order and the
selected formatter, including its non-secret format and culture metadata.

Final local failures use `TypeAdaptationException` with source/target types,
provider and strategy identity, loss class, and a stable reason code. The
exception does not retain an inner conversion exception that could echo input.

## Current boundary

This release activates promotion, decay, schema discovery, and hooks for
structured QueryBuilder command parameters on both target frameworks. Existing
DTO read materialization remains governed by `DataMappingFailureMode`; read-side
temporal reporting and per-field loss authorization are not part of this slice.
