#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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
            Undefined,
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
        private bool _allowAllRowsUpdate;

        private void StartStatement(QueryType queryType)
        {
            _queryType = queryType;
            _table = null;
            _columns.Clear();
            _conditions.Clear();
            _groupByColumns.Clear();
            _orderByColumns.Clear();
            _joins.Clear();
            _insertValues.Clear();
            _updateValues.Clear();
            _parameters.Clear();
            _paramIndex = 0;
            _top = null;
            _isDistinctSelect = false;
            _countColumn = null;
            _isDistinctCount = false;
            _allowAllRowsUpdate = false;
        }

        private void RequireQueryType(QueryType queryType, string operation)
        {
            if (_queryType != queryType)
            {
                throw new InvalidOperationException(
                    operation + " is not valid for the current query state.");
            }
        }

        private void RequirePredicateQuery(string operation)
        {
            if (_queryType != QueryType.Select && _queryType != QueryType.Update)
            {
                throw new InvalidOperationException(
                    operation + " requires an active SELECT or UPDATE statement.");
            }
        }

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
            RequireQueryType(QueryType.Select, nameof(Top));
            if (N <= 0)
                throw new ArgumentOutOfRangeException(nameof(N), "TOP must be greater than zero.");

            _top = N;
            return this;
        }

        #endregion

        #region SELECT / DISTINCT / COUNT

        public QueryBuilder Select(params string[] Columns)
        {
            StartStatement(QueryType.Select);
            if (Columns != null && Columns.Length > 0)
                _columns.AddRange(Columns);
            return this;
        }

        public QueryBuilder SelectDistinct(params string[] Columns)
        {
            StartStatement(QueryType.Select);
            _isDistinctSelect = true;
            if (Columns != null && Columns.Length > 0)
                _columns.AddRange(Columns);
            return this;
        }

        public QueryBuilder Distinct()
        {
            RequireQueryType(QueryType.Select, nameof(Distinct));
            if (_countColumn != null)
            {
                throw new InvalidOperationException(
                    "Distinct cannot modify a COUNT projection. Use DistinctCount instead.");
            }

            _isDistinctSelect = true;
            return this;
        }

        public QueryBuilder Count()
        {
            StartStatement(QueryType.Select);
            _countColumn = "*";
            _isDistinctCount = false;
            return this;
        }

        public QueryBuilder Count(string Column)
        {
            StartStatement(QueryType.Select);
            _countColumn = Column;
            _isDistinctCount = false;
            return this;
        }

        public QueryBuilder DistinctCount(string Column)
        {
            StartStatement(QueryType.Select);
            _countColumn = Column;
            _isDistinctCount = true;
            return this;
        }

        #endregion

        #region FROM / JOIN

        public QueryBuilder From(string Table)
        {
            RequireQueryType(QueryType.Select, nameof(From));
            _table = Table;
            return this;
        }

        /// <summary>
        /// Adiciona um JOIN genérico.
        /// Ex: Join("Produtos P", "P.Id = V.ProdutoId", "LEFT")
        /// </summary>
        public QueryBuilder Join(string Table, string OnExpression, string Type = "INNER")
        {
            RequireQueryType(QueryType.Select, nameof(Join));
            _joins.Add(Type + " JOIN " + Table + " ON " + OnExpression);
            return this;
        }

        #endregion

        #region WHERE

        public QueryBuilder Where(string Condition, params object[] Values)
        {
            RequirePredicateQuery(nameof(Where));
            object[] conditionValues = Values ?? Array.Empty<object>();
            int valueCount = conditionValues.Length;
            if (string.IsNullOrWhiteSpace(Condition))
            {
                if (valueCount != 0)
                    throw new ArgumentException("Condition values were supplied without a condition template.", nameof(Values));
                return this;
            }

            IReadOnlyList<SqlParameterToken> tokens = SqlParameterTokenizer.Tokenize(Condition);
            ValidateConditionTemplate(tokens, valueCount, nameof(Values));

            Dictionary<int, string> replacements = new Dictionary<int, string>();
            for (int index = 1; index <= valueCount; index++)
            {
                string parameterName = NewParamName();
                replacements[index] = parameterName;
                _parameters[parameterName] = conditionValues[index - 1];
            }

            _conditions.Add(RewriteParameterTokens(
                Condition,
                tokens,
                token => replacements[token.Index]));

            return this;
        }

        private static void ValidateConditionTemplate(
            IReadOnlyList<SqlParameterToken> tokens,
            int valueCount,
            string valuesParameterName)
        {
            HashSet<int> indexes = new HashSet<int>();
            for (int i = 0; i < tokens.Count; i++)
            {
                SqlParameterToken token = tokens[i];
                string canonicalName = "@p" + token.Index.ToString(CultureInfo.InvariantCulture);
                if (token.Index < 1 ||
                    !string.Equals(token.Name, canonicalName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(
                        "Condition placeholders must use the canonical @p1, @p2, ... grammar.",
                        valuesParameterName);
                }

                indexes.Add(token.Index);
            }

            if (indexes.Count != valueCount)
            {
                throw new ArgumentException(
                    "Condition placeholder count must exactly match the supplied value count.",
                    valuesParameterName);
            }

            for (int index = 1; index <= valueCount; index++)
            {
                if (!indexes.Contains(index))
                {
                    throw new ArgumentException(
                        "Condition placeholders must be contiguous and start at @p1.",
                        valuesParameterName);
                }
            }
        }

        public QueryBuilder WhereIn(string Column, IEnumerable<object> Values)
        {
            return AddCollectionCondition(Column, Values, negated: false);
        }

        public QueryBuilder WhereNotIn(string Column, IEnumerable<object> Values)
        {
            return AddCollectionCondition(Column, Values, negated: true);
        }

        private QueryBuilder AddCollectionCondition(
            string column,
            IEnumerable<object> values,
            bool negated)
        {
            RequirePredicateQuery(negated ? nameof(WhereNotIn) : nameof(WhereIn));
            if (column == null)
                throw new ArgumentNullException(nameof(column));
            if (string.IsNullOrWhiteSpace(column))
                throw new ArgumentException("A collection predicate requires a column name.", nameof(column));
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            List<object> list = new List<object>(values);
            if (list.Count == 0)
            {
                _conditions.Add(negated ? "1 = 1" : "1 = 0");
                return this;
            }

            List<string> prmNames = new List<string>();

            foreach (object value in list)
            {
                string paramName = NewParamName();
                prmNames.Add(paramName);
                _parameters[paramName] = value;
            }

            string keyword = negated ? " NOT IN (" : " IN (";
            _conditions.Add(column + keyword + string.Join(", ", prmNames) + ")");
            return this;
        }

        public QueryBuilder WhereBetween(string Column, object Start, object End)
        {
            RequirePredicateQuery(nameof(WhereBetween));
            string p1 = NewParamName();
            string p2 = NewParamName();

            _parameters[p1] = Start;
            _parameters[p2] = End;

            _conditions.Add(Column + " BETWEEN " + p1 + " AND " + p2);
            return this;
        }

        public QueryBuilder WhereLike(string Column, string Pattern)
        {
            RequirePredicateQuery(nameof(WhereLike));
            string p = NewParamName();
            _parameters[p] = Pattern;
            _conditions.Add(Column + " LIKE " + p);
            return this;
        }

        public QueryBuilder WhereExists(QueryBuilder SubQuery)
        {
            RequirePredicateQuery(nameof(WhereExists));
            if (SubQuery == null) return this;

            AddSubQueryCondition("EXISTS", SubQuery);
            return this;
        }

        public QueryBuilder WhereNotExists(QueryBuilder SubQuery)
        {
            RequirePredicateQuery(nameof(WhereNotExists));
            if (SubQuery == null) return this;

            AddSubQueryCondition("NOT EXISTS", SubQuery);
            return this;
        }

        private void AddSubQueryCondition(string keyword, QueryBuilder subQuery)
        {
            QueryModel model = subQuery.Build();
            if (model.Top.HasValue)
            {
                throw new NotSupportedException(
                    "TOP/LIMIT inside a subquery is not supported until nested query models are translated recursively.");
            }

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
            IReadOnlyList<SqlParameterToken> tokens = SqlParameterTokenizer.Tokenize(sql);
            return RewriteParameterTokens(
                sql,
                tokens,
                token => string.Equals(token.Name, oldName, StringComparison.OrdinalIgnoreCase)
                    ? newName
                    : token.Name);
        }

        private static string RewriteParameterTokens(
            string sql,
            IReadOnlyList<SqlParameterToken> tokens,
            Func<SqlParameterToken, string> replacement)
        {
            if (tokens.Count == 0)
                return sql;

            StringBuilder builder = new StringBuilder(sql.Length);
            int position = 0;
            for (int i = 0; i < tokens.Count; i++)
            {
                SqlParameterToken token = tokens[i];
                builder.Append(sql, position, token.Start - position);
                builder.Append(replacement(token));
                position = token.Start + token.Length;
            }

            builder.Append(sql, position, sql.Length - position);
            return builder.ToString();
        }

        #endregion

        #region GROUP BY / ORDER BY

        public QueryBuilder GroupBy(params string[] Columns)
        {
            RequireQueryType(QueryType.Select, nameof(GroupBy));
            if (Columns != null && Columns.Length > 0)
                _groupByColumns.AddRange(Columns);
            return this;
        }

        public QueryBuilder OrderBy(params string[] Columns)
        {
            RequireQueryType(QueryType.Select, nameof(OrderBy));
            if (Columns != null && Columns.Length > 0)
                _orderByColumns.AddRange(Columns);
            return this;
        }

        #endregion

        #region INSERT / UPDATE

        public QueryBuilder InsertInto(string Table, Dictionary<string, object?> Values)
        {
            StartStatement(QueryType.Insert);
            _table = Table;

            if (Values != null)
            {
                foreach (KeyValuePair<string, object?> kv in Values)
                    _insertValues[kv.Key] = kv.Value;
            }

            return this;
        }

        public QueryBuilder Update(string Table, Dictionary<string, object?> Values)
        {
            StartStatement(QueryType.Update);
            _table = Table;

            if (Values != null)
            {
                foreach (KeyValuePair<string, object?> kv in Values)
                    _updateValues[kv.Key] = kv.Value;
            }

            return this;
        }

        /// <summary>
        /// Explicitly allows the current UPDATE statement to affect every row.
        /// </summary>
        /// <remarks>
        /// This opt-in applies only to the current UPDATE state and is cleared
        /// when <see cref="Update(string, Dictionary{string, object?})"/> is
        /// called again.
        /// </remarks>
        public QueryBuilder AllowAllRowsUpdate()
        {
            if (_queryType != QueryType.Update)
                throw new InvalidOperationException("AllowAllRowsUpdate can be used only after Update.");

            _allowAllRowsUpdate = true;
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

            if (_conditions.Count == 0 && !_allowAllRowsUpdate)
            {
                throw new InvalidOperationException(
                    "UPDATE requires a predicate. Call AllowAllRowsUpdate to explicitly affect every row.");
            }

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

