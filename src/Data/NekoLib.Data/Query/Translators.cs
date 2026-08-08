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

            string sql = RewriteDistinctCount(Model.Sql);

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

        /// <summary>
        /// Rewrites <c>COUNT(DISTINCT expr)</c> as a count over a distinct subselect.
        /// <para/>
        /// <c>COUNT(DISTINCT …)</c> is ordinary SQL and <see cref="QueryBuilder"/>
        /// emits it for every dialect, but Jet and ACE have never supported it: Access
        /// answers <i>Syntax error (missing operator) in query expression</i> and the
        /// query cannot run at all. Counting the rows of a distinct subselect is the
        /// equivalent Access does accept; the derived table is aliased because Jet
        /// requires one.
        /// <para/>
        /// Anything that is not exactly this shape is returned untouched.
        /// </summary>
        private static string RewriteDistinctCount(string sql)
        {
            const string marker = "SELECT COUNT(DISTINCT ";

            if(sql == null || !sql.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
                return sql!;

            int depth = 1;
            int index = marker.Length;

            while(index < sql.Length)
            {
                if(sql[index] == '(') depth++;
                else if(sql[index] == ')') depth--;

                if(depth == 0) break;
                index++;
            }

            // Unbalanced: leave it alone and let the engine report it.
            if(depth != 0)
                return sql;

            string expression = sql.Substring(marker.Length, index - marker.Length);
            string rest = sql.Substring(index + 1);

            return "SELECT COUNT(*) FROM (SELECT DISTINCT " + expression + rest + ") AS [d]";
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
