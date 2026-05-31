#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
namespace NekoLib.Data.Query 
{
    /// <summary>
    /// Construtor fluente neutro de SQL parametrizada (SELECT, INSERT, UPDATE).
    /// Gera um <see cref="QueryModel"/> que é traduzido por <see cref="IDbQueryTranslator"/>.
    /// </summary>
    public class QueryBuilder
    {
        internal enum QueryType
        {
            Select,
            Insert,
            Update
        }

        private QueryType _queryType;
        private string? _table;

        private readonly List<string> _columns = new List<string>();
        private readonly List<string> _conditions = new List<string>();
        private readonly List<string> _groupByColumns = new List<string>();
        private readonly List<string> _orderByColumns = new List<string>();
        private readonly List<string> _joins = new List<string>();

        private readonly Dictionary<string, object?> _insertValues = new Dictionary<string, object?>();
        private readonly Dictionary<string, object?> _updateValues = new Dictionary<string, object?>();

        private readonly Dictionary<string, object?> _parameters = new Dictionary<string, object?>();
        private int _paramIndex;

        /// <summary>
        /// Parâmetros acumulados durante a construção da query.
        /// </summary>
        public IReadOnlyDictionary<string, object?> Parameters
        {
            get => _parameters;
        }

        private int? _top;

        private bool _isDistinctSelect;
        private string? _countColumn;
        private bool _isDistinctCount;

        private string NewParamName()
        {
            _paramIndex++;
            return "@p" + _paramIndex;
        }

        private static string NewParamName(ref int paramIndex)
        {
            paramIndex++;
            return "@p" + paramIndex;
        }

        #region TOP

        /// <summary>
        /// Define SELECT TOP N. Interpretação fica para o tradutor.
        /// </summary>
        public QueryBuilder Top(int N)
        {
            if (N > 0)
                _top = N;
            return this;
        }

        #endregion

        #region SELECT / DISTINCT / COUNT

        public QueryBuilder Select(params string[] Columns)
        {
            _queryType = QueryType.Select;
            if (Columns != null && Columns.Length > 0)
                _columns.AddRange(Columns);
            return this;
        }

        public QueryBuilder SelectDistinct(params string[] Columns)
        {
            _queryType = QueryType.Select;
            _isDistinctSelect = true;
            if (Columns != null && Columns.Length > 0)
                _columns.AddRange(Columns);
            return this;
        }

        public QueryBuilder Distinct()
        {
            _queryType = QueryType.Select;
            _isDistinctSelect = true;
            return this;
        }

        public QueryBuilder Count()
        {
            _queryType = QueryType.Select;
            _countColumn = "*";
            _isDistinctCount = false;
            return this;
        }

        public QueryBuilder Count(string Column)
        {
            _queryType = QueryType.Select;
            _countColumn = Column;
            _isDistinctCount = false;
            return this;
        }

        public QueryBuilder DistinctCount(string Column)
        {
            _queryType = QueryType.Select;
            _countColumn = Column;
            _isDistinctCount = true;
            return this;
        }

        #endregion

        #region FROM / JOIN

        public QueryBuilder From(string Table)
        {
            _table = Table;
            return this;
        }

        /// <summary>
        /// Adiciona um JOIN genérico.
        /// Ex: Join("Produtos P", "P.Id = V.ProdutoId", "LEFT")
        /// </summary>
        public QueryBuilder Join(string Table, string OnExpression, string Type = "INNER")
        {
            _joins.Add(Type + " JOIN " + Table + " ON " + OnExpression);
            return this;
        }

        #endregion

        #region WHERE

        public QueryBuilder Where(string Condition, params object[] Values)
        {
            if (!string.IsNullOrWhiteSpace(Condition) && Values != null && Values.Length > 0)
            {
                for (int i = 0; i < Values.Length; i++)
                {
                    string placeholder = "@p" + (i + 1);
                    string paramName = NewParamName();

                    Condition = Regex.Replace(
                        Condition,
                        Regex.Escape(placeholder) + @"\b",
                        paramName);

                    _parameters[paramName] = Values[i];
                }
            }

            if (!string.IsNullOrWhiteSpace(Condition))
                _conditions.Add(Condition);

            return this;
        }

