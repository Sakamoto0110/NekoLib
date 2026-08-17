using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using NekoLib.Data.Gateway;
using NekoLib.Data.Query;
using Xunit;

namespace NekoLib.Data.Tests.Unit.Gateway
{
    public class DmlTransactionSymmetryTests
    {
        [Fact]
        public async Task Delete_InterfaceQueryBuilderSession_UsesSessionTransaction()
        {
            FakeNonQueryConnectionFactory factory = CreateFactory();
            using (QueryExecutionContext context = new QueryExecutionContext(
                factory,
                new SqliteQueryTranslator()))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                IDmlGateway dml = gateway;
                using (DbSession session = await gateway.OpenSessionAsync())
                {
                    session.BeginTransaction();
                    DbTransaction transaction = session.Transaction;

                    int result = await dml.Delete(
                        new QueryBuilder()
                            .DeleteFrom("Ledger")
                            .Where("Id = @p1", 7),
                        session);

                    Assert.Equal(1, result);
                    Assert.Same(transaction, factory.LastConnection.LastCommand.Transaction);
                    Assert.Equal(1, factory.CreateCalls);
                    session.Commit();
                }
            }
        }

        [Fact]
        public async Task Insert_QueryBuilderSession_UsesSessionTransaction()
        {
            FakeNonQueryConnectionFactory factory = CreateFactory();
            using (QueryExecutionContext context = new QueryExecutionContext(
                factory,
                new SqliteQueryTranslator()))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                using (DbSession session = await gateway.OpenSessionAsync())
                {
                    session.BeginTransaction();
                    DbTransaction transaction = session.Transaction;

                    int result = await gateway.Insert(
                        new QueryBuilder().InsertInto(
                            "Ledger",
                            new Dictionary<string, object> { { "Amount", 10 } }),
                        session);

                    Assert.Equal(1, result);
                    Assert.Same(
                        transaction,
                        factory.LastConnection.LastCommand.Transaction);
                    Assert.Equal(1, factory.CreateCalls);
                    session.Commit();
                }
            }
        }

        [Fact]
        public async Task Update_InterfaceQueryBuilderSession_UsesSameSessionPath()
        {
            FakeNonQueryConnectionFactory factory = CreateFactory();
            using (QueryExecutionContext context = new QueryExecutionContext(
                factory,
                new SqliteQueryTranslator()))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                IDmlGateway dml = gateway;
                using (DbSession session = await gateway.OpenSessionAsync())
                {
                    session.BeginTransaction();
                    DbTransaction transaction = session.Transaction;
                    QueryBuilder builder = new QueryBuilder()
                        .Update(
                            "Ledger",
                            new Dictionary<string, object> { { "Amount", 11 } })
                        .Where("Id = @p1", 7);

                    int result = await dml.Update(builder, session);

                    Assert.Equal(1, result);
                    Assert.Same(
                        transaction,
                        factory.LastConnection.LastCommand.Transaction);
                    Assert.Equal(1, factory.CreateCalls);
                    session.Commit();
                }
            }
        }

        [Fact]
        public void Commit_NestedTransaction_CommitsOnlyAtDepthZero()
        {
            using (FakeNonQueryConnection connection = CreateOpenConnection())
            using (DbSession session = new DbSession(connection))
            {
                session.BeginTransaction();
                FakeNonQueryTransaction transaction = connection.LastTransaction;
                session.BeginTransaction();

                session.Commit();

                Assert.Equal(0, transaction.CommitCalls);
                Assert.Same(transaction, session.Transaction);

                session.Commit();

                Assert.Equal(1, transaction.CommitCalls);
                Assert.True(transaction.WasDisposed);
                Assert.Null(session.Transaction);
                Assert.Equal(1, connection.BeginTransactionCalls);
            }
        }

        [Fact]
        public void BeginTransaction_AfterCommit_StartsNewProviderTransaction()
        {
            using (FakeNonQueryConnection connection = CreateOpenConnection())
            using (DbSession session = new DbSession(connection))
            {
                session.BeginTransaction();
                FakeNonQueryTransaction first = connection.LastTransaction;
                session.Commit();

                session.BeginTransaction();
                FakeNonQueryTransaction second = connection.LastTransaction;
                session.Commit();

                Assert.NotSame(first, second);
                Assert.Equal(1, first.CommitCalls);
                Assert.Equal(1, second.CommitCalls);
                Assert.Equal(2, connection.BeginTransactionCalls);
            }
        }

        [Fact]
        public void BeginTransaction_AfterNestedRollback_StartsNewProviderTransaction()
        {
            using (FakeNonQueryConnection connection = CreateOpenConnection())
            using (DbSession session = new DbSession(connection))
            {
                session.BeginTransaction();
                session.BeginTransaction();
                FakeNonQueryTransaction first = connection.LastTransaction;

                session.Rollback();

                Assert.Equal(1, first.RollbackCalls);
                Assert.True(first.WasDisposed);
                Assert.Null(session.Transaction);
                Assert.Throws<InvalidOperationException>(() => session.Commit());

                session.BeginTransaction();
                FakeNonQueryTransaction second = connection.LastTransaction;
                session.Commit();

                Assert.NotSame(first, second);
                Assert.Equal(1, second.CommitCalls);
                Assert.Equal(2, connection.BeginTransactionCalls);
            }
        }

        private static FakeNonQueryConnectionFactory CreateFactory()
        {
            return new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand { Result = 1 });
        }

        private static FakeNonQueryConnection CreateOpenConnection()
        {
            FakeNonQueryConnection connection = new FakeNonQueryConnection(
                () => new FakeNonQueryCommand { Result = 1 });
            connection.Open();
            return connection;
        }
    }
}
