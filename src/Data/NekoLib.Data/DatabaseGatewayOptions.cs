using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NekoLib.Data.Mapping;

namespace NekoLib.Data 
{
    /// <summary>Controls fallback to blocking ADO.NET provider methods.</summary>
    public enum DbSynchronousFallbackMode
    {
        /// <summary>Requires native asynchronous provider support.</summary>
        Disabled = 0,

        /// <summary>
        /// Allows a blocking call after the provider rejects its async method.
        /// Cancellation is checked immediately before the blocking call but
        /// cannot interrupt that call once it has started.
        /// </summary>
        Enabled = 1
    }

    /// <summary>
    /// Available dynamic-result modes.
    /// </summary>
    [Flags]
    public enum DynamicMode
    {
        /// <summary>
        /// Uses the optional Reflection.Emit compatibility path. Emitted types
        /// are process-wide and not unloadable; Expando remains the production default.
        /// </summary>
        IL = 1,

        /// <summary>Uses the AOT-safe ExpandoObject/dictionary path.</summary>
        Expando = 2,

        /// <summary>Disables dynamic results and requires DTO or raw APIs.</summary>
        Disabled = 4,
    }

    /// <summary>
    /// Configures production-safe gateway behavior.
    /// </summary>
    public sealed class DatabaseGatewayOptions
    {
        /// <summary>
        /// Gets or sets the dynamic-result mode. Expando is recommended in production.
        /// </summary>
        public DynamicMode DynamicMode { get; set; } = DynamicMode.Expando;

        /// <summary>
        /// Gets or sets the process-wide IL schema limit requested by this
        /// context. The first IL use locks the process limit; later contexts
        /// cannot reconfigure it because emitted types are not unloadable.
        /// </summary>
        public int MaxDynamicSchemas { get; set; } = 64;

        /// <summary>
        /// Gets or sets whether an unsupported or exhausted IL path throws
        /// instead of using the permitted Expando fallback.
        /// </summary>
        public bool FailOnDynamicSchemaLimit { get; set; } = true;

        /// <summary>
        /// Gets or sets whether IL may fall back to Expando, including on AOT runtimes.
        /// </summary>
        public bool AllowExpandoFallback { get; set; } = true;

        /// <summary>
        /// Gets or sets whether disposing the query context clears event subscribers.
        /// </summary>
        public bool ClearEventsOnContextDispose { get; set; } = true;

        /// <summary>
        /// Gets or sets whether query events receive the original SQL. The
        /// secure default redacts SQL to avoid exposing literals in logs.
        /// </summary>
        public bool EmitRawSqlInEvents { get; set; } = false;

        /// <summary>
        /// Gets or sets whether success events may include the complete result object.
        /// </summary>
        public bool IncludeCommandResultInSuccessEvents { get; set; } = false;

        /// <summary>
        /// Maximum number of recent query-observer failures retained by each
        /// <see cref="Query.QueryExecutionContext"/>.
        /// </summary>
        public int MaxObserverFailures { get; set; } = 32;

        /// <summary>
        /// Controls DTO property failures. Strict mapping is the production default.
        /// </summary>
        public DataMappingFailureMode MappingFailureMode { get; set; } =
            DataMappingFailureMode.Strict;

        /// <summary>
        /// Gets or sets the command timeout used when a command has no
        /// per-query override. A null value preserves the provider default.
        /// </summary>
        public int? DefaultCommandTimeoutSeconds { get; set; }

        /// <summary>
        /// Gets or sets the parameter-marker policy. Automatic selects
        /// positional binding only for OleDb commands.
        /// </summary>
        public DbParameterBindingMode ParameterBindingMode { get; set; } =
            DbParameterBindingMode.Automatic;

        /// <summary>
        /// Gets or sets the explicit opt-in for providers that do not support
        /// native asynchronous open, execute, or read operations.
        /// </summary>
        public DbSynchronousFallbackMode SynchronousFallbackMode { get; set; } =
            DbSynchronousFallbackMode.Disabled;

        public void Validate()
        {
            if (MaxDynamicSchemas < 1) MaxDynamicSchemas = 1;
            if (MaxObserverFailures < 1) MaxObserverFailures = 1;
            if (MappingFailureMode != DataMappingFailureMode.Strict &&
                MappingFailureMode != DataMappingFailureMode.Lenient)
            {
                throw new ArgumentOutOfRangeException(nameof(MappingFailureMode));
            }
            if (DefaultCommandTimeoutSeconds.HasValue &&
                DefaultCommandTimeoutSeconds.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(DefaultCommandTimeoutSeconds));
            }
            if (!Enum.IsDefined(typeof(DbParameterBindingMode), ParameterBindingMode))
                throw new ArgumentOutOfRangeException(nameof(ParameterBindingMode));
            if (!Enum.IsDefined(typeof(DbSynchronousFallbackMode), SynchronousFallbackMode))
                throw new ArgumentOutOfRangeException(nameof(SynchronousFallbackMode));
        }
    }
}
