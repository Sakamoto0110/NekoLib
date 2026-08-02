#nullable enable

namespace NekoLib.Data.Dynamic
{
    /// <summary>
    /// Describes process-wide Reflection.Emit schema activity for dynamic rows.
    /// </summary>
    public sealed class DynamicIlMetrics
    {
        internal DynamicIlMetrics(
            int schemaLimit,
            int emittedSchemaCount,
            long cacheHits,
            long cacheMisses,
            long limitRejections)
        {
            SchemaLimit = schemaLimit;
            EmittedSchemaCount = emittedSchemaCount;
            CacheHits = cacheHits;
            CacheMisses = cacheMisses;
            LimitRejections = limitRejections;
        }

        public int SchemaLimit { get; }
        public int EmittedSchemaCount { get; }
        public long CacheHits { get; }
        public long CacheMisses { get; }
        public long LimitRejections { get; }
    }
}
