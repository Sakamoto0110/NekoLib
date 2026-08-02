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
    /// Modos possíveis para retornos dinâmicos.
    /// </summary>
    [Flags]
    public enum DynamicMode
    {
        /// <summary>Usa Reflection.Emit (rápido, mas não funciona em AOT e não é unloadable).</summary>
        IL = 1,

        /// <summary>Usa ExpandoObject/Dictionary (sem Reflection.Emit, seguro para AOT).</summary>
        Expando = 2,

        /// <summary>Desabilita retornos dinâmicos (força DTO/Raw).</summary>
        Disabled = 4,
    }

    /// <summary>
    /// Opções globais para o DatabaseGateway (produção / kiosk-safe).
    /// </summary>
    public sealed class DatabaseGatewayOptions
    {
        /// <summary>
        /// Modo dinâmico padrão (recomendado: Expando em produção).
        /// </summary>
        public DynamicMode DynamicMode { get; set; } = DynamicMode.Expando;

        /// <summary>
        /// Limite máximo de "schemas" para modo IL. (Tipos emitidos não são unloadable).
        /// </summary>
        public int MaxDynamicSchemas { get; set; } = 64;

        /// <summary>
        /// Se true, ao estourar o limite ou quando IL não é suportado, lança exceção.
        /// Se false, faz fallback para Expando (se permitido).
        /// </summary>
        public bool FailOnDynamicSchemaLimit { get; set; } = true;

        /// <summary>
        /// Se true, permite fallback automático para Expando quando IL não é suportado (ex: AOT).
        /// </summary>
        public bool AllowExpandoFallback { get; set; } = true;

        /// <summary>
        /// Se true, limpa eventos em Dispose() do QueryExecutionContext (evita leaks por assinantes).
        /// </summary>
        public bool ClearEventsOnContextDispose { get; set; } = true;

        /// <summary>
        /// Se true, eventos recebem o SQL original. O padrão evita vazamento de literais em logs.
        /// </summary>
        public bool EmitRawSqlInEvents { get; set; } = false;

        /// <summary>
        /// Se true, eventos de sucesso podem carregar o objeto de resultado completo.
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
