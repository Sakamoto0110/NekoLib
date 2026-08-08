#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
namespace NekoLib.Data.Query 
{
    /// <summary>
    /// Builds provider-neutral SELECT, INSERT, and UPDATE statements and
    /// produces a <see cref="QueryModel"/> for an <see cref="IDbQueryTranslator"/>.
    /// </summary>
    /// <remarks>
    /// Only values supplied through supported condition placeholders or value
    /// collections are parameterized. Table names, column names, projections,
    /// joins, grouping, ordering, and raw condition text are trusted SQL
    /// fragments. Applications must not pass untrusted input to those fragments.
    /// </remarks>
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

        /// <summary>
        /// Positions in <see cref="_conditions"/> that carry a subquery. Kept so the
        /// WHERE clause can put them first - see <see cref="OrderedConditions"/>.
        /// </summary>
        private readonly HashSet<int> _subQueryConditions = new HashSet<int>();
        private readonly List<string> _groupByColumns = new List<string>();
        private readonly List<string> _orderByColumns = new List<string>();
        private readonly List<string> _joins = new List<string>();

        private readonly Dictionary<string, object?> _insertValues = new Dictionary<string, object?>();
        private readonly Dictionary<string, object?> _updateValues = new Dictionary<string, object?>();

        private readonly Dictionary<string, object?> _parameters = new Dictionary<string, object?>();
        private int _paramIndex;

        /// <summary>
        /// Gets the values parameterized by the current statement.
        /// </summary>
        public IReadOnlyDictionary<string, object?> Parameters
        {
            get => _parameters;
        }

        private int? _top;
        private int? _commandTimeoutSeconds;

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
            _subQueryConditions.Clear();
            _groupByColumns.Clear();
            _orderByColumns.Clear();
            _joins.Clear();
            _insertValues.Clear();
            _updateValues.Clear();
            _parameters.Clear();
            _paramIndex = 0;
            _top = null;
            _commandTimeoutSeconds = null;
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
        /// Overrides the context command timeout for the current statement.
        /// </summary>
        public QueryBuilder CommandTimeout(int seconds)
        {
            if (_queryType == QueryType.Undefined)
                throw new InvalidOperationException("CommandTimeout requires an active statement.");
            if (seconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(seconds), "Command timeout must be greater than zero.");

            _commandTimeoutSeconds = seconds;
            return this;
        }

        /// <summary>
        /// Sets the provider-neutral row limit for the current SELECT statement.
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

        /// <summary>
        /// Starts a SELECT statement with trusted projection fragments.
        /// </summary>
        public QueryBuilder Select(params string[] Columns)
        {
            StartStatement(QueryType.Select);
            if (Columns != null && Columns.Length > 0)
                _columns.AddRange(Columns);
            return this;
        }

        /// <summary>
        /// Starts a DISTINCT SELECT statement with trusted projection fragments.
        /// </summary>
        public QueryBuilder SelectDistinct(params string[] Columns)
        {
            StartStatement(QueryType.Select);
            _isDistinctSelect = true;
            if (Columns != null && Columns.Length > 0)
                _columns.AddRange(Columns);
            return this;
        }

        /// <summary>
        /// Applies DISTINCT to the active SELECT projection.
        /// </summary>
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

        /// <summary>
        /// Starts a COUNT(*) SELECT statement.
        /// </summary>
        public QueryBuilder Count()
        {
            StartStatement(QueryType.Select);
            _countColumn = "*";
            _isDistinctCount = false;
            return this;
        }

        /// <summary>
        /// Starts a COUNT SELECT statement for a trusted column fragment.
        /// </summary>
        public QueryBuilder Count(string Column)
        {
            StartStatement(QueryType.Select);
            _countColumn = Column;
            _isDistinctCount = false;
            return this;
        }

        /// <summary>
        /// Starts a COUNT(DISTINCT ...) SELECT statement for a trusted column fragment.
        /// </summary>
        public QueryBuilder DistinctCount(string Column)
        {
            StartStatement(QueryType.Select);
            _countColumn = Column;
            _isDistinctCount = true;
            return this;
        }

        #endregion

        #region FROM / JOIN

        /// <summary>
        /// Sets the trusted table or table-expression fragment for a SELECT statement.
        /// </summary>
        public QueryBuilder From(string Table)
        {
            RequireQueryType(QueryType.Select, nameof(From));
            _table = Table;
            return this;
        }

        /// <summary>
        /// Adds a join assembled from trusted table, ON-expression, and join-type fragments.
        /// </summary>
        public QueryBuilder Join(string Table, string OnExpression, string Type = "INNER")
        {
            RequireQueryType(QueryType.Select, nameof(Join));
            _joins.Add(Type + " JOIN " + Table + " ON " + OnExpression);
            return this;
        }

