using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.Connection;
using NekoLib.Data.Internal.Gateway;
using NekoLib.Data.Query;
using Xunit;

namespace NekoLib.Data.Tests.Unit.Gateway
{
    public class SessionOwnershipTests
    {
        [Fact]
        public void Dispose_ContextOwnedFactory_DisposesFactory()
        {
            FakeNonQueryConnectionFactory factory = CreateReaderFactory();
            QueryExecutionContext context = new QueryExecutionContext(
                factory,
                new SqliteQueryTranslator());

            context.Dispose();

            Assert.True(factory.WasDisposed);
            Assert.Equal(1, factory.DisposeCalls);
        }

        [Fact]
        public void Dispose_ExternalFactory_DoesNotDisposeFactory()
        {
            FakeNonQueryConnectionFactory factory = CreateReaderFactory();
            QueryExecutionContext context = new QueryExecutionContext(
                factory,
                new SqliteQueryTranslator(),
                connectionFactoryOwnership: DbConnectionFactoryOwnership.External);

            context.Dispose();

            Assert.False(factory.WasDisposed);
            Assert.Equal(0, factory.DisposeCalls);
        }

        [Fact]
        public async Task Create_GenericStringFactory_ReturnsClosedConfiguredConnection()
        {
            using (DbConnectionAbstractFactory<FakeNonQueryConnection> factory =
                new DbConnectionAbstractFactory<FakeNonQueryConnection>("Data Source=fake"))
            using (FakeNonQueryConnection connection =
                (FakeNonQueryConnection)await factory.Create())
            {
                Assert.Equal("Data Source=fake", connection.ConnectionString);
                Assert.Equal(ConnectionState.Closed, connection.State);
            }
        }

        [Fact]
        public async Task GetRaw_ClosedSession_ThrowsBeforeCommandCreation()
        {
            FakeNonQueryConnection connection = new FakeNonQueryConnection(
                CreateReaderCommand);
            using (DbSession session = new DbSession(connection))
            using (QueryExecutionContext context = new QueryExecutionContext(
                CreateReaderFactory(),
                new SqliteQueryTranslator()))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    gateway.GetRaw(
                        "SELECT Value FROM T",
                        new Dictionary<string, object>(),
                        session));

                Assert.Equal(ConnectionState.Closed, connection.State);
                Assert.Null(connection.LastCommand);
            }
        }

        [Fact]
        public async Task GetRaw_ExternalSessionFirstUse_BindsSessionToContext()
        {
            FakeNonQueryConnection connection = new FakeNonQueryConnection(
                CreateReaderCommand);
            await connection.OpenAsync(CancellationToken.None);
            using (DbSession session = new DbSession(connection))
            using (QueryExecutionContext firstContext = new QueryExecutionContext(
                CreateReaderFactory(),
                new SqliteQueryTranslator()))
            using (QueryExecutionContext secondContext = new QueryExecutionContext(
                CreateReaderFactory(),
                new SqliteQueryTranslator()))
            {
                DatabaseGateway firstGateway = new DatabaseGateway(firstContext);
                DatabaseGateway secondGateway = new DatabaseGateway(secondContext);

                await firstGateway.GetRaw(
                    "SELECT Value FROM T",
                    new Dictionary<string, object>(),
                    session);

                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    secondGateway.GetRaw(
                        "SELECT Value FROM T",
                        new Dictionary<string, object>(),
                        session));

                Assert.Equal(1, connection.CreateCommandCalls);
            }
        }

        [Fact]
        public async Task GetRaw_GatewaySessionWithDifferentContext_RejectsAffinity()
        {
            FakeNonQueryConnectionFactory firstFactory = CreateReaderFactory();
            FakeNonQueryConnectionFactory secondFactory = CreateReaderFactory();
            using (QueryExecutionContext firstContext = new QueryExecutionContext(
                firstFactory,
                new SqliteQueryTranslator()))
            using (QueryExecutionContext secondContext = new QueryExecutionContext(
                secondFactory,
                new SqliteQueryTranslator()))
            {
                DatabaseGateway firstGateway = new DatabaseGateway(firstContext);
                DatabaseGateway secondGateway = new DatabaseGateway(secondContext);
                using (DbSession session = await firstGateway.OpenSessionAsync())
                {
                    await Assert.ThrowsAsync<InvalidOperationException>(() =>
                        secondGateway.GetRaw(
                            "SELECT Value FROM T",
                            new Dictionary<string, object>(),
                            session));

                    Assert.Equal(0, secondFactory.CreateCalls);
                    Assert.Null(firstFactory.LastConnection.LastCommand);
                }
            }
        }

        private static FakeNonQueryConnectionFactory CreateReaderFactory()
        {
            return new FakeNonQueryConnectionFactory(CreateReaderCommand);
        }

        private static FakeNonQueryCommand CreateReaderCommand()
        {
            return new FakeNonQueryCommand
            {
                Reader = new FakeDataReader(Array.Empty<string>(), Array.Empty<Type>())
            };
        }
    }
}
