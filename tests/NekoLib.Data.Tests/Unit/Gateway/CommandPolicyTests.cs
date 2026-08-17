using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using NekoLib.Data.Gateway;
using NekoLib.Data.Query;
using Xunit;

namespace NekoLib.Data.Tests.Unit.Gateway
{
    public class CommandPolicyTests
    {
        [Fact]
        public async Task Insert_ParameterSpecification_AppliesPortableMetadataAndNullValue()
        {
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand { Result = 1 });
            DatabaseGatewayOptions options = new DatabaseGatewayOptions
            {
                DefaultCommandTimeoutSeconds = 14
            };

            using (QueryExecutionContext context = new QueryExecutionContext(
                factory,
                new SqliteQueryTranslator(),
                options))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                DbParameterSpec specification = new DbParameterSpec(null)
                {
                    DbType = DbType.Decimal,
                    Size = 12,
                    Precision = 9,
                    Scale = 2,
                    Direction = ParameterDirection.InputOutput
                };

                int result = await gateway.Insert(
                    "INSERT INTO Ledger (Amount) VALUES (@p1)",
                    new Dictionary<string, object> { { "@p1", specification } });

                Assert.Equal(1, result);
                FakeNonQueryCommand command = factory.LastConnection.LastCommand;
                Assert.Equal(14, command.CommandTimeout);
                Assert.Single(command.Parameters);
                DbParameter parameter = (DbParameter)command.Parameters[0];
                Assert.Equal("@p1", parameter.ParameterName);
                Assert.Equal(DbType.Decimal, parameter.DbType);
                Assert.Equal(12, parameter.Size);
                Assert.Equal((byte)9, parameter.Precision);
                Assert.Equal((byte)2, parameter.Scale);
                Assert.Equal(ParameterDirection.InputOutput, parameter.Direction);
                Assert.Same(DBNull.Value, parameter.Value);
            }
        }

        [Fact]
        public async Task Insert_QueryBuilderTimeout_OverridesContextDefault()
        {
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand { Result = 1 });
            DatabaseGatewayOptions options = new DatabaseGatewayOptions
            {
                DefaultCommandTimeoutSeconds = 14
            };

            using (QueryExecutionContext context = new QueryExecutionContext(
                factory,
                new SqliteQueryTranslator(),
                options))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                QueryBuilder builder = new QueryBuilder()
                    .InsertInto("Ledger", new Dictionary<string, object> { { "Amount", 10 } })
                    .CommandTimeout(31);

                await gateway.Insert(builder);

                Assert.Equal(31, factory.LastConnection.LastCommand.CommandTimeout);
            }
        }

        [Fact]
        public async Task GetRaw_QueryBuilderTimeout_AppliesToReadCommand()
        {
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand
                {
                    Reader = new FakeDataReader(Array.Empty<string>(), Array.Empty<Type>())
                });

            using (QueryExecutionContext context = new QueryExecutionContext(
                factory,
                new SqliteQueryTranslator()))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                QueryBuilder builder = new QueryBuilder()
                    .Select("*")
                    .From("Ledger")
                    .CommandTimeout(19);

                await gateway.GetRaw(builder);

                Assert.Equal(19, factory.LastConnection.LastCommand.CommandTimeout);
            }
        }

        [Fact]
        public void Select_NewStatement_ClearsCommandTimeoutOverride()
        {
            QueryBuilder builder = new QueryBuilder()
                .Select("*")
                .From("Ledger")
                .CommandTimeout(19);

            Assert.Equal(19, builder.Build().CommandPolicy.TimeoutSeconds);

            QueryModel next = builder.Select("*").From("Archive").Build();

            Assert.Null(next.CommandPolicy.TimeoutSeconds);
        }

        [Fact]
        public void QueryExecutionContext_InvalidDefaultTimeout_Throws()
        {
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand());

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new QueryExecutionContext(
                    factory,
                    new SqliteQueryTranslator(),
                    new DatabaseGatewayOptions { DefaultCommandTimeoutSeconds = 0 }));
        }
    }
}
