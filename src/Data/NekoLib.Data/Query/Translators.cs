#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
namespace NekoLib.Data.Query
{
    /// <summary>
    /// Translates <see cref="QueryModel"/> for SQL Server and applies row
    /// limits with <c>SELECT DISTINCT TOP (n)</c> or <c>SELECT TOP (n)</c>.
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
            return new DatabaseQuery(sql, parameters, Model.CommandPolicy);
        }
    }

    /// <summary>
    /// Translates <see cref="QueryModel"/> for Access/OleDb and applies row
    /// limits with <c>SELECT DISTINCT TOP n</c> or <c>SELECT TOP n</c>.
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
                    sql = "SELECT DISTINCT TOP " + top + " " + rest;
                }
                else if(sql.StartsWith(select, StringComparison.OrdinalIgnoreCase))
                {
                    string rest = sql.Substring(select.Length);
                    sql = "SELECT TOP " + top + " " + rest;
                }
            }

            Dictionary<string, object?> parameters = new Dictionary<string, object?>(Model.Parameters.ToDictionary(k => k.Key, k => k.Value));
            return new DatabaseQuery(sql, parameters, Model.CommandPolicy);
        }
    }

    /// <summary>
    /// Translates <see cref="QueryModel"/> for SQLite and applies row limits
    /// with <c>LIMIT n</c>.
    /// </summary>
    public sealed class SqliteQueryTranslator : IDbQueryTranslator
    {
        public DatabaseQuery Translate(QueryModel Model)
        {
            if(Model == null) throw new ArgumentNullException(nameof(Model));

            string sql = Model.Sql;

            if(Model.Top.HasValue)
            {
                sql = sql + " LIMIT " + Model.Top.Value;
            }

            Dictionary<string, object?> parameters = new Dictionary<string, object?>(Model.Parameters.ToDictionary(k => k.Key, k => k.Value));
            return new DatabaseQuery(sql, parameters, Model.CommandPolicy);
        }
    }
}
