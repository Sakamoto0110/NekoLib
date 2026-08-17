using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.Gateway;
using NekoLib.Data.Query;
using Xunit;

namespace NekoLib.Data.Tests.Unit.Gateway
{
    public class GatewayLifetimeContractTests
    {
        [Fact]
        public async Task ContainsData_Parameters_BindsBeforeReading()
        {
            FakeDataReader reader = Reader(new object[] { 1 });
            FakeNonQueryCommand command = new FakeNonQueryCommand { Reader = reader };
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => command);
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                bool containsData = await gateway.ContainsData(
                    "SELECT Value FROM T WHERE Value = @p1",
                    new Dictionary<string, object> { { "@p1", 1 } });

                Assert.True(containsData);
                Assert.Single(command.Parameters);
                Assert.Equal("@p1", command.Parameters[0].ParameterName);
                Assert.Equal(1, command.Parameters[0].Value);
            }
        }

        [Fact]
        public async Task Insert_Success_DisposesOwnedCommandAndConnection()
        {
            FakeNonQueryCommand command = new FakeNonQueryCommand { Result = 3 };
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => command);
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                int result = await gateway.Insert("UPDATE T SET A = 1");

                Assert.Equal(3, result);
                Assert.True(command.WasDisposed);
                Assert.True(factory.LastConnection.WasDisposed);
            }
        }

        [Fact]
        public async Task Insert_ProviderFailure_DisposesResourcesAndReportsOriginalFailureOnce()
        {
            InvalidOperationException failure = new InvalidOperationException("execute failed");
            FakeNonQueryCommand command = new FakeNonQueryCommand
            {
                ExecuteException = failure
            };
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => command);
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                List<DbQueryFailureEventArgs> failures = new List<DbQueryFailureEventArgs>();
                context.OnError += failures.Add;

                InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => gateway.Insert("UPDATE T SET A = 1"));

                Assert.Same(failure, thrown);
                Assert.Same(failure, Assert.Single(failures).Ex);
                Assert.True(command.WasDisposed);
                Assert.True(factory.LastConnection.WasDisposed);
            }
        }

        [Fact]
        public async Task Insert_OpenFailure_DisposesConnectionBeforeCommandCreation()
        {
            InvalidOperationException failure = new InvalidOperationException("open failed");
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand { Result = 1 },
                openException: failure);
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => gateway.Insert("UPDATE T SET A = 1"));

                Assert.Same(failure, thrown);
                Assert.True(factory.LastConnection.WasDisposed);
                Assert.Equal(0, factory.LastConnection.CreateCommandCalls);
            }
        }

        [Fact]
        public async Task Insert_CommandCreationFailure_DisposesOwnedConnection()
        {
            InvalidOperationException failure = new InvalidOperationException("command failed");
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => throw failure);
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => gateway.Insert("UPDATE T SET A = 1"));

                Assert.Same(failure, thrown);
                Assert.True(factory.LastConnection.WasDisposed);
                Assert.Equal(1, factory.LastConnection.CreateCommandCalls);
            }
        }

        [Fact]
        public async Task GetRaw_ReadFailure_DisposesReaderCommandAndOwnedConnection()
        {
            InvalidOperationException failure = new InvalidOperationException("read failed");
            FakeDataReader reader = Reader(new object[] { 1 });
            reader.ReadException = failure;
            FakeNonQueryCommand command = new FakeNonQueryCommand { Reader = reader };
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => command);
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => gateway.GetRaw("SELECT Value FROM T"));

                Assert.Same(failure, thrown);
                Assert.True(reader.WasDisposed);
                Assert.True(command.WasDisposed);
                Assert.True(factory.LastConnection.WasDisposed);
            }
        }

        [Fact]
        public async Task GetRaw_CancelledDuringRead_DisposesResourcesWithoutErrorEvent()
        {
            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                FakeDataReader reader = Reader(new object[] { 1 });
                reader.BeforeReadAsync = cancellation.Cancel;
                FakeNonQueryCommand command = new FakeNonQueryCommand { Reader = reader };
                FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                    () => command);
                using (QueryExecutionContext context = CreateContext(factory))
                {
                    DatabaseGateway gateway = new DatabaseGateway(context);
                    int errorEvents = 0;
                    context.OnError += _ => errorEvents++;

                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                        gateway.GetRaw("SELECT Value FROM T", cancellation.Token));

                    Assert.Equal(0, errorEvents);
                    Assert.True(reader.WasDisposed);
                    Assert.True(command.WasDisposed);
                    Assert.True(factory.LastConnection.WasDisposed);
                }
            }
        }

        [Fact]
        public async Task GetRaw_SessionSuccess_DisposesCommandAndReaderButKeepsConnectionOpen()
        {
            FakeDataReader reader = Reader(new object[] { 1 });
            FakeNonQueryCommand command = new FakeNonQueryCommand { Reader = reader };
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => command);
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                using (DbSession session = await gateway.OpenSessionAsync())
                {
                    await gateway.GetRaw(
                        "SELECT Value FROM T",
                        new Dictionary<string, object>(),
                        session);

                    Assert.True(reader.WasDisposed);
                    Assert.True(command.WasDisposed);
                    Assert.False(factory.LastConnection.WasDisposed);
                    Assert.Equal(ConnectionState.Open, factory.LastConnection.State);
                }

                Assert.True(factory.LastConnection.WasDisposed);
            }
        }

        private static QueryExecutionContext CreateContext(
            FakeNonQueryConnectionFactory factory)
        {
            return new QueryExecutionContext(factory, new SqliteQueryTranslator());
        }

        private static FakeDataReader Reader(params object[][] rows)
        {
            return new FakeDataReader(
                new[] { "Value" },
                new[] { typeof(int) },
                rows);
        }
    }
}
