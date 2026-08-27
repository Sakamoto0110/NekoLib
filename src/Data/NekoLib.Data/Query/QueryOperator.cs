#nullable enable

namespace NekoLib.Data.Query
{
    /// <summary>
    /// Defines the provider-neutral comparison used by a structured predicate.
    /// </summary>
    public enum QueryOperator
    {
        Equal,
        NotEqual,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual
    }
}
