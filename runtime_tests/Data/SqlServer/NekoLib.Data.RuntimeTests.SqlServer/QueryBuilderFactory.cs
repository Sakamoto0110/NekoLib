#nullable enable
using System;
using System.Collections.Generic;
using NekoLib.Data.Query;

namespace NekoLib.Data.RuntimeTests.SqlServer
{
    internal static class QueryBuilderFactory
    {
        public static QueryBuilder InsertInto(
            string table,
            IEnumerable<KeyValuePair<string, object?>> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            QueryBuilder builder = new QueryBuilder().InsertInto(table);
            foreach (KeyValuePair<string, object?> value in values)
                builder.Value(value.Key, value.Value);

            return builder;
        }
    }
}
