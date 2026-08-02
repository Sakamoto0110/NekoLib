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
        public void Build_UndefinedQueryType_ThrowsInvalidOperationException()
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => new QueryBuilder().Build());

            Assert.Contains("Query type was not defined", exception.Message);
        }

        [Fact]
        public void Select_AfterPreviousSelect_ReplacesEntireStatementState()
        {
            QueryBuilder builder = new QueryBuilder()
                .SelectDistinct("Region")
                .From("Customers")
                .Top(5)
                .Join("Orders", "Orders.CustomerId = Customers.Id")
                .Where("Customers.Active = @p1", true)
                .GroupBy("Region")
                .OrderBy("Region");

            QueryModel model = builder
                .Select("Id")
                .From("Archive")
                .Build();

            Assert.Equal("SELECT Id FROM Archive", model.Sql);
            Assert.Null(model.Top);
            Assert.Empty(model.Parameters);
        }

        [Fact]
        public void Count_AfterSelectDistinct_ReplacesProjectionAndStatementState()
        {
            QueryModel model = new QueryBuilder()
                .SelectDistinct("Region")
                .From("Customers")
                .Where("Active = @p1", true)
                .Count("Id")
                .From("Orders")
                .Build();

            Assert.Equal("SELECT COUNT(Id) FROM Orders", model.Sql);
            Assert.Empty(model.Parameters);
        }

        [Fact]
        public void Update_AfterSelect_DoesNotRetainSelectPredicateOrParameters()
        {
            QueryBuilder builder = new QueryBuilder()
                .Select("Id")
                .From("Customers")
                .Where("Active = @p1", true)
                .Update(
                    "Customers",
                    new Dictionary<string, object> { { "Active", false } });

            Assert.Empty(builder.Parameters);
            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Fact]
        public void Select_AfterUpdate_DoesNotRetainUpdateState()
        {
            QueryModel model = new QueryBuilder()
                .Update(
                    "Customers",
                    new Dictionary<string, object> { { "Active", false } })
                .AllowAllRowsUpdate()
                .Select("Id")
                .From("Archive")
                .Build();

            Assert.Equal("SELECT Id FROM Archive", model.Sql);
            Assert.Empty(model.Parameters);
        }

        [Fact]
        public void Distinct_AfterCount_ThrowsInvalidOperationException()
        {
            QueryBuilder builder = new QueryBuilder().Count();

            Assert.Throws<InvalidOperationException>(() => builder.Distinct());
        }

        [Fact]
        public void Top_WithoutSelect_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() => new QueryBuilder().Top(5));
        }

        [Fact]
        public void Where_DuringInsert_ThrowsInvalidOperationException()
        {
            QueryBuilder builder = new QueryBuilder().InsertInto(
                "Customers",
                new Dictionary<string, object> { { "Id", 1 } });

            Assert.Throws<InvalidOperationException>(() =>
                builder.Where("Id = @p1", 1));
        }

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
