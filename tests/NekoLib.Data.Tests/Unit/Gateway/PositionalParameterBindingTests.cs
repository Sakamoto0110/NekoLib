using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using NekoLib.Data.Internal.Gateway;
using NekoLib.Data.Query;
using Xunit;

namespace NekoLib.Data.Tests.Unit.Gateway
{
    public class PositionalParameterBindingTests
    {
        [Fact]
        public async Task Insert_ReversedPlaceholders_BindsBySqlOccurrence()
        {
            FakeNonQueryConnectionFactory factory = CreateFactory();
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                await gateway.Insert(
                    "UPDATE T SET A = @p2 WHERE B = @p1",
                    new Dictionary<string, object>
                    {
                        { "@p1", "first" },
                        { "@p2", "second" }
                    });

                FakeNonQueryCommand command = factory.LastConnection.LastCommand;
                Assert.Equal("UPDATE T SET A = ? WHERE B = ?", command.CommandText);
                Assert.Equal(new object[] { "second", "first" }, ParameterValues(command));
            }
        }

        [Fact]
        public async Task Insert_RepeatedPlaceholder_BindsOncePerOccurrence()
        {
            FakeNonQueryConnectionFactory factory = CreateFactory();
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                await gateway.Insert(
                    "UPDATE T SET A = @p1 WHERE B = @p1",
                    new Dictionary<string, object> { { "@p1", 7 } });

                FakeNonQueryCommand command = factory.LastConnection.LastCommand;
                Assert.Equal("UPDATE T SET A = ? WHERE B = ?", command.CommandText);
                Assert.Equal(new object[] { 7, 7 }, ParameterValues(command));
            }
        }

        [Fact]
        public async Task Insert_QuotedAndCommentedMarkers_RewritesOnlyExecutablePlaceholders()
        {
            FakeNonQueryConnectionFactory factory = CreateFactory();
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                string sql = "UPDATE T SET A = @p1, Note = '@p2' /* @p3 */ -- @p4\r\nWHERE B = @p2";

                await gateway.Insert(
                    sql,
                    new Dictionary<string, object>
                    {
                        { "@p1", 1 },
                        { "@p2", 2 }
                    });

                FakeNonQueryCommand command = factory.LastConnection.LastCommand;
                Assert.Equal(
                    "UPDATE T SET A = ?, Note = '@p2' /* @p3 */ -- @p4\r\nWHERE B = ?",
                    command.CommandText);
                Assert.Equal(new object[] { 1, 2 }, ParameterValues(command));
            }
        }

        [Fact]
        public async Task Insert_PrefixCollision_DoesNotRewriteLongerIdentifier()
        {
            FakeNonQueryConnectionFactory factory = CreateFactory();
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                await gateway.Insert(
                    "UPDATE T SET A = @p1suffix WHERE B = @p1",
                    new Dictionary<string, object> { { "@p1", 7 } });

                FakeNonQueryCommand command = factory.LastConnection.LastCommand;
                Assert.Equal("UPDATE T SET A = @p1suffix WHERE B = ?", command.CommandText);
                Assert.Equal(new object[] { 7 }, ParameterValues(command));
            }
        }

        [Fact]
        public async Task Insert_MissingPositionalValue_ThrowsBeforeDispatch()
        {
            FakeNonQueryConnectionFactory factory = CreateFactory();
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                int dispatchCalls = 0;
                context.OnSqlDispatch += _ => dispatchCalls++;

                await Assert.ThrowsAsync<ArgumentException>(() => gateway.Insert(
                    "UPDATE T SET A = @p1 WHERE B = @p2",
                    new Dictionary<string, object> { { "@p1", 1 } }));

                Assert.Equal(0, dispatchCalls);
                Assert.Empty(factory.LastConnection.LastCommand.Parameters);
            }
        }

        [Fact]
        public async Task Insert_UnusedPositionalValue_ThrowsBeforeDispatch()
        {
            FakeNonQueryConnectionFactory factory = CreateFactory();
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                int dispatchCalls = 0;
                context.OnSqlDispatch += _ => dispatchCalls++;

                await Assert.ThrowsAsync<ArgumentException>(() => gateway.Insert(
                    "UPDATE T SET A = @p1",
                    new Dictionary<string, object>
                    {
                        { "@p1", 1 },
                        { "@p2", 2 }
                    }));

                Assert.Equal(0, dispatchCalls);
                Assert.Empty(factory.LastConnection.LastCommand.Parameters);
            }
        }

        private static FakeNonQueryConnectionFactory CreateFactory()
        {
            return new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand { Result = 1 });
        }

        private static QueryExecutionContext CreateContext(
            FakeNonQueryConnectionFactory factory)
        {
            return new QueryExecutionContext(
                factory,
                new SqliteQueryTranslator(),
                new DatabaseGatewayOptions
                {
                    ParameterBindingMode = DbParameterBindingMode.Positional
                });
        }

        private static object[] ParameterValues(FakeNonQueryCommand command)
        {
            return command.Parameters
                .Cast<DbParameter>()
                .Select(parameter => parameter.Value)
                .ToArray();
        }
    }
}
