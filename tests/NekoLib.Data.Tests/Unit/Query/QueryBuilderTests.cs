using System.Collections.Generic;
using NekoLib.Data.Query;
using Xunit;

namespace NekoLib.Data.Tests.Unit.Query
{
    /// <summary>
    /// Regression locks for QueryBuilder behavior previously flagged in DataAudit.md
    /// (findings #5 subquery parameter collision and #6 Build idempotency for
    /// INSERT/UPDATE). These tests assert the current, intended behavior.
    /// </summary>
    public class QueryBuilderTests
    {
        // ---------------------------------------------------------------------
        // Finding #5 - WhereExists / WhereNotExists must rename subquery params
        // so they cannot collide with parent params that share the same name.
        // ---------------------------------------------------------------------

        [Fact]
        public void WhereExists_SubqueryParameter_DoesNotOverwriteParentParameter()
        {
            QueryBuilder sub = new QueryBuilder()
                .Select("1")
                .From("Orders")
                .Where("CustomerId = @p1", 42);

            QueryBuilder parent = new QueryBuilder()
                .Select("*")
                .From("Customers")
                .Where("Active = @p1", true)
                .WhereExists(sub);

            QueryModel model = parent.Build();

            // Parent kept its own parameter intact.
            Assert.True(model.Parameters.ContainsKey("@p1"));
            Assert.Equal(true, model.Parameters["@p1"]);

            // Subquery contributed a renamed parameter with its own value.
            Assert.Contains(model.Parameters, kv =>
                kv.Key != "@p1" && kv.Value is int i && i == 42);

            // Parent and subquery values are both present (no overwrite).
            Assert.Equal(2, model.Parameters.Count);
        }

        [Fact]
        public void WhereExists_RenamesSubqueryPlaceholdersInsideEmittedSql()
        {
            QueryBuilder sub = new QueryBuilder()
                .Select("1")
                .From("Orders")
                .Where("CustomerId = @p1", 7);

            QueryBuilder parent = new QueryBuilder()
                .Select("*")
                .From("Customers")
                .Where("Active = @p1", true)
                .WhereExists(sub);

            QueryModel model = parent.Build();

            // The literal "@p1" must still appear (parent uses it). The subquery
            // occurrence must have been rewritten to a different name.
            Assert.Contains("@p1", model.Sql);
            // EXISTS clause must not reference @p1 anymore - the subquery's
            // placeholder was renamed when copied into the parent.
            int existsIndex = model.Sql.IndexOf("EXISTS");
            Assert.True(existsIndex >= 0, "EXISTS clause should be present");
            string existsClause = model.Sql.Substring(existsIndex);
            Assert.DoesNotContain("@p1)", existsClause);
            Assert.DoesNotContain("@p1 ", existsClause);
        }

        [Fact]
        public void WhereNotExists_AlsoRenamesSubqueryParameters()
        {
            QueryBuilder sub = new QueryBuilder()
                .Select("1")
                .From("Returns")
                .Where("Reason = @p1", "fraud");

            QueryBuilder parent = new QueryBuilder()
                .Select("*")
                .From("Customers")
                .Where("Region = @p1", "NA")
                .WhereNotExists(sub);

            QueryModel model = parent.Build();

            Assert.Equal("NA", model.Parameters["@p1"]);
            Assert.Contains(model.Parameters, kv =>
                kv.Key != "@p1" && (kv.Value as string) == "fraud");
            Assert.Contains("NOT EXISTS", model.Sql);
        }

        // ---------------------------------------------------------------------
        // Finding #6 - Build() must be idempotent for INSERT/UPDATE. The
        // previous implementation appended parameters to internal state on
        // every Build() call, breaking reuse and re-translation.
        // ---------------------------------------------------------------------

        [Fact]
        public void Build_Insert_IsIdempotent_AcrossMultipleCalls()
        {
            QueryBuilder qb = new QueryBuilder()
                .InsertInto("Customers", new Dictionary<string, object>
                {
                    { "Id", 1 },
                    { "Name", "Alice" }
                });

            QueryModel first = qb.Build();
            QueryModel second = qb.Build();

            Assert.Equal(first.Sql, second.Sql);
            Assert.Equal(first.Parameters.Count, second.Parameters.Count);
            foreach (KeyValuePair<string, object> kv in first.Parameters)
            {
                Assert.True(second.Parameters.ContainsKey(kv.Key));
                Assert.Equal(kv.Value, second.Parameters[kv.Key]);
            }
        }

        [Fact]
        public void Build_Update_IsIdempotent_AcrossMultipleCalls()
        {
            QueryBuilder qb = new QueryBuilder()
                .Update("Customers", new Dictionary<string, object>
                {
                    { "Name", "Bob" },
                    { "Active", true }
                })
                .Where("Id = @p1", 99);

            QueryModel first = qb.Build();
            QueryModel second = qb.Build();

            Assert.Equal(first.Sql, second.Sql);
            Assert.Equal(first.Parameters.Count, second.Parameters.Count);
            foreach (KeyValuePair<string, object> kv in first.Parameters)
            {
                Assert.True(second.Parameters.ContainsKey(kv.Key));
                Assert.Equal(kv.Value, second.Parameters[kv.Key]);
            }
        }

        [Fact]
        public void Build_Insert_DoesNotMutateBuilderParameters()
        {
            QueryBuilder qb = new QueryBuilder()
                .InsertInto("T", new Dictionary<string, object> { { "X", 1 } });

            int countBefore = qb.Parameters.Count;
            qb.Build();
            qb.Build();
            qb.Build();
            int countAfter = qb.Parameters.Count;

            // Internal state must not accumulate per-build parameters.
            Assert.Equal(countBefore, countAfter);
        }

        [Fact]
        public void Build_Update_DoesNotMutateBuilderParameters()
        {
            QueryBuilder qb = new QueryBuilder()
                .Update("T", new Dictionary<string, object> { { "X", 1 } })
                .Where("Y = @p1", 2);

            int countBefore = qb.Parameters.Count;
            qb.Build();
            qb.Build();
            qb.Build();
            int countAfter = qb.Parameters.Count;

            Assert.Equal(countBefore, countAfter);
        }

        [Fact]
        public void Build_Select_IsIdempotent_AcrossMultipleCalls()
        {
            QueryBuilder qb = new QueryBuilder()
                .Select("Id", "Name")
                .From("Customers")
                .Where("Active = @p1", true);

            QueryModel first = qb.Build();
            QueryModel second = qb.Build();

            Assert.Equal(first.Sql, second.Sql);
            Assert.Equal(first.Parameters.Count, second.Parameters.Count);
        }
    }
}
