# F1-DATA Candidate Migration

**Kind:** guide

**Lifecycle:** current

**Subject:** migration from the initial NekoLib.Data candidate surface to the
accepted F1-DATA public contract

**Affected package:** `NekoLib.Data`

**Affected release:** pre-stable candidates targeting the first `1.0.0` stable
family release

F1-DATA intentionally corrects the candidate API before the first stable
release. There is no compatibility shim or deprecation window for the removed
candidate members.

## Gateway namespace

Replace the old concrete gateway import:

```csharp
using NekoLib.Data.Internal.Gateway;
```

with:

```csharp
using NekoLib.Data.Gateway;
```

`DatabaseGateway` now lives beside `IDatabaseGateway` and its capability
interfaces. No type remains at the old public name.

## Universal read removal

Use the context-owned translator and an explicit result family:

| Removed member | Replacement |
|---|---|
| `Get<TTranslator,T>(builder, ...)` | configure `TTranslator` in `QueryExecutionContext`, then call `GetDto<T>(builder, ...)` |
| `Read<T>(builder, callback, ...)` for DTOs | `ReadDto<T>(builder, callback, ...)` |
| `Read<DynamicRow>` or `Read<object>` | `ReadDynamic(builder, callback, ...)` |
| `Read(builder, Delegate, ...)` | a typed `ReadDto<T>` or `ReadDynamic` callback |
| `StreamData<T>(builder, ...)` | `StreamDto<T>(builder, ...)` |
| dynamic `StreamData(builder, ...)` | `StreamDynamic(builder, ...)` |

Remove references to `IUniversalQueryGateway`; it has no replacement interface
because its behavior is already present in the explicit capability contracts.

## Session overload order

String DML session overloads now use the same order as builder, query, and
streaming overloads:

```csharp
// Before
await gateway.Update(sql, parameters, cancellationToken, session);

// After
await gateway.Update(sql, parameters, session, cancellationToken);
```

This applies to `Insert`, `Update`, and `Delete`. Calls that previously selected
the two-argument concrete convenience overload must supply the parameter slot:

```csharp
// Before
await gateway.Insert(sql, cancellationToken);

// After
await gateway.Insert(sql, null, cancellationToken);
```

The supported capability methods are public on both `DatabaseGateway` and the
matching interfaces. Code no longer needs a cast merely to reach string DTO,
session-aware DML, `Delete`, or non-session `StreamRaw` members.

## Parameterized existence checks

Replace inlined values with the new parameter overload when appropriate:

```csharp
bool found = await gateway.ContainsData(
    "SELECT Id FROM Products WHERE Quantity > @p1",
    new Dictionary<string, object?> { ["@p1"] = threshold },
    cancellationToken);
```

The original non-parameter and session overloads remain available.

## Fluent delete support

`Delete` now has the same builder overloads as `Insert` and `Update`. Prefer the
builder for ordinary predicates so deletion participates in translation and
raises `OnSqlGenerated` before command dispatch:

```csharp
await gateway.Delete(
    new QueryBuilder()
        .DeleteFrom("Rows")
        .Where("Id = @p1", id),
    cancellationToken);
```

`DeleteFrom` is fail-closed. A statement without a predicate throws by default;
an intentional whole-table operation must opt in explicitly, and the opt-in is
cleared when another statement is started:

```csharp
await gateway.Delete(
    new QueryBuilder()
        .DeleteFrom("TemporaryRows")
        .AllowAllRowsDelete(),
    cancellationToken);
```

Raw string `Delete` overloads remain available for provider-specific SQL and
compatibility. Since that SQL is supplied directly rather than generated, the
raw lifecycle begins at `OnSqlDispatch` rather than `OnSqlGenerated`.

## net481 streaming surface

`IDqlStreamingGateway` no longer exists in the `net481` assembly. Keep streaming
code behind the target guard and use callback reads on `net481`:

```csharp
#if NET6_0_OR_GREATER
await foreach (Product product in gateway.StreamDto<Product>(query, cancellationToken))
{
    Consume(product);
}
#else
await gateway.ReadDto<Product>(query, Consume, cancellationToken);
#endif
```

The package no longer carries `Microsoft.Bcl.AsyncInterfaces` solely to publish
an unusable net481 interface.

## Sealed concrete types

The supported extension seams are interfaces and composition. Migrate derived
types as follows:

| Former base type | Migration |
|---|---|
| `DatabaseGateway` | wrap or depend on `IDatabaseGateway` |
| `QueryExecutionContext` | compose a context; supply a custom factory, translator, options, or event subscribers |
| `QueryBuilder` | use helper/extension methods that return or configure a builder |
| `RecordItem` | compose or translate the lossy cell value |
| `DbConnectionAbstractFactory<T>` | implement `IDbConnectionFactory` for custom creation behavior |

The protected gateway `Upsert` member and protected context disposal hook are no
longer public extension contracts.

## Internalized helper and metadata

`DbDataReaderExtensions.HasColumn` is now internal. Consumer code should use the
ordinary `IDataRecord` members or its own application extension.

On `net9.0`, DTO interface and `DataMapper` entry points now declare the public
constructor/property reflection requirements already used by the concrete
implementation. This is metadata correction and does not declare the entire
package trim-safe or NativeAOT-compatible.
