using System.Collections.Generic;
using NekoLib.Data.Query;

internal static class Program
{
    private static void Main()
    {
        _ = typeof(QueryBuilder);

#if LEGACY_INSERT
        _ = new QueryBuilder().InsertInto("Rows", new Dictionary<string, object?> { { "Id", 1 } });
#elif LEGACY_UPDATE
        _ = new QueryBuilder().Update("Rows", new Dictionary<string, object?> { { "Id", 1 } });
#elif LEGACY_WHERE
        _ = new QueryBuilder().Select().From("Rows").Where("Id = @p1", 1);
#elif LEGACY_JOIN
        _ = new QueryBuilder().Select().From("Rows").Join("OtherRows", "OtherRows.Id = Rows.Id");
#endif
    }
}
