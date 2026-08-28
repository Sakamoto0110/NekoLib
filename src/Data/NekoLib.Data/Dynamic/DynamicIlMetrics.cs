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

        /// <summary>Gets the process-wide maximum number of emitted schemas.</summary>
        public int SchemaLimit { get; }

        /// <summary>Gets the number of schemas emitted in the current process.</summary>
        public int EmittedSchemaCount { get; }

        /// <summary>Gets the number of requests served by an existing emitted type.</summary>
        public long CacheHits { get; }

        /// <summary>Gets the number of requests that required a new schema lookup.</summary>
        public long CacheMisses { get; }

        /// <summary>Gets the number of emissions rejected by the schema limit.</summary>
        public long LimitRejections { get; }
    }
}
