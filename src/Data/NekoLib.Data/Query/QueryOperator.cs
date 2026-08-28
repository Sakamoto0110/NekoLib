#nullable enable

namespace NekoLib.Data.Query
{
    /// <summary>
    /// Defines the provider-neutral comparison used by a structured predicate.
    /// </summary>
    public enum QueryOperator
    {
        /// <summary>Equality comparison.</summary>
        Equal,
        /// <summary>Inequality comparison.</summary>
        NotEqual,
        /// <summary>Strict greater-than comparison.</summary>
        GreaterThan,
        /// <summary>Greater-than-or-equal comparison.</summary>
        GreaterThanOrEqual,
        /// <summary>Strict less-than comparison.</summary>
        LessThan,
        /// <summary>Less-than-or-equal comparison.</summary>
        LessThanOrEqual
    }
}
