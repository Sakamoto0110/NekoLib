#if NET9_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.Dynamic;
using NekoLib.Data.Gateway;
using NekoLib.Data.Mapping;
using NekoLib.Data.Query;
using Xunit;

namespace NekoLib.Data.Tests.Unit.Gateway
{
    public class StreamTerminalOutcomeTests
    {
        [Fact]
        public async Task StreamDto_FullEnumeration_ReportsCompletedAfterCleanup()
        {
            FakeDataReader reader = Reader(
                new[] { "Name" },
                new[] { typeof(string) },
                new object[] { "Alice" });
            FakeNonQueryCommand command = new FakeNonQueryCommand { Reader = reader };
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(() => command);
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                List<DbQueryStreamTerminalEventArgs> terminals =
                    new List<DbQueryStreamTerminalEventArgs>();
                bool cleanupObservedBySubscriber = false;
                context.OnStreamTerminal += _ => throw new InvalidOperationException("terminal observer");
                context.OnStreamTerminal += args =>
                {
                    cleanupObservedBySubscriber =
                        reader.WasDisposed &&
                        command.WasDisposed &&
                        factory.LastConnection.WasDisposed;
                    terminals.Add(args);
                };

                List<SimpleDto> rows = new List<SimpleDto>();
                await foreach (SimpleDto row in gateway.StreamDto<SimpleDto>(SelectAll()))
                    rows.Add(row);

                Assert.Equal("Alice", Assert.Single(rows).Name);
                DbQueryStreamTerminalEventArgs terminal = Assert.Single(terminals);
                Assert.Equal(DbQueryStreamOutcome.Completed, terminal.Outcome);
                Assert.Null(terminal.Exception);
                Assert.Equal("[SQL redacted]", terminal.RawSqlQuery);
                Assert.True(cleanupObservedBySubscriber);
                Assert.Single(context.GetObserverFailures());
            }
        }

        [Fact]
        public async Task StreamDto_MappingFailure_ReportsFailedExactlyOnce()
        {
            FakeDataReader reader = Reader(
                new[] { "Count" },
                new[] { typeof(long) },
                new object[] { long.MaxValue });
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand { Reader = reader });
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                List<DbQueryStreamTerminalEventArgs> terminals =
                    new List<DbQueryStreamTerminalEventArgs>();
                context.OnStreamTerminal += terminals.Add;

                DataMappingException failure = await Assert.ThrowsAsync<DataMappingException>(async () =>
                {
                    await foreach (OverflowDto _ in gateway.StreamDto<OverflowDto>(SelectAll()))
                    {
                    }
                });

                DbQueryStreamTerminalEventArgs terminal = Assert.Single(terminals);
                Assert.Equal(DbQueryStreamOutcome.Failed, terminal.Outcome);
                Assert.Same(failure, terminal.Exception);
                Assert.True(reader.WasDisposed);
            }
        }

        [Fact]
        public async Task StreamDto_CancelledBeforeOpen_ReportsCancelledExactlyOnce()
        {
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand { Reader = Reader(Array.Empty<string>(), Array.Empty<Type>()) });
            using (QueryExecutionContext context = CreateContext(factory))
            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                List<DbQueryStreamTerminalEventArgs> terminals =
                    new List<DbQueryStreamTerminalEventArgs>();
                context.OnStreamTerminal += terminals.Add;
                cancellation.Cancel();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                {
                    await foreach (SimpleDto _ in gateway.StreamDto<SimpleDto>(
                        SelectAll(),
                        cancellation.Token))
                    {
                    }
                });

                DbQueryStreamTerminalEventArgs terminal = Assert.Single(terminals);
                Assert.Equal(DbQueryStreamOutcome.Cancelled, terminal.Outcome);
                Assert.IsAssignableFrom<OperationCanceledException>(terminal.Exception);
            }
        }

        [Fact]
        public async Task StreamRaw_EarlyEnumeratorDisposal_ReportsDisposedBeforeCompletion()
        {
            FakeDataReader reader = Reader(
                new[] { "Name" },
                new[] { typeof(string) },
                new object[] { "Alice" },
                new object[] { "Bob" });
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand { Reader = reader });
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                List<DbQueryStreamTerminalEventArgs> terminals =
                    new List<DbQueryStreamTerminalEventArgs>();
                context.OnStreamTerminal += terminals.Add;

                IAsyncEnumerator<Dictionary<string, RecordItem>> enumerator =
                    ((IDqlStreamingGateway)gateway)
                        .StreamRaw("SELECT Name FROM Rows", null, CancellationToken.None)
                        .GetAsyncEnumerator();
                Assert.True(await enumerator.MoveNextAsync());
                await enumerator.DisposeAsync();

                DbQueryStreamTerminalEventArgs terminal = Assert.Single(terminals);
                Assert.Equal(DbQueryStreamOutcome.DisposedBeforeCompletion, terminal.Outcome);
                Assert.Null(terminal.Exception);
                Assert.True(reader.WasDisposed);
            }
        }

        [Fact]
        public async Task StreamDynamic_EmptySchema_ReportsCompletedExactlyOnce()
        {
            FakeDataReader reader = Reader(Array.Empty<string>(), Array.Empty<Type>());
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand { Reader = reader });
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                List<DbQueryStreamTerminalEventArgs> terminals =
                    new List<DbQueryStreamTerminalEventArgs>();
                context.OnStreamTerminal += terminals.Add;
                int rowCount = 0;

                await foreach (DynamicRow _ in gateway.StreamDynamic(SelectAll()))
                    rowCount++;

                Assert.Equal(0, rowCount);
                Assert.Equal(
                    DbQueryStreamOutcome.Completed,
                    Assert.Single(terminals).Outcome);
                Assert.True(reader.WasDisposed);
            }
        }

        private static QueryExecutionContext CreateContext(FakeNonQueryConnectionFactory factory)
        {
            return new QueryExecutionContext(factory, new SqliteQueryTranslator());
        }

        private static QueryBuilder SelectAll()
        {
            return new QueryBuilder().Select().From("Rows");
        }

        private static FakeDataReader Reader(
            string[] names,
            Type[] types,
            params object[][] rows)
        {
            return new FakeDataReader(names, types, rows);
        }

        public sealed class SimpleDto
        {
            public string Name { get; set; }
        }

        public sealed class OverflowDto
        {
            public int Count { get; set; }
        }
    }
}
#endif
