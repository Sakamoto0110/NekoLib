#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
namespace NekoLib.Data.Query
{
    /// <summary>
    /// Tradutor de <see cref="QueryModel"/> para SQL Server.
    /// Aplica TOP com sintaxe "SELECT DISTINCT TOP (n)" ou "SELECT TOP (n)".
    /// </summary>
    public sealed class SqlServerQueryTranslator : IDbQueryTranslator
    {
        public DatabaseQuery Translate(QueryModel Model)
        {
            if(Model == null) throw new ArgumentNullException(nameof(Model));

            string sql = Model.Sql;

            if(Model.Top.HasValue)
            {
                int top = Model.Top.Value;

                const string selectDistinct = "SELECT DISTINCT ";
                const string select = "SELECT ";

                if(sql.StartsWith(selectDistinct, StringComparison.OrdinalIgnoreCase))
                {
                    string rest = sql.Substring(selectDistinct.Length);
                    sql = "SELECT DISTINCT TOP (" + top + ") " + rest;
                }
                else if(sql.StartsWith(select, StringComparison.OrdinalIgnoreCase))
                {
                    string rest = sql.Substring(select.Length);
                    sql = "SELECT TOP (" + top + ") " + rest;
                }
            }

            Dictionary<string, object?> parameters = new Dictionary<string, object?>(Model.Parameters.ToDictionary(k=>k.Key,k=>k.Value));
            return new DatabaseQuery(sql, parameters);
        }
    }

    /// <summary>
    /// Tradutor de <see cref="QueryModel"/> para Access (OleDb).
    /// Aplica TOP com sintaxe "SELECT TOP n DISTINCT" ou "SELECT TOP n".
    /// </summary>
    public sealed class AccessQueryTranslator : IDbQueryTranslator
    {
        public DatabaseQuery Translate(QueryModel Model)
        {
            if(Model == null) throw new ArgumentNullException(nameof(Model));

            string sql = Model.Sql;

            if(Model.Top.HasValue)
            {
                int top = Model.Top.Value;

                const string selectDistinct = "SELECT DISTINCT ";
                const string select = "SELECT ";

                if(sql.StartsWith(selectDistinct, StringComparison.OrdinalIgnoreCase))
                {
                    string rest = sql.Substring(selectDistinct.Length);
                    sql = "SELECT TOP " + top + " DISTINCT " + rest;
                }
                else if(sql.StartsWith(select, StringComparison.OrdinalIgnoreCase))
                {
                    string rest = sql.Substring(select.Length);
                    sql = "SELECT TOP " + top + " " + rest;
                }
            }

            Dictionary<string, object?> parameters = new Dictionary<string, object?>(Model.Parameters.ToDictionary(k => k.Key, k => k.Value));
            return new DatabaseQuery(sql, parameters);
        }
    }
}
