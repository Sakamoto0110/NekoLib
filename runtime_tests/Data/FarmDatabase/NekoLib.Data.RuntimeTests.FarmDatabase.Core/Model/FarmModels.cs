#nullable enable
using System;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.Core.Model
{
    /// <summary>
    /// DTOs consumed by <c>IDqlGateway.GetDto&lt;T&gt;</c>, which maps by public
    /// property name against the reader's column names. Names therefore have to
    /// match the schema exactly - and they are the same in both dialects, which is
    /// the point of keeping the DDL aligned across profiles.
    /// </summary>
    public sealed class Role
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public double BaseSalary { get; set; }

        public override string ToString() => Title;
    }

    public sealed class Employee
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Cpf { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public int RoleId { get; set; }
    }

    /// <summary>Stock item: fruit, vegetable or legume.</summary>
    public sealed class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public double UnitPrice { get; set; }

        public override string ToString() => Name + " (" + Quantity + " " + Unit + ")";
    }

    public sealed class Animal
    {
        public int Id { get; set; }
        public string Species { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public int AgeYears { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string? Notes { get; set; }

        public override string ToString() => Tag + " - " + Species;
    }

    /// <summary>
    /// One audited stock movement. <c>OccurredAt</c> is stored as a round-trip
    /// ISO-8601 string rather than a native date: Access and SQLite disagree on date
    /// literals and on how ADO.NET surfaces them, and a sortable text column keeps
    /// ordering identical on both engines without a dialect branch.
    /// </summary>
    public sealed class OperationLogEntry
    {
        public int Id { get; set; }
        public string OccurredAt { get; set; } = string.Empty;
        public string EntityKind { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string EntityName { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string? Reason { get; set; }

        public DateTime OccurredAtLocal =>
            DateTime.TryParse(
                OccurredAt,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out DateTime parsed)
                ? parsed.ToLocalTime()
                : DateTime.MinValue;
    }

    /// <summary>
    /// What the registration prompt collects. The tag is deliberately absent: it is
    /// assigned by the database inside the same transaction as the insert, so the
    /// caller never gets to pick one.
    /// </summary>
    public sealed class NewAnimalRequest
    {
        public string Species { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public int AgeYears { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>Entity families the operation log can reference.</summary>
    public static class EntityKinds
    {
        public const string Product = "Produto";
        public const string Animal = "Animal";
    }

    /// <summary>Operations the log records.</summary>
    public static class Operations
    {
        public const string Add = "Entrada";
        public const string Remove = "Saída";
    }

    /// <summary>Product categories seeded by the scenario.</summary>
    public static class Categories
    {
        public const string Fruit = "Fruta";
        public const string Vegetable = "Verdura";
        public const string Legume = "Legume";
    }
}
