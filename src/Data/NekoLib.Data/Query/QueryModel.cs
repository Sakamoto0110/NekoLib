#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NekoLib.Data.Query 
{
    /// <summary>
    /// Modelo neutro de consulta SQL gerado pelo <see cref="QueryBuilder"/>.
    /// Contém SQL base (sem TOP específico de provedor), parâmetros e metadados.
    /// </summary>
    public sealed class QueryModel
    {
        /// <summary>
        /// SQL neutra gerada pelo <see cref="QueryBuilder"/>.
        /// </summary>
        public string Sql { get; }

        /// <summary>
        /// Parâmetros do comando (somente leitura externamente).
        /// </summary>
        public IReadOnlyDictionary<string, object?> Parameters { get; }

        /// <summary>
        /// Valor de TOP solicitado (caso exista). Interpretação fica a cargo do tradutor.
        /// </summary>
        public int? Top { get; }

        public QueryModel(string Sql, Dictionary<string, object?> Parameters, int? Top = null)
        {
            if (Sql == null) throw new ArgumentNullException(nameof(Sql));
            if (Parameters == null) throw new ArgumentNullException(nameof(Parameters));

            this.Sql = Sql;
            this.Parameters = new Dictionary<string, object?>(Parameters);
            this.Top = Top;
        }
    }



    /// <summary>
    /// Interface de tradutores de <see cref="QueryModel"/> para <see cref="DatabaseQuery"/> (SQL Server, Access, etc.).
    /// </summary>
    public interface IDbQueryTranslator
    {
        /// <summary>
        /// Traduz um <see cref="QueryModel"/> neutro para um <see cref="DatabaseQuery"/> específico de provedor.
        /// </summary>
        DatabaseQuery Translate(QueryModel Model);
    }
}