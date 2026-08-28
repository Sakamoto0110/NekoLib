#nullable enable

namespace NekoLib.Data.Query
{
    /// <summary>
    /// Defines the provider-neutral join type used by a structured join.
    /// </summary>
    public enum QueryJoinType
    {
        /// <summary>Inner join.</summary>
        Inner,
        /// <summary>Left outer join.</summary>
        Left,
        /// <summary>Right outer join.</summary>
        Right,
        /// <summary>Full outer join.</summary>
        Full
    }
}
