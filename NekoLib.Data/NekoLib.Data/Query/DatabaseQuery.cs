#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NekoLib.Data.Query 
{
    /// <summary>
    /// Representa uma consulta já traduzida para o SQL específico de um provedor,
    /// pronta para ser executada pelo <see cref="DatabaseGateway"/>.
    /// </summary>
    public sealed class DatabaseQuery
    {
        public string Sql { get; }

        /// <summary>
        /// Parâmetros mutáveis, permitindo ajustes após a tradução, se necessário.
        /// </summary>
        public Dictionary<string, object?> Parameters { get; }

        public DatabaseQuery(string sql, Dictionary<string, object?> parameters)
        {
            if (sql == null) throw new ArgumentNullException("sql");
            Sql = sql;
            Parameters = parameters ?? new Dictionary<string, object?>();
        }
    }

}