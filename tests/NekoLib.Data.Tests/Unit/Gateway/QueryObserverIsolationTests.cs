using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NekoLib.Data.Gateway;
using NekoLib.Data.Query;
using Xunit;

namespace NekoLib.Data.Tests.Unit.Gateway
{
    public class QueryObserverIsolationTests
    {
        [Fact]
        public async Task Delete_QueryBuilder_RaisesGeneratedBeforeDispatchAndSuccess()
        {
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand { Result = 1 });
            using (QueryExecutionContext context = new QueryExecutionContext(
                factory,
                new SqliteQueryTranslator()))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                List<string> notifications = new List<string>();

                context.OnSqlGenerated += _ => notifications.Add("generated");
                context.OnSqlDispatch += _ => notifications.Add("dispatch");
                context.OnSuccess += _ => notifications.Add("success");

                int result = await gateway.Delete(
                    new QueryBuilder()
                        .DeleteFrom("Customers")
                        .Where("Id = @p1", 7));

                Assert.Equal(1, result);
                Assert.Equal(new[] { "generated", "dispatch", "success" }, notifications);
            }
        }

        [Fact]
        public async Task Insert_ThrowingObservers_PreservesResultAndSubscriberOrder()
        {
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand { Result = 3 });
            using (QueryExecutionContext context = new QueryExecutionContext(
                factory,
                new SqliteQueryTranslator()))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                List<string> notifications = new List<string>();

                context.OnSqlGenerated += _ =>
                {
                    notifications.Add("generated-throw");
                    throw new InvalidOperationException("generated observer");
                };
                context.OnSqlGenerated += args =>
                {
                    notifications.Add("generated-next");
                    Assert.Equal("[SQL redacted]", args.RawSqlQuery);
                };
                context.OnSqlDispatch += _ =>
                {
                    notifications.Add("dispatch-throw");
                    throw new InvalidOperationException("dispatch observer");
                };
                context.OnSqlDispatch += _ => notifications.Add("dispatch-next");
                context.OnSuccess += _ =>
                {
                    notifications.Add("success-throw");
                    throw new InvalidOperationException("success observer");
                };
                context.OnSuccess += args =>
                {
                    notifications.Add("success-next");
                    Assert.Null(args.Result);
                };

                int result = await gateway.Insert(
                    new QueryBuilder().InsertInto(
                        "Customers",
                        new Dictionary<string, object> { { "Name", "Alice" } }));

                Assert.Equal(3, result);
                Assert.Equal(
                    new[]
                    {
                        "generated-throw",
                        "generated-next",
                        "dispatch-throw",
                        "dispatch-next",
                        "success-throw",
                        "success-next"
                    },
                    notifications);

                IReadOnlyList<DbQueryObserverFailure> failures = context.GetObserverFailures();
                Assert.Equal(3, failures.Count);
                Assert.Equal(DbQueryEventType.SqlGenerated, failures[0].EventType);
                Assert.Equal(DbQueryEventType.SqlDispatched, failures[1].EventType);
                Assert.True((failures[2].EventType & DbQueryEventType.Success) != 0);
            }
        }

        [Fact]
        public async Task Insert_ProviderFailureAndThrowingErrorObserver_PreservesProviderException()
        {
            InvalidOperationException providerException =
                new InvalidOperationException("provider failure");
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand { ExecuteException = providerException });
            using (QueryExecutionContext context = new QueryExecutionContext(
                factory,
                new SqliteQueryTranslator()))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                int laterSubscriberCalls = 0;
                DbQueryFailureEventArgs observed = null;

                context.OnError += _ => throw new ApplicationException("observer failure");
                context.OnError += args =>
                {
                    laterSubscriberCalls++;
                    observed = args;
                };

                InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => gateway.Insert("INSERT INTO Customers (Name) VALUES ('Alice')"));

                Assert.Same(providerException, thrown);
                Assert.Equal(1, laterSubscriberCalls);
                Assert.Same(providerException, observed.Ex);
                Assert.Equal("[SQL redacted]", observed.RawSqlQuery);
                Assert.Single(context.GetObserverFailures());
            }
        }

        [Fact]
        public async Task ObserverFailureBuffer_OverCapacity_DropsOldestFailure()
        {
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand { Result = 1 });
            DatabaseGatewayOptions options = new DatabaseGatewayOptions
            {
                MaxObserverFailures = 2
            };
            using (QueryExecutionContext context = new QueryExecutionContext(
                factory,
                new SqliteQueryTranslator(),
                options))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                context.OnSqlGenerated += _ => throw new InvalidOperationException("first");
                context.OnSqlGenerated += _ => throw new InvalidOperationException("second");
                context.OnSqlGenerated += _ => throw new InvalidOperationException("third");

                await gateway.Insert(new QueryBuilder().InsertInto(
                    "Customers",
                    new Dictionary<string, object> { { "Name", "Alice" } }));

                IReadOnlyList<DbQueryObserverFailure> failures = context.GetObserverFailures();
                Assert.Equal(2, failures.Count);
                Assert.Equal("second", failures[0].Exception.Message);
                Assert.Equal("third", failures[1].Exception.Message);
                Assert.Equal(2, failures[0].Sequence);
                Assert.Equal(3, failures[1].Sequence);
            }
        }
    }
}
