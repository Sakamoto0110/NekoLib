using System;
using System.Collections.Generic;
using NekoLib.Data.Query;
using Xunit;

namespace NekoLib.Data.Tests.Unit.Query
{
    /// <summary>
    /// Translator output is pure text manipulation - covered by string assertions.
    /// Finding #20 in docs/audit/data-first-pass.md flagged Access TOP DISTINCT ordering; these
    /// tests lock the expected provider syntax for each translator.
    /// </summary>
    public class TranslatorTests
    {
        // ---------------------------------------------------------------------
        // SQL Server: TOP is parenthesized, sits after DISTINCT when present.
        // ---------------------------------------------------------------------

        [Fact]
        public void SqlServer_Top_AppliesParenthesizedTop()
        {
            QueryModel model = new QueryModel(
                "SELECT Id, Name FROM Customers",
                new Dictionary<string, object>(),
                Top: 5);

            DatabaseQuery dq = new SqlServerQueryTranslator().Translate(model);

            Assert.Equal("SELECT TOP (5) Id, Name FROM Customers", dq.Sql);
        }

        [Fact]
        public void SqlServer_DistinctTop_PlacesTopAfterDistinct()
        {
            QueryModel model = new QueryModel(
                "SELECT DISTINCT Region FROM Customers",
                new Dictionary<string, object>(),
                Top: 10);

            DatabaseQuery dq = new SqlServerQueryTranslator().Translate(model);

            Assert.Equal("SELECT DISTINCT TOP (10) Region FROM Customers", dq.Sql);
        }

        [Fact]
        public void SqlServer_NoTop_PassesThroughSql()
        {
            QueryModel model = new QueryModel(
                "SELECT * FROM Customers",
                new Dictionary<string, object>());

            DatabaseQuery dq = new SqlServerQueryTranslator().Translate(model);

            Assert.Equal("SELECT * FROM Customers", dq.Sql);
        }

        // ---------------------------------------------------------------------
        // Access (OleDb): TOP is unparenthesized; ordering is
        // SELECT DISTINCT TOP n ... (finding #20).
        // ---------------------------------------------------------------------

        [Fact]
        public void Access_Top_AppliesUnparenthesizedTop()
        {
            QueryModel model = new QueryModel(
                "SELECT Id FROM Customers",
                new Dictionary<string, object>(),
                Top: 3);

            DatabaseQuery dq = new AccessQueryTranslator().Translate(model);

            Assert.Equal("SELECT TOP 3 Id FROM Customers", dq.Sql);
        }

        [Fact]
        public void Access_DistinctTop_EmitsSelectDistinctTopN()
        {
            QueryModel model = new QueryModel(
                "SELECT DISTINCT Region FROM Customers",
                new Dictionary<string, object>(),
                Top: 7);

            DatabaseQuery dq = new AccessQueryTranslator().Translate(model);

            // Access SQL accepts "SELECT DISTINCT TOP n ..." - DISTINCT must
            // come before TOP, mirroring SQL Server's ordering but without
            // parentheses around the limit.
            Assert.Equal("SELECT DISTINCT TOP 7 Region FROM Customers", dq.Sql);
        }

        [Fact]
        public void Access_NoTop_PassesThroughSql()
        {
            QueryModel model = new QueryModel(
                "SELECT * FROM Customers",
                new Dictionary<string, object>());

            DatabaseQuery dq = new AccessQueryTranslator().Translate(model);

            Assert.Equal("SELECT * FROM Customers", dq.Sql);
        }

        [Fact]
        public void Access_DistinctCount_RewritesAsAliasedDistinctSubquery()
        {
            QueryModel model = new QueryBuilder()
                .DistinctCount("[Category]")
                .From("[Products]")
                .Build();

            DatabaseQuery dq = new AccessQueryTranslator().Translate(model);

            Assert.Equal(
                "SELECT COUNT(*) FROM (SELECT DISTINCT [Category] FROM [Products]) AS [d]",
                dq.Sql);
        }

        [Fact]
        public void Access_RegularCount_LeavesSqlUnchanged()
        {
            QueryModel model = new QueryBuilder()
                .Count("[Category]")
                .From("[Products]")
                .Build();

            DatabaseQuery dq = new AccessQueryTranslator().Translate(model);

            Assert.Equal("SELECT COUNT([Category]) FROM [Products]", dq.Sql);
        }

        // ---------------------------------------------------------------------
        // SQLite: TOP is converted to a trailing LIMIT clause.
        // ---------------------------------------------------------------------

        [Fact]
        public void Sqlite_Top_AppendsLimitClause()
        {
            QueryModel model = new QueryModel(
                "SELECT Id, Name FROM Customers",
                new Dictionary<string, object>(),
                Top: 25);

            DatabaseQuery dq = new SqliteQueryTranslator().Translate(model);

            Assert.Equal("SELECT Id, Name FROM Customers LIMIT 25", dq.Sql);
        }

        [Fact]
        public void Sqlite_DistinctTop_AppendsLimitWithoutTouchingDistinct()
        {
            QueryModel model = new QueryModel(
                "SELECT DISTINCT Region FROM Customers",
                new Dictionary<string, object>(),
                Top: 4);

            DatabaseQuery dq = new SqliteQueryTranslator().Translate(model);

            Assert.Equal("SELECT DISTINCT Region FROM Customers LIMIT 4", dq.Sql);
        }

        [Fact]
        public void Sqlite_NoTop_PassesThroughSql()
        {
            QueryModel model = new QueryModel(
                "SELECT * FROM Customers",
                new Dictionary<string, object>());

            DatabaseQuery dq = new SqliteQueryTranslator().Translate(model);

            Assert.Equal("SELECT * FROM Customers", dq.Sql);
        }

        // ---------------------------------------------------------------------
        // Parameter passthrough - translators must not lose or rename params.
        // ---------------------------------------------------------------------

        [Theory]
        [InlineData("sqlserver")]
        [InlineData("access")]
        [InlineData("sqlite")]
        public void Translators_CopyParametersToOutput(string provider)
        {
            QueryModel model = new QueryModel(
                "SELECT * FROM T WHERE A = @p1 AND B = @p2",
                new Dictionary<string, object>
                {
                    { "@p1", 1 },
                    { "@p2", "x" }
                });

            IDbQueryTranslator translator = provider switch
            {
                "sqlserver" => new SqlServerQueryTranslator(),
                "access" => new AccessQueryTranslator(),
                "sqlite" => new SqliteQueryTranslator(),
                _ => throw new ArgumentOutOfRangeException(nameof(provider))
            };

            DatabaseQuery dq = translator.Translate(model);

            Assert.Equal(2, dq.Parameters.Count);
            Assert.Equal(1, dq.Parameters["@p1"]);
            Assert.Equal("x", dq.Parameters["@p2"]);
        }

        // ---------------------------------------------------------------------
        // Null guards.
        // ---------------------------------------------------------------------

        [Theory]
        [InlineData("sqlserver")]
        [InlineData("access")]
        [InlineData("sqlite")]
        public void Translators_NullModel_Throws(string provider)
        {
            IDbQueryTranslator translator = provider switch
            {
                "sqlserver" => new SqlServerQueryTranslator(),
                "access" => new AccessQueryTranslator(),
                "sqlite" => new SqliteQueryTranslator(),
                _ => throw new ArgumentOutOfRangeException(nameof(provider))
            };

            Assert.Throws<ArgumentNullException>(() => translator.Translate(null));
        }
    }
}