        public QueryBuilder WhereIn(string Column, IEnumerable<object> Values)
        {
            if (string.IsNullOrEmpty(Column) || Values == null)
                return this;

            List<object> list = new List<object>(Values);
            if (list.Count == 0) return this;

            List<string> prmNames = new List<string>();

            foreach (object value in list)
            {
                string paramName = NewParamName();
                prmNames.Add(paramName);
                _parameters[paramName] = value;
            }

            _conditions.Add(Column + " IN (" + string.Join(", ", prmNames) + ")");
            return this;
        }

        public QueryBuilder WhereNotIn(string Column, IEnumerable<object> Values)
        {
            if (string.IsNullOrEmpty(Column) || Values == null)
                return this;

            List<object> list = new List<object>(Values);
            if (list.Count == 0) return this;

            List<string> prmNames = new List<string>();

            foreach (object value in list)
            {
                string paramName = NewParamName();
                prmNames.Add(paramName);
                _parameters[paramName] = value;
            }

            _conditions.Add(Column + " NOT IN (" + string.Join(", ", prmNames) + ")");
            return this;
        }

        public QueryBuilder WhereBetween(string Column, object Start, object End)
        {
            string p1 = NewParamName();
            string p2 = NewParamName();

            _parameters[p1] = Start;
            _parameters[p2] = End;

            _conditions.Add(Column + " BETWEEN " + p1 + " AND " + p2);
            return this;
        }

        public QueryBuilder WhereLike(string Column, string Pattern)
        {
            string p = NewParamName();
            _parameters[p] = Pattern;
            _conditions.Add(Column + " LIKE " + p);
            return this;
        }

        public QueryBuilder WhereExists(QueryBuilder SubQuery)
        {
            if (SubQuery == null) return this;

            AddSubQueryCondition("EXISTS", SubQuery);
            return this;
        }

        public QueryBuilder WhereNotExists(QueryBuilder SubQuery)
        {
            if (SubQuery == null) return this;

            AddSubQueryCondition("NOT EXISTS", SubQuery);
            return this;
        }

        private void AddSubQueryCondition(string keyword, QueryBuilder subQuery)
        {
            QueryModel model = subQuery.Build();
            string sql = model.Sql;

            foreach (KeyValuePair<string, object?> kv in model.Parameters)
            {
                string newName = NewParamName();
                sql = ReplaceParameterName(sql, kv.Key, newName);
                _parameters[newName] = kv.Value;
            }

            _conditions.Add(keyword + " (" + sql + ")");
        }

        private static string ReplaceParameterName(string sql, string oldName, string newName)
        {
            return Regex.Replace(sql, Regex.Escape(oldName) + @"\b", newName);
        }

        #endregion

        #region GROUP BY / ORDER BY

        public QueryBuilder GroupBy(params string[] Columns)
        {
            if (Columns != null && Columns.Length > 0)
                _groupByColumns.AddRange(Columns);
            return this;
        }

        public QueryBuilder OrderBy(params string[] Columns)
        {
            if (Columns != null && Columns.Length > 0)
                _orderByColumns.AddRange(Columns);
            return this;
        }

        #endregion

        #region INSERT / UPDATE

        public QueryBuilder InsertInto(string Table, Dictionary<string, object?> Values)
        {
            _queryType = QueryType.Insert;
            _table = Table;
            _insertValues.Clear();

            if (Values != null)
            {
                foreach (KeyValuePair<string, object?> kv in Values)
                    _insertValues[kv.Key] = kv.Value;
            }

            return this;
        }

        public QueryBuilder Update(string Table, Dictionary<string, object?> Values)
        {
            _queryType = QueryType.Update;
            _table = Table;
            _updateValues.Clear();

            if (Values != null)
            {
                foreach (KeyValuePair<string, object?> kv in Values)
                    _updateValues[kv.Key] = kv.Value;
            }

            return this;
        }