        #endregion

        #region WHERE

        /// <summary>
        /// Adds a trusted condition template and parameterizes only canonical
        /// <c>@p1</c>, <c>@p2</c>, ... placeholders outside literals and comments.
        /// </summary>
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

        /// <summary>
        /// Adds an IN predicate for a trusted column fragment and parameterized values.
        /// </summary>
        public QueryBuilder WhereIn(string Column, IEnumerable<object> Values)
        {
            return AddCollectionCondition(Column, Values, negated: false);
        }

        /// <summary>
        /// Adds a NOT IN predicate for a trusted column fragment and parameterized values.
        /// </summary>
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

        /// <summary>
        /// Adds a BETWEEN predicate for a trusted column fragment and parameterized values.
        /// </summary>
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

        /// <summary>
        /// Adds a LIKE predicate for a trusted column fragment and a parameterized pattern.
        /// </summary>
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

            _subQueryConditions.Add(_conditions.Count);
            _conditions.Add(keyword + " (" + sql + ")");
        }

        /// <summary>
        /// The WHERE conditions with any subquery predicate placed first.
        /// <para/>
        /// Ordering here is not cosmetic; on a positional provider it decides whether
        /// the query is answered correctly. Placeholders are rewritten to <c>?</c> and
        /// bound in the order they appear in the text, but the ACE/Jet engine consumes
        /// them with the subquery's first regardless of where it sits in the clause. A
        /// predicate written before an <c>EXISTS</c> therefore receives the subquery's
        /// value and vice versa.
        /// <para/>
        /// Measured against a real database: with an integer predicate before an
        /// <c>EXISTS</c> carrying a string, Access answered <i>Data type mismatch in
        /// criteria expression</i>. With two compatible predicates it silently returned
        /// zero rows where the correct answer was six — the same builder returning the
        /// right answer on SQLite. Emitting the subquery first makes the text order and
        /// the consumption order agree, and both engines then return the same rows.
        /// <para/>
        /// Predicates combine with AND, so reordering them cannot change the result;
        /// queries without a subquery keep their original order and their original SQL.
        /// <para/>
        /// Verified for a single subquery. Where two or more carry parameters, the
        /// relative order among them is authoring order and has not been measured.
        /// </summary>
        private List<string> OrderedConditions()
        {
            if (_subQueryConditions.Count == 0)
                return _conditions;

            List<string> ordered = new List<string>(_conditions.Count);

            for (int index = 0; index < _conditions.Count; index++)
                if (_subQueryConditions.Contains(index))
                    ordered.Add(_conditions[index]);

            for (int index = 0; index < _conditions.Count; index++)
                if (!_subQueryConditions.Contains(index))
                    ordered.Add(_conditions[index]);

            return ordered;
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

        /// <summary>
        /// Adds trusted GROUP BY fragments to the active SELECT statement.
        /// </summary>
        public QueryBuilder GroupBy(params string[] Columns)
        {
            RequireQueryType(QueryType.Select, nameof(GroupBy));
            if (Columns != null && Columns.Length > 0)
                _groupByColumns.AddRange(Columns);
            return this;
        }

        /// <summary>
        /// Adds trusted ORDER BY fragments to the active SELECT statement.
        /// </summary>
        public QueryBuilder OrderBy(params string[] Columns)
        {
            RequireQueryType(QueryType.Select, nameof(OrderBy));
            if (Columns != null && Columns.Length > 0)
                _orderByColumns.AddRange(Columns);
            return this;
        }

        #endregion

        #region INSERT / UPDATE

        /// <summary>
        /// Starts an INSERT statement with trusted table and column names and
        /// parameterized values.
        /// </summary>
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

        /// <summary>
        /// Starts an UPDATE statement with trusted table and column names and
        /// parameterized values.
        /// </summary>
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

            return new QueryModel(
                sql,
                parameters,
                _top,
                new DbCommandPolicy { TimeoutSeconds = _commandTimeoutSeconds });
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
                sb.Append(" WHERE ").Append(string.Join(" AND ", OrderedConditions()));

            if (_groupByColumns.Count > 0)
                sb.Append(" GROUP BY ").Append(string.Join(", ", _groupByColumns));

            if (_orderByColumns.Count > 0)
                sb.Append(" ORDER BY ").Append(string.Join(", ", _orderByColumns));

            // The provider translator applies TOP/LIMIT.
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
                sb.Append(" WHERE ").Append(string.Join(" AND ", OrderedConditions()));

            return sb.ToString();
        }

        #endregion
    }

}

