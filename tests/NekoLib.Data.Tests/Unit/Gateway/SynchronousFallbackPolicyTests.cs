using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.Gateway;
using NekoLib.Data.Query;
using Xunit;

namespace NekoLib.Data.Tests.Unit.Gateway
{
    public class SynchronousFallbackPolicyTests
    {
        [Fact]
        public async Task Insert_SynchronousOpenFallbackDisabled_PropagatesNotSupported()
        {
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand { Result = 1 },
                useSynchronousOpenFallback: true);

            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                await Assert.ThrowsAsync<NotSupportedException>(() =>
                    gateway.Insert("UPDATE T SET A = 1"));

                Assert.Equal(1, factory.LastConnection.OpenAsyncCalls);
                Assert.Equal(0, factory.LastConnection.OpenCalls);
            }
        }

        [Fact]
        public async Task Insert_SynchronousOpenFallbackEnabled_UsesBlockingOpen()
        {
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand { Result = 1 },
                useSynchronousOpenFallback: true);

            using (QueryExecutionContext context = CreateContext(factory, enabled: true))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                int result = await gateway.Insert("UPDATE T SET A = 1");

                Assert.Equal(1, result);
                Assert.Equal(1, factory.LastConnection.OpenCalls);
            }
        }

        [Fact]
        public async Task Insert_SynchronousExecuteFallbackDisabled_PropagatesNotSupported()
        {
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand
                {
                    Result = 1,
                    UseSynchronousNonQueryFallback = true
                });

            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                await Assert.ThrowsAsync<NotSupportedException>(() =>
                    gateway.Insert("UPDATE T SET A = 1"));

                Assert.Equal(0, factory.LastConnection.LastCommand.ExecuteNonQueryCalls);
            }
        }

        [Fact]
        public async Task GetRaw_SynchronousExecuteAndReadFallbackEnabled_UsesBlockingMethods()
        {
            FakeDataReader reader = new FakeDataReader(
                new[] { "Value" },
                new[] { typeof(int) },
                new object[] { 7 })
            {
                UseSynchronousReadFallback = true
            };
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand
                {
                    Reader = reader,
                    UseSynchronousReaderFallback = true
                });

            using (QueryExecutionContext context = CreateContext(factory, enabled: true))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                List<Dictionary<string, RecordItem>> rows = await gateway.GetRaw(
                    "SELECT Value FROM T");

                Assert.Single(rows);
                Assert.Equal(1, factory.LastConnection.LastCommand.ExecuteReaderCalls);
                Assert.True(reader.ReadCalls >= 2);
            }
        }

        [Fact]
        public async Task Insert_CancelledBeforeBlockingFallback_DoesNotInvokeSyncExecute()
        {
            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                    () => new FakeNonQueryCommand
                    {
                        Result = 1,
                        UseSynchronousNonQueryFallback = true,
                        BeforeAsyncNotSupported = cancellation.Cancel
                    });

                using (QueryExecutionContext context = CreateContext(factory, enabled: true))
                {
                    DatabaseGateway gateway = new DatabaseGateway(context);

                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                        gateway.Insert("UPDATE T SET A = 1", null, cancellation.Token));

                    Assert.Equal(0, factory.LastConnection.LastCommand.ExecuteNonQueryCalls);
                }
            }
        }

        private static QueryExecutionContext CreateContext(
            FakeNonQueryConnectionFactory factory,
            bool enabled = false)
        {
            return new QueryExecutionContext(
                factory,
                new SqliteQueryTranslator(),
                new DatabaseGatewayOptions
                {
                    SynchronousFallbackMode = enabled
                        ? DbSynchronousFallbackMode.Enabled
                        : DbSynchronousFallbackMode.Disabled
                });
        }
    }
}