        #endregion

        #region BUILD

        public QueryModel Build()
        {
            string sql;
            Dictionary<string, object?> parameters = new Dictionary<string, object?>(_parameters);
            int buildParamIndex = _paramIndex;

            switch (_queryType)
            {
                case QueryType.Select:
                    sql = BuildSelect();
                    break;

                case QueryType.Insert:
                    sql = BuildInsert(parameters, ref buildParamIndex);
                    break;

                case QueryType.Update:
                    sql = BuildUpdate(parameters, ref buildParamIndex);
                    break;

                default:
                    throw new InvalidOperationException("Query type was not defined. Call Select/Insert/Update first.");
            }

            return new QueryModel(sql, parameters, _top);
        }

        private string BuildSelect()
        {
            if (string.IsNullOrEmpty(_table))
                throw new InvalidOperationException("FROM table not specified.");

            string cols;

            if (!string.IsNullOrEmpty(_countColumn))
            {
                if (_isDistinctCount)
                    cols = "COUNT(DISTINCT " + _countColumn + ")";
                else
                    cols = "COUNT(" + _countColumn + ")";
            }
            else
            {
                if (_isDistinctSelect)
                {
                    if (_columns.Count > 0)
                        cols = "DISTINCT " + string.Join(", ", _columns);
                    else
                        cols = "DISTINCT *";
                }
                else
                {
                    cols = _columns.Count > 0 ? string.Join(", ", _columns) : "*";
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("SELECT ").Append(cols).Append(" FROM ").Append(_table);

            if (_joins.Count > 0)
                sb.Append(" ").Append(string.Join(" ", _joins));

            if (_conditions.Count > 0)
                sb.Append(" WHERE ").Append(string.Join(" AND ", _conditions));

            if (_groupByColumns.Count > 0)
                sb.Append(" GROUP BY ").Append(string.Join(", ", _groupByColumns));

            if (_orderByColumns.Count > 0)
                sb.Append(" ORDER BY ").Append(string.Join(", ", _orderByColumns));

            // TOP é aplicado pelo tradutor.
            return sb.ToString();
        }

        private string BuildInsert(Dictionary<string, object?> parameters, ref int buildParamIndex)
        {
            if (string.IsNullOrEmpty(_table))
                throw new InvalidOperationException("INSERT table not specified.");

            if (_insertValues.Count == 0)
                throw new InvalidOperationException("No values specified for INSERT.");

            List<string> cols = new List<string>(_insertValues.Keys);
            List<string> paramNames = new List<string>();

            for (int i = 0; i < cols.Count; i++)
            {
                string column = cols[i];
                string paramName = NewParamName(ref buildParamIndex);
                paramNames.Add(paramName);
                parameters[paramName] = _insertValues[column];
            }

            string sql = "INSERT INTO " + _table +
                         " (" + string.Join(", ", cols) + ")" +
                         " VALUES (" + string.Join(", ", paramNames) + ")";

            return sql;
        }

        private string BuildUpdate(Dictionary<string, object?> parameters, ref int buildParamIndex)
        {
            if (string.IsNullOrEmpty(_table))
                throw new InvalidOperationException("UPDATE table not specified.");

            if (_updateValues.Count == 0)
                throw new InvalidOperationException("No values specified for UPDATE.");

            List<string> sets = new List<string>();

            foreach (KeyValuePair<string, object?> kv in _updateValues)
            {
                string paramName = NewParamName(ref buildParamIndex);
                sets.Add(kv.Key + " = " + paramName);
                parameters[paramName] = kv.Value;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("UPDATE ").Append(_table).Append(" SET ").Append(string.Join(", ", sets));

            if (_conditions.Count > 0)
                sb.Append(" WHERE ").Append(string.Join(" AND ", _conditions));

            return sb.ToString();
        }

        #endregion
    }

}

