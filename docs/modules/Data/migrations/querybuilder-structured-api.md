# QueryBuilder Structured API Migration

**Document ID:** DATA-MIGRATION-QUERYBUILDER-STRUCTURED-API

**Schema version:** 1

**Kind:** guide

**Lifecycle:** current

**Subject:** migration from dictionary and condition-template QueryBuilder calls to the canonical structured fluent API

**Surface:** migration

**Boundary:** data

**Authority role:** non-normative

**Mutation:** authored

**Affected package:** `NekoLib.Data`

**Affected release:** `1.1.0` introduction, `2.0.0` removal (unreleased)

**Indexing:** include

**Reference date:** 2026-11-27

The [Data technical reference](../REFERENCE.md) owns the current contract.
See the [upcoming 2.0.0 release notes](../../../release-notes-2.0.0.md) and
the [family changelog](../../../../CHANGELOG.md#unreleased) for the upgrade's
impact on other packages.

`1.1.0` introduced structured calls and explicitly trusted SQL fragments,
with warning-only deprecations for four replaced overloads throughout `1.x`.
The `2.0.0` source removes those overloads on both targets. Update source
callers as shown below and rebuild consuming binaries before upgrading.
The before-migration examples show the removed `1.x` API.

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

For values supplied as a dictionary at runtime, enumerate it into the builder:

```csharp
var insert = new QueryBuilder().InsertInto("Inventory");
foreach (var entry in values)
    insert.Value(entry.Key, entry.Value);

var update = new QueryBuilder().Update("Inventory");
foreach (var entry in values)
    update.Set(entry.Key, entry.Value);
update.Where("Id", QueryOperator.Equal, id);
```

The removed overloads accepted a null dictionary as an empty value collection.
If the application allows null, skip enumeration in that case and keep the
builder's existing empty-statement and unconstrained-update validation.

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
parameterization behavior of the former overload. Renaming a call from
`Where("Id = @p1", id)` to `WhereTrusted("Id = @p1", id)` preserves that
template behavior. The condition text itself is trusted SQL and must not
contain untrusted input.

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

Both replacements take `QueryJoinType` instead of the legacy join-type string.
If the old call omitted `Type`, keep the default inner join by omitting it
from the replacement. For example, `Join("Warehouses w", "w.Id = Inventory.WarehouseId")`
becomes `JoinOn("Warehouses w", "w.Id", "Inventory.WarehouseId")`. To reuse
the same trusted SQL expression, call `JoinTrusted("Warehouses w", "w.Id = Inventory.WarehouseId")`.
Map `INNER`, `LEFT`, `RIGHT`, and `FULL` to `Inner`, `Left`, `Right`,
and `Full`. `LEFT OUTER`, `RIGHT OUTER`, and `FULL OUTER` map to those same
outer join values. Provider-specific join types outside that enum require the gateway's
[raw SQL surface](../REFERENCE.md#raw-sql-and-explicit-parameter-binding).

## Completed compatibility window

These overloads emitted warning-only deprecations starting in the released
`1.1.0` family. They are removed from the upcoming `2.0.0` family:

| Deprecated overload | Canonical replacement |
|---|---|
| `InsertInto(string Table, Dictionary<string, object?> Values)` | `InsertInto(Table).Value(...)` |
| `Update(string Table, Dictionary<string, object?> Values)` | `Update(Table).Set(...)` |
| `Where(string Condition, params object[] Values)` | structured `Where(...)` or `WhereTrusted(Condition, Values)` |
| `Join(string Table, string OnExpression, string Type = "INNER")` | `JoinOn(...)` or `JoinTrusted(...)` |

The warning-only window spanned the released `1.x` minors before this major.
An existing binary compiled against a removed member also needs a source
migration and rebuild. The coordinated `2.0.0` family stays unreleased until
its release qualification and publication complete.

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
