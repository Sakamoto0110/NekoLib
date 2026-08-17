using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NekoLib.Data.Dynamic;
using NekoLib.Data.Gateway;
using NekoLib.Data.Query;
using Xunit;

namespace NekoLib.Data.Tests.Unit.Gateway
{
    public class DynamicIlStabilityTests
    {
        [Fact]
        public async Task GetDynamic_IlSchemas_AlignNullsAndKeepProcessTypesWithoutEviction()
        {
            DynamicIlMetrics initial = DatabaseGateway.GetDynamicIlMetrics();
            Assert.Equal(0, initial.EmittedSchemaCount);

            DynamicRow expandoNull = await ReadSingle(
                CreateOptions(DynamicMode.Expando, 2),
                new[] { "Value" },
                new[] { typeof(int) },
                new object[] { DBNull.Value });
            Assert.Null(expandoNull["Value"]);

            DatabaseGatewayOptions ilWithFallback = CreateOptions(
                DynamicMode.IL | DynamicMode.Expando,
                2);
            DynamicRow first = await ReadSingle(
                ilWithFallback,
                new[] { "Value" },
                new[] { typeof(int) },
                new object[] { DBNull.Value });
            object firstInstance = GetBackend(first, "_instance");
            Assert.Null(first["Value"]);
            Assert.Equal(typeof(int?), firstInstance.GetType().GetProperty("Value").PropertyType);

            DynamicRow second = await ReadSingle(
                ilWithFallback,
                new[] { "Name" },
                new[] { typeof(string) },
                new object[] { "Alice" });
            Assert.NotNull(GetBackend(second, "_instance"));

            DatabaseGatewayOptions laterLargerLimit = CreateOptions(
                DynamicMode.IL | DynamicMode.Expando,
                100);
            DynamicRow fallback = await ReadSingle(
                laterLargerLimit,
                new[] { "Enabled" },
                new[] { typeof(bool) },
                new object[] { true });
            Assert.NotNull(GetBackend(fallback, "_dict"));

            DatabaseGatewayOptions failAtLimit = CreateOptions(DynamicMode.IL, 100);
            await Assert.ThrowsAsync<InvalidOperationException>(() => ReadSingle(
                failAtLimit,
                new[] { "Created" },
                new[] { typeof(DateTime) },
                new object[] { DateTime.UtcNow }));

            DynamicRow firstAgain = await ReadSingle(
                laterLargerLimit,
                new[] { "Value" },
                new[] { typeof(int) },
                new object[] { 7 });
            object firstAgainInstance = GetBackend(firstAgain, "_instance");

            Assert.Same(firstInstance.GetType(), firstAgainInstance.GetType());
            Assert.Equal(7, firstAgain["Value"]);

            DynamicIlMetrics final = DatabaseGateway.GetDynamicIlMetrics();
            Assert.Equal(2, final.SchemaLimit);
            Assert.Equal(2, final.EmittedSchemaCount);
            Assert.True(final.CacheHits >= initial.CacheHits + 1);
            Assert.True(final.CacheMisses >= initial.CacheMisses + 4);
            Assert.True(final.LimitRejections >= initial.LimitRejections + 2);
        }

        private static DatabaseGatewayOptions CreateOptions(
            DynamicMode dynamicMode,
            int maxDynamicSchemas)
        {
            return new DatabaseGatewayOptions
            {
                DynamicMode = dynamicMode,
                MaxDynamicSchemas = maxDynamicSchemas,
                AllowExpandoFallback =
                    (dynamicMode & DynamicMode.Expando) == DynamicMode.Expando,
                FailOnDynamicSchemaLimit =
                    (dynamicMode & DynamicMode.Expando) != DynamicMode.Expando
            };
        }

        private static async Task<DynamicRow> ReadSingle(
            DatabaseGatewayOptions options,
            string[] names,
            Type[] types,
            object[] values)
        {
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand
                {
                    Reader = new FakeDataReader(names, types, values)
                });
            using (QueryExecutionContext context = new QueryExecutionContext(
                factory,
                new SqliteQueryTranslator(),
                options))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                List<DynamicRow> rows = await gateway.GetDynamic(
                    new QueryBuilder().Select("*").From("T"));
                return Assert.Single(rows);
            }
        }

        private static object GetBackend(DynamicRow row, string fieldName)
        {
            FieldInfo field = typeof(DynamicRow).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            object value = field.GetValue(row);
            Assert.NotNull(value);
            return value;
        }
    }
}
