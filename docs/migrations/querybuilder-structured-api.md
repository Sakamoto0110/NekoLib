# QueryBuilder Structured API Migration

**Kind:** guide

**Lifecycle:** current

**Subject:** migration from dictionary and condition-template QueryBuilder calls
to the canonical structured fluent API

**Affected package:** `NekoLib.Data`

**Affected release:** unreleased compatible minor after `1.0.0`

**Indexing:** include

`QueryBuilder` now separates ordinary structured calls from explicitly trusted
SQL fragments. The new APIs are additive. Replaced overloads remain callable
with warning-only `Obsolete` markers and will not be removed before `2.0.0`.

## INSERT and UPDATE values

Replace dictionary-first statements with one fluent value operation per column:

```csharp
// Before
var insert = new QueryBuilder().InsertInto(
    "Inventory",
    new Dictionary<string, object?>
    {
        ["Sku"] = sku,
        ["Quantity"] = quantity
    });

// After
var insert = new QueryBuilder()
    .InsertInto("Inventory")
    .Value("Sku", sku)
    .Value("Quantity", quantity);
```

```csharp
// Before
var update = new QueryBuilder()
    .Update(
        "Inventory",
        new Dictionary<string, object?> { ["Quantity"] = quantity })
    .Where("Id = @p1", id);

// After
var update = new QueryBuilder()
    .Update("Inventory")
    .Set("Quantity", quantity)
    .Where("Id", QueryOperator.Equal, id);
```

Repeated `Value` or `Set` calls for the same column retain only the last value,
matching the former dictionary assignment behavior. Repeated `Build()` calls
remain idempotent.

## Structured predicates

Use `Where(column, operator, value)` for ordinary comparisons. Supported
operators are `Equal`, `NotEqual`, `GreaterThan`, `GreaterThanOrEqual`,
`LessThan`, and `LessThanOrEqual`.

```csharp
builder
    .Where("Quantity", QueryOperator.GreaterThan, minimum)
    .Where("Active", QueryOperator.Equal, true);
```

`Equal` with a null value emits `IS NULL`; `NotEqual` emits `IS NOT NULL`.
Other operators reject null instead of emitting a meaningless comparison.

When a structured predicate cannot represent the required SQL, make the trust
boundary explicit:

```csharp
builder.WhereTrusted(
    "Inventory.Quantity > @p1 OR Inventory.Reserved > @p1",
    minimum);
```

`WhereTrusted` retains the canonical `@p1`, `@p2`, ... template validation and
parameterization behavior of the former overload. The condition text itself is
trusted SQL and must not contain untrusted input.

## Joins

Use `JoinOn` for structured column comparisons:

```csharp
builder.JoinOn(
    "Warehouses w",
    "w.Id",
    "Inventory.WarehouseId",
    QueryJoinType.Left);
```

The overload that accepts `QueryOperator` supports non-equality joins. Use
`JoinTrusted` only when the ON-expression cannot be represented structurally:

```csharp
builder.JoinTrusted(
    "Thresholds t",
    "Inventory.Quantity BETWEEN t.Minimum AND t.Maximum",
    QueryJoinType.Inner);
```

## Compatibility window

These overloads remain binary and source compatible but now emit a compiler
warning with their concrete replacements:

| Deprecated overload | Canonical replacement |
|---|---|
| `InsertInto(string, Dictionary<string, object?>)` | `InsertInto(string).Value(...)` |
| `Update(string, Dictionary<string, object?>)` | `Update(string).Set(...)` |
| `Where(string, params object[])` | structured `Where(...)` or `WhereTrusted(...)` |
| `Join(string, string, string)` | `JoinOn(...)` or `JoinTrusted(...)` |

The attributes use `error: false`. They remain for at least one released minor
and may be removed only in `2.0.0` or later.

## Provider binding boundary

The fluent API does not expose named versus positional binding. It creates the
same logical placeholders for every provider. Named binders retain logical
identity; positional binders use final SQL occurrence order and may therefore
bind an UPDATE assignment before a predicate even when the predicate received
the lower placeholder number.

The structured parameter identities introduced here now also carry optional
type-adaptation intent. Promotion, provider adaptation, schema discovery,
decay/loss policy, and the observational hook are documented separately in the
[Data type-adaptation migration guide](data-type-adaptation.md). Existing calls
without configuration retain their exact supplied values unless the consumer
opts into `SchemaValidated` promotion.

Table names, column names, projections, grouping, ordering, and every explicitly
trusted fragment remain caller-supplied SQL. The builder parameterizes values;
it does not quote or validate identifiers.
