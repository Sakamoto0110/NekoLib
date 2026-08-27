using System;
using System.Collections.Generic;
using System.Reflection;
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
                .JoinOn("Orders", "Orders.CustomerId", "Customers.Id")
                .Where("Customers.Active", QueryOperator.Equal, true)
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
                .Where("Active", QueryOperator.Equal, true)
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
                .Where("Active", QueryOperator.Equal, true)
                .Update("Customers")
                .Set("Active", false);

            Assert.Empty(builder.Parameters);
            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Fact]
        public void Select_AfterUpdate_DoesNotRetainUpdateState()
        {
            QueryModel model = new QueryBuilder()
                .Update("Customers")
                .Set("Active", false)
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
            QueryBuilder builder = new QueryBuilder()
                .InsertInto("Customers")
                .Value("Id", 1);

            Assert.Throws<InvalidOperationException>(() =>
                builder.Where("Id", QueryOperator.Equal, 1));
        }

        [Fact]
        public void WhereTrusted_MissingPlaceholderValue_ThrowsBeforeMutatingParameters()
        {
            QueryBuilder builder = new QueryBuilder().Select().From("Customers");

            Assert.Throws<ArgumentException>(() =>
                builder.WhereTrusted("Id = @p1 AND Region = @p2", 7));
            Assert.Empty(builder.Parameters);
        }

        [Fact]
        public void WhereTrusted_UnusedValue_ThrowsBeforeMutatingParameters()
        {
            QueryBuilder builder = new QueryBuilder().Select().From("Customers");

            Assert.Throws<ArgumentException>(() =>
                builder.WhereTrusted("Id = @p1", 7, "north"));
            Assert.Empty(builder.Parameters);
        }

        [Fact]
        public void WhereTrusted_PlaceholderGap_ThrowsArgumentException()
        {
            QueryBuilder builder = new QueryBuilder().Select().From("Customers");

            Assert.Throws<ArgumentException>(() =>
                builder.WhereTrusted("Id = @p2", 7));
        }

        [Theory]
        [InlineData("Id = @p0")]
        [InlineData("Id = @p01")]
        public void WhereTrusted_NonCanonicalPlaceholder_ThrowsArgumentException(string condition)
        {
            QueryBuilder builder = new QueryBuilder().Select().From("Customers");

            Assert.Throws<ArgumentException>(() => builder.WhereTrusted(condition, 7));
        }

        [Fact]
        public void WhereTrusted_RepeatedPlaceholder_ReusesOneGeneratedParameter()
        {
            QueryModel model = new QueryBuilder()
                .Select()
                .From("Customers")
                .WhereTrusted("PrimaryId = @p1 OR BackupId = @p1", 7)
                .Build();

            Assert.Equal(
                "SELECT * FROM Customers WHERE PrimaryId = @p1 OR BackupId = @p1",
                model.Sql);
            Assert.Single(model.Parameters);
            Assert.Equal(7, model.Parameters["@p1"]);
        }

        [Fact]
        public void WhereTrusted_QuotedAndCommentedPlaceholderText_IsNotTokenized()
        {
            QueryModel model = new QueryBuilder()
                .Select()
                .From("Customers")
                .WhereTrusted(
                    "Id = @p1 AND Note = '@p2' /* @p3 */ -- @p4\r\nAND Active = 1",
                    7)
                .Build();

            Assert.Contains("Note = '@p2'", model.Sql);
            Assert.Contains("/* @p3 */", model.Sql);
            Assert.Contains("-- @p4", model.Sql);
            Assert.Single(model.Parameters);
        }

        [Fact]
        public void WhereTrusted_PlaceholderPrefixCollision_IsTreatedAsTrustedRawText()
        {
            QueryModel model = new QueryBuilder()
                .Select()
                .From("Customers")
                .WhereTrusted("ProviderVariable = @p1suffix")
                .Build();

            Assert.Contains("@p1suffix", model.Sql);
            Assert.Empty(model.Parameters);
        }

        [Fact]
        public void WhereTrusted_ValuesWithoutCondition_ThrowsArgumentException()
        {
            QueryBuilder builder = new QueryBuilder().Select().From("Customers");

            Assert.Throws<ArgumentException>(() => builder.WhereTrusted(" ", 7));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Subquery_WithTop_ThrowsBeforeMutatingParent(bool negated)
        {
            QueryBuilder subquery = new QueryBuilder()
                .Select("Id")
                .From("Orders")
                .Top(1);
            QueryBuilder parent = new QueryBuilder()
                .Select()
                .From("Customers")
                .Where("Active", QueryOperator.Equal, true);

            NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            {
                if (negated)
                    parent.WhereNotExists(subquery);
                else
                    parent.WhereExists(subquery);
            });

            Assert.Contains("nested query models", exception.Message);
            Assert.Single(parent.Parameters);
            Assert.Equal(true, parent.Parameters["@p1"]);
            Assert.DoesNotContain("EXISTS", parent.Build().Sql);
        }

        [Fact]
        public void Build_TrustedSqlFragmentsAndParameterizedValue_PreserveSeparateBoundaries()
        {
            QueryModel model = new QueryBuilder()
                .Select("Customers.DisplayName AS UnsafeAlias")
                .From("Customers /* trusted table fragment */")
                .JoinTrusted(
                    "Orders /* trusted join fragment */",
                    "Orders.CustomerId = Customers.Id",
                    QueryJoinType.Left)
                .WhereTrusted("Customers.Region = @p1 /* trusted condition fragment */", "north")
                .GroupBy("Customers.DisplayName /* trusted grouping fragment */")
                .OrderBy("UnsafeAlias DESC /* trusted ordering fragment */")
                .Build();

            Assert.Contains("UnsafeAlias", model.Sql);
            Assert.Contains("trusted table fragment", model.Sql);
            Assert.Contains("trusted join fragment", model.Sql);
            Assert.Contains("trusted condition fragment", model.Sql);
            Assert.Contains("trusted grouping fragment", model.Sql);
            Assert.Contains("trusted ordering fragment", model.Sql);
            Assert.DoesNotContain("north", model.Sql);
            Assert.Equal("north", model.Parameters["@p1"]);
        }

        [Fact]
        public void InsertInto_Value_EmitsStructuredInsert()
        {
            QueryModel model = new QueryBuilder()
                .InsertInto("Inventory")
                .Value("Name", "Neko")
                .Value("Quantity", 54)
                .Build();

            Assert.Equal(
                "INSERT INTO Inventory (Name, Quantity) VALUES (@p1, @p2)",
                model.Sql);
            Assert.Equal("Neko", model.Parameters["@p1"]);
            Assert.Equal(54, model.Parameters["@p2"]);
        }

        [Fact]
        public void Update_Set_EmitsStructuredUpdateWithoutChangingPredicateOwnership()
        {
            QueryModel model = new QueryBuilder()
                .Update("Inventory")
                .Set("Quantity", 54)
                .Where("Id", QueryOperator.Equal, 7)
                .Build();

            Assert.Equal(
                "UPDATE Inventory SET Quantity = @p2 WHERE Id = @p1",
                model.Sql);
            Assert.Equal(7, model.Parameters["@p1"]);
            Assert.Equal(54, model.Parameters["@p2"]);
        }

        [Theory]
        [InlineData(QueryOperator.Equal, "=")]
        [InlineData(QueryOperator.NotEqual, "<>")]
        [InlineData(QueryOperator.GreaterThan, ">")]
        [InlineData(QueryOperator.GreaterThanOrEqual, ">=")]
        [InlineData(QueryOperator.LessThan, "<")]
        [InlineData(QueryOperator.LessThanOrEqual, "<=")]
        public void Where_Operator_EmitsStructuredParameterizedPredicate(
            QueryOperator queryOperator,
            string expectedSqlOperator)
        {
            QueryModel model = new QueryBuilder()
                .Select()
                .From("Inventory")
                .Where("Quantity", queryOperator, 54)
                .Build();

            Assert.Equal(
                "SELECT * FROM Inventory WHERE Quantity " + expectedSqlOperator + " @p1",
                model.Sql);
            Assert.Equal(54, model.Parameters["@p1"]);
        }

        [Theory]
        [InlineData(QueryOperator.Equal, "IS NULL")]
        [InlineData(QueryOperator.NotEqual, "IS NOT NULL")]
        public void Where_EqualityWithNull_EmitsNullPredicateWithoutParameter(
            QueryOperator queryOperator,
            string expectedPredicate)
        {
            QueryModel model = new QueryBuilder()
                .Select()
                .From("Inventory")
                .Where("ArchivedAt", queryOperator, null)
                .Build();

            Assert.Equal(
                "SELECT * FROM Inventory WHERE ArchivedAt " + expectedPredicate,
                model.Sql);
            Assert.Empty(model.Parameters);
        }

        [Fact]
        public void Where_NonEqualityWithNull_ThrowsBeforeMutatingParameters()
        {
            QueryBuilder builder = new QueryBuilder().Select().From("Inventory");

            Assert.Throws<ArgumentException>(() =>
                builder.Where("Quantity", QueryOperator.GreaterThan, null));

            Assert.Empty(builder.Parameters);
        }

        [Fact]
        public void Where_UnknownOperator_ThrowsBeforeMutatingParameters()
        {
            QueryBuilder builder = new QueryBuilder().Select().From("Inventory");

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                builder.Where("Quantity", (QueryOperator)999, 54));

            Assert.Empty(builder.Parameters);
        }

        [Fact]
        public void Where_UnknownOperatorWithNull_ThrowsBeforeMutatingParameters()
        {
            QueryBuilder builder = new QueryBuilder().Select().From("Inventory");

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                builder.Where("Quantity", (QueryOperator)999, null));

            Assert.Empty(builder.Parameters);
        }

        [Fact]
        public void JoinOn_ColumnsAndOperator_EmitStructuredJoin()
        {
            QueryModel model = new QueryBuilder()
                .Select("Inventory.Id")
                .From("Inventory")
                .JoinOn(
                    "Thresholds",
                    "Inventory.Quantity",
                    QueryOperator.GreaterThanOrEqual,
                    "Thresholds.Minimum",
                    QueryJoinType.Left)
                .Build();

            Assert.Equal(
                "SELECT Inventory.Id FROM Inventory LEFT JOIN Thresholds ON Inventory.Quantity >= Thresholds.Minimum",
                model.Sql);
            Assert.Empty(model.Parameters);
        }

        [Fact]
        public void CompatibilityOverloads_DelegateToCanonicalBehavior()
        {
#pragma warning disable CS0618
            QueryModel insert = new QueryBuilder()
                .InsertInto(
                    "Inventory",
                    new Dictionary<string, object> { { "Quantity", 54 } })
                .Build();
            QueryModel update = new QueryBuilder()
                .Update(
                    "Inventory",
                    new Dictionary<string, object> { { "Quantity", 55 } })
                .Where("Id = @p1", 7)
                .Build();
            QueryModel select = new QueryBuilder()
                .Select()
                .From("Inventory")
                .Join("Warehouses", "Warehouses.Id = Inventory.WarehouseId")
                .Build();
#pragma warning restore CS0618

            Assert.Equal(
                "INSERT INTO Inventory (Quantity) VALUES (@p1)",
                insert.Sql);
            Assert.Equal(
                "UPDATE Inventory SET Quantity = @p2 WHERE Id = @p1",
                update.Sql);
            Assert.Equal(
                "SELECT * FROM Inventory INNER JOIN Warehouses ON Warehouses.Id = Inventory.WarehouseId",
                select.Sql);
        }

        [Fact]
        public void CompatibilityOverloads_AreWarningOnlyAndNameConcreteReplacements()
        {
            AssertWarningOnlyObsolete(
                "InsertInto",
                new[] { typeof(string), typeof(Dictionary<string, object>) },
                "InsertInto(string).Value(string, object)");
            AssertWarningOnlyObsolete(
                "Update",
                new[] { typeof(string), typeof(Dictionary<string, object>) },
                "Update(string).Set(string, object)");
            AssertWarningOnlyObsolete(
                "Where",
                new[] { typeof(string), typeof(object[]) },
                "WhereTrusted(string, params object[])");
            AssertWarningOnlyObsolete(
                "Join",
                new[] { typeof(string), typeof(string), typeof(string) },
                "JoinOn(string, string, string, QueryJoinType)");
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
            QueryBuilder builder = new QueryBuilder()
                .Update("Customers")
                .Set("Active", false);

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() => builder.Build());

            Assert.Contains("AllowAllRowsUpdate", exception.Message);
        }

        [Fact]
        public void Build_UpdateWithAllRowsOptIn_EmitsUpdateWithoutWhereClause()
        {
            QueryModel model = new QueryBuilder()
                .Update("Customers")
                .Set("Active", false)
                .AllowAllRowsUpdate()
                .Build();

            Assert.DoesNotContain(" WHERE ", model.Sql);
            Assert.Single(model.Parameters);
        }

        [Fact]
        public void Build_DeleteWithPredicate_EmitsParameterizedDelete()
        {
            QueryModel model = new QueryBuilder()
                .DeleteFrom("Customers")
                .Where("Id", QueryOperator.Equal, 7)
                .Build();

            Assert.Equal("DELETE FROM Customers WHERE Id = @p1", model.Sql);
            Assert.Equal(7, model.Parameters["@p1"]);
        }

        [Fact]
        public void Build_DeleteWithoutPredicate_DefaultsToFailClosed()
        {
            QueryBuilder builder = new QueryBuilder().DeleteFrom("Customers");

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() => builder.Build());

            Assert.Contains("AllowAllRowsDelete", exception.Message);
        }

        [Fact]
        public void Build_DeleteWithAllRowsOptIn_EmitsDeleteWithoutWhereClause()
        {
            QueryModel model = new QueryBuilder()
                .DeleteFrom("Customers")
                .AllowAllRowsDelete()
                .Build();

            Assert.Equal("DELETE FROM Customers", model.Sql);
            Assert.Empty(model.Parameters);
        }

        [Fact]
        public void AllowAllRowsDelete_WithoutDelete_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() =>
                new QueryBuilder().AllowAllRowsDelete());
        }

        [Fact]
        public void DeleteFrom_AfterAllRowsOptIn_ClearsAuthorization()
        {
            QueryBuilder builder = new QueryBuilder()
                .DeleteFrom("Customers")
                .AllowAllRowsDelete()
                .DeleteFrom("Archive");

            Assert.Throws<InvalidOperationException>(() => builder.Build());
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
                .Where("CustomerId", QueryOperator.Equal, 42);

            QueryBuilder parent = new QueryBuilder()
                .Select("*")
                .From("Customers")
                .Where("Active", QueryOperator.Equal, true)
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
                .Where("CustomerId", QueryOperator.Equal, 7);

            QueryBuilder parent = new QueryBuilder()
                .Select("*")
                .From("Customers")
                .Where("Active", QueryOperator.Equal, true)
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
                .Where("Reason", QueryOperator.Equal, "fraud");

            QueryBuilder parent = new QueryBuilder()
                .Select("*")
                .From("Customers")
                .Where("Region", QueryOperator.Equal, "NA")
                .WhereNotExists(sub);

            QueryModel model = parent.Build();

            Assert.Equal("NA", model.Parameters["@p1"]);
            Assert.Contains(model.Parameters, kv =>
                kv.Key != "@p1" && (kv.Value as string) == "fraud");
            Assert.Contains("NOT EXISTS", model.Sql);
        }

        [Fact]
        public void WhereExists_AfterOrdinaryPredicate_EmitsSubqueryPredicateFirst()
        {
            QueryBuilder sub = new QueryBuilder()
                .Select("1")
                .From("Animals")
                .Where("Species", QueryOperator.Equal, "cow");

            QueryModel model = new QueryBuilder()
                .Select("Name")
                .From("Products")
                .Where("Quantity", QueryOperator.GreaterThan, 10)
                .WhereExists(sub)
                .Build();

            int existsIndex = model.Sql.IndexOf("EXISTS", StringComparison.Ordinal);
            int quantityIndex = model.Sql.IndexOf("Quantity", StringComparison.Ordinal);

            Assert.True(existsIndex >= 0, "EXISTS clause should be present");
            Assert.True(quantityIndex >= 0, "ordinary predicate should be present");
            Assert.True(existsIndex < quantityIndex,
                "subquery predicates must precede ordinary predicates for positional providers");
            Assert.Equal(2, model.Parameters.Count);
            Assert.Equal(10, model.Parameters["@p1"]);
            Assert.Equal("cow", model.Parameters["@p2"]);
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
                .InsertInto("Customers")
                .Value("Id", 1)
                .Value("Name", "Alice");

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
                .Update("Customers")
                .Set("Name", "Bob")
                .Set("Active", true)
                .Where("Id", QueryOperator.Equal, 99);

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
        public void Build_Delete_IsIdempotent_AcrossMultipleCalls()
        {
            QueryBuilder builder = new QueryBuilder()
                .DeleteFrom("Customers")
                .Where("Id", QueryOperator.Equal, 7);

            QueryModel first = builder.Build();
            QueryModel second = builder.Build();

            Assert.Equal(first.Sql, second.Sql);
            Assert.Equal(first.Parameters.Count, second.Parameters.Count);
            Assert.Equal(first.Parameters["@p1"], second.Parameters["@p1"]);
        }

        [Fact]
        public void Build_Insert_DoesNotMutateBuilderParameters()
        {
            QueryBuilder qb = new QueryBuilder()
                .InsertInto("T")
                .Value("X", 1);

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
                .Update("T")
                .Set("X", 1)
                .Where("Y", QueryOperator.Equal, 2);

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
                .Where("Active", QueryOperator.Equal, true);

            QueryModel first = qb.Build();
            QueryModel second = qb.Build();

            Assert.Equal(first.Sql, second.Sql);
            Assert.Equal(first.Parameters.Count, second.Parameters.Count);
        }

        private static void AssertWarningOnlyObsolete(
            string methodName,
            Type[] parameterTypes,
            string expectedReplacement)
        {
            MethodInfo method = typeof(QueryBuilder).GetMethod(methodName, parameterTypes);
            Assert.NotNull(method);

            ObsoleteAttribute attribute = method.GetCustomAttribute<ObsoleteAttribute>();
            Assert.NotNull(attribute);
            Assert.False(attribute.IsError);
            Assert.Contains(expectedReplacement, attribute.Message);
        }
    }
}
