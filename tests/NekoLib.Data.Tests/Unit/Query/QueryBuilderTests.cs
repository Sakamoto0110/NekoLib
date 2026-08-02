using System;
using System.Collections.Generic;
using NekoLib.Data.Query;
using Xunit;

namespace NekoLib.Data.Tests.Unit.Query
{
    /// <summary>
    /// Regression locks for QueryBuilder behavior previously flagged in
    /// docs/audit/data-first-pass.md
    /// (findings #5 subquery parameter collision and #6 Build idempotency for
    /// INSERT/UPDATE). These tests assert the current, intended behavior.
    /// </summary>
    public class QueryBuilderTests
    {
        [Fact]
        public void WhereIn_EmptyCollection_EmitsConstantFalsePredicate()
        {
            QueryModel model = new QueryBuilder()
                .Select()
                .From("Customers")
                .WhereIn("Id", Array.Empty<object>())
                .Build();

            Assert.Equal("SELECT * FROM Customers WHERE 1 = 0", model.Sql);
            Assert.Empty(model.Parameters);
        }

        [Fact]
        public void WhereNotIn_EmptyCollection_EmitsConstantTruePredicate()
        {
            QueryModel model = new QueryBuilder()
                .Select()
                .From("Customers")
                .WhereNotIn("Id", Array.Empty<object>())
                .Build();

            Assert.Equal("SELECT * FROM Customers WHERE 1 = 1", model.Sql);
            Assert.Empty(model.Parameters);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CollectionPredicate_NullValues_ThrowsArgumentNullException(bool negated)
        {
            QueryBuilder builder = new QueryBuilder().Select().From("Customers");

            Assert.Throws<ArgumentNullException>(() =>
            {
                if (negated)
                    builder.WhereNotIn("Id", null);
                else
                    builder.WhereIn("Id", null);
            });
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void WhereIn_MissingColumn_ThrowsArgumentException(string column)
        {
            QueryBuilder builder = new QueryBuilder().Select().From("Customers");

            Assert.ThrowsAny<ArgumentException>(() =>
                builder.WhereIn(column, new object[] { 1 }));
        }

        [Fact]
        public void WhereIn_SingleValue_UsesOneParameter()
        {
            QueryModel model = new QueryBuilder()
                .Select()
                .From("Customers")
                .WhereIn("Id", new object[] { 7 })
                .Build();

            Assert.Equal("SELECT * FROM Customers WHERE Id IN (@p1)", model.Sql);
            Assert.Equal(7, model.Parameters["@p1"]);
        }

        [Fact]
        public void WhereNotIn_MultipleValues_PreservesValueOrder()
        {
            QueryModel model = new QueryBuilder()
                .Select()
                .From("Customers")
                .WhereNotIn("Id", new object[] { 7, 11 })
                .Build();

            Assert.Equal("SELECT * FROM Customers WHERE Id NOT IN (@p1, @p2)", model.Sql);
            Assert.Equal(7, model.Parameters["@p1"]);
            Assert.Equal(11, model.Parameters["@p2"]);
        }

        [Fact]
        public void Build_UpdateWithoutPredicate_ThrowsInvalidOperationException()
        {
            QueryBuilder builder = new QueryBuilder().Update(
                "Customers",
                new Dictionary<string, object> { { "Active", false } });

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() => builder.Build());

            Assert.Contains("AllowAllRowsUpdate", exception.Message);
        }

        [Fact]
        public void Build_UpdateWithAllRowsOptIn_EmitsUpdateWithoutWhereClause()
        {
            QueryModel model = new QueryBuilder()
                .Update(
                    "Customers",
                    new Dictionary<string, object> { { "Active", false } })
                .AllowAllRowsUpdate()
                .Build();

            Assert.DoesNotContain(" WHERE ", model.Sql);
            Assert.Single(model.Parameters);
        }

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
