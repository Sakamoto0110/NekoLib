#nullable enable
using System;

namespace NekoLib.Data.RuntimeTests.SqlServer.Model
{
    /// <summary>
    /// The typed shapes the DTO paths map into.
    /// <para/>
    /// Property types are chosen to match the SQL types exactly, because strict
    /// mapping is the library's default and a scenario that quietly relaxed it
    /// would stop testing the thing it is here to test. Ordinary classes rather
    /// than records: <c>record</c> needs <c>IsExternalInit</c>, which
    /// <c>net481</c> does not have.
    /// </summary>
    public sealed class WarehouseRow
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public DateTime OpenedOn { get; set; }
        public bool Active { get; set; }
    }

    public sealed class PartRow
    {
        public int Id { get; set; }
        public int WarehouseId { get; set; }
        public string Sku { get; set; } = string.Empty;

        /// <summary>Nullable in the schema, so nullable here; strict mapping rejects the alternative.</summary>
        public string? Description { get; set; }

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public double? Weight { get; set; }
        public long Serial { get; set; }
        public bool Discontinued { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class MovementRow
    {
        public long Id { get; set; }
        public int PartId { get; set; }
        public DateTime OccurredAt { get; set; }
        public string Kind { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>A joined projection, used to prove aliases survive the mapper.</summary>
    public sealed class PartLocationRow
    {
        public string Sku { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    /// <summary>The server's session id, read through the gateway to observe pool reuse.</summary>
    public sealed class SessionIdRow
    {
        public int Spid { get; set; }
    }

    /// <summary>An aggregate projection; SQL Server requires the alias, and so does the mapper.</summary>
    public sealed class CategoryTotalRow
    {
        public int WarehouseId { get; set; }
        public int TotalQuantity { get; set; }
        public decimal TotalValue { get; set; }
    }
}
