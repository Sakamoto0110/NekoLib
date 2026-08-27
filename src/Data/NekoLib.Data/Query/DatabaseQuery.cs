#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NekoLib.Data.Gateway;

namespace NekoLib.Data.Query 
{
    /// <summary>
    /// Represents provider-specific SQL translated and ready for execution by
    /// <see cref="DatabaseGateway"/>.
    /// </summary>
    public sealed class DatabaseQuery
    {
        public string Sql { get; }

        /// <summary>
        /// Gets mutable parameters that may be adjusted after translation.
        /// </summary>
        public Dictionary<string, object?> Parameters { get; }

        private readonly Dictionary<string, LogicalParameter> _logicalByName;

        /// <summary>
        /// Gets a current logical-parameter snapshot. Mutations to the
        /// compatibility <see cref="Parameters"/> dictionary are reflected in
        /// this projection while structured metadata is retained by name.
        /// </summary>
        public IReadOnlyList<LogicalParameter> LogicalParameters
        {
            get
            {
                List<LogicalParameter> snapshot = new List<LogicalParameter>();
                foreach (KeyValuePair<string, object?> parameter in Parameters)
                {
                    LogicalParameter? logical;
                    if (_logicalByName.TryGetValue(parameter.Key, out logical))
                        snapshot.Add(logical.WithValue(parameter.Value));
                    else
                    {
                        snapshot.Add(new LogicalParameter(
                            parameter.Key,
                            parameter.Value,
                            null,
                            null,
                            parameter.Value?.GetType(),
                            null,
                            Array.Empty<TypeDecayRule>()));
                    }
                }
                return new ReadOnlyCollection<LogicalParameter>(snapshot);
            }
        }

        /// <summary>Gets the policy overrides for this translated command.</summary>
        public DbCommandPolicy CommandPolicy { get; }

        public DatabaseQuery(
            string sql,
            Dictionary<string, object?> parameters,
            DbCommandPolicy? commandPolicy = null)
            : this(sql, parameters, null, commandPolicy)
        {
        }

        /// <summary>
        /// Creates translated SQL while preserving logical parameter metadata.
        /// Custom translators should use this factory when they rewrite SQL but
        /// retain the model's logical identities.
        /// </summary>
        public static DatabaseQuery FromLogicalParameters(
            string sql,
            IEnumerable<LogicalParameter> logicalParameters,
            DbCommandPolicy? commandPolicy = null)
        {
            if (logicalParameters == null)
                throw new ArgumentNullException(nameof(logicalParameters));

            List<LogicalParameter> logical = new List<LogicalParameter>();
            Dictionary<string, object?> values = new Dictionary<string, object?>();
            foreach (LogicalParameter parameter in logicalParameters)
            {
                if (parameter == null)
                    throw new ArgumentException("Logical parameters cannot contain null.", nameof(logicalParameters));
                if (values.ContainsKey(parameter.Name))
                    throw new ArgumentException("Logical parameter names must be unique.", nameof(logicalParameters));
                logical.Add(parameter);
                values.Add(parameter.Name, parameter.Value);
            }
            return new DatabaseQuery(sql, values, logical, commandPolicy);
        }

        internal DatabaseQuery(
            string sql,
            Dictionary<string, object?> parameters,
            IEnumerable<LogicalParameter>? logicalParameters,
            DbCommandPolicy? commandPolicy = null)
        {
            if (sql == null) throw new ArgumentNullException("sql");
            Sql = sql;
            Parameters = parameters ?? new Dictionary<string, object?>();
            _logicalByName = new Dictionary<string, LogicalParameter>(StringComparer.Ordinal);
            if (logicalParameters != null)
            {
                foreach (LogicalParameter logical in logicalParameters)
                {
                    if (logical == null)
                        throw new ArgumentException("Logical parameters cannot contain null.", nameof(logicalParameters));
                    if (_logicalByName.ContainsKey(logical.Name))
                        throw new ArgumentException("Logical parameter names must be unique.", nameof(logicalParameters));
                    _logicalByName.Add(logical.Name, logical);
                }
            }
            CommandPolicy = commandPolicy?.Copy() ?? new DbCommandPolicy();
            CommandPolicy.Validate(nameof(commandPolicy));
        }
    }

}
