#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        /// <summary>Gets the policy overrides for this translated command.</summary>
        public DbCommandPolicy CommandPolicy { get; }

        public DatabaseQuery(
            string sql,
            Dictionary<string, object?> parameters,
            DbCommandPolicy? commandPolicy = null)
        {
            if (sql == null) throw new ArgumentNullException("sql");
            Sql = sql;
            Parameters = parameters ?? new Dictionary<string, object?>();
            CommandPolicy = commandPolicy?.Copy() ?? new DbCommandPolicy();
            CommandPolicy.Validate(nameof(commandPolicy));
        }
    }

}
