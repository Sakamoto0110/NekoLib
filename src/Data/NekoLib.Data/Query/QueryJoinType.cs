#nullable enable

namespace NekoLib.Data.Query
{
    /// <summary>
    /// Defines the provider-neutral join type used by a structured join.
    /// </summary>
    public enum QueryJoinType
    {
        Inner,
        Left,
        Right,
        Full
    }
}
