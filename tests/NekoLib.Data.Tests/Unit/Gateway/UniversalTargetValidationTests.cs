using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NekoLib.Data.Dynamic;
using NekoLib.Data.Internal.Gateway;
using NekoLib.Data.Query;
using Xunit;

namespace NekoLib.Data.Tests.Unit.Gateway
{
    public class UniversalTargetValidationTests
    {
        [Fact]
        public async Task Read_TargetWithoutPublicParameterlessConstructor_ThrowsBeforeConnectionCreation()
        {
            FakeNonQueryConnectionFactory factory = CreateFactory();
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                Action<NoDefaultConstructorDto> handler = _ => { };

                InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => gateway.Read(SelectAll(), (Delegate)handler));

                Assert.Contains("public parameterless constructor", exception.Message);
                Assert.Equal(0, factory.CreateCalls);
            }
        }

        [Fact]
        public async Task Read_ValueReturningDelegate_ThrowsBeforeConnectionCreation()
        {
            FakeNonQueryConnectionFactory factory = CreateFactory();
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                Func<ValidDto, int> handler = _ => 1;

                InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => gateway.Read(SelectAll(), (Delegate)handler));

                Assert.Contains("void-returning", exception.Message);
                Assert.Equal(0, factory.CreateCalls);
            }
        }

        [Fact]
        public async Task Read_ZeroParameterDelegate_ThrowsBeforeConnectionCreation()
        {
            FakeNonQueryConnectionFactory factory = CreateFactory();
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                Action handler = () => { };

                InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => gateway.Read(SelectAll(), (Delegate)handler));

                Assert.Contains("exactly one", exception.Message);
                Assert.Equal(0, factory.CreateCalls);
            }
        }

        [Fact]
        public async Task Read_RawRowTarget_RejectsUniversalPathBeforeConnectionCreation()
        {
            FakeNonQueryConnectionFactory factory = CreateFactory();
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                Action<Dictionary<string, RecordItem>> handler = _ => { };

                InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => gateway.Read(SelectAll(), (Delegate)handler));

                Assert.Contains("raw API", exception.Message);
                Assert.Equal(0, factory.CreateCalls);
            }
        }

        [Fact]
        public async Task Read_DynamicRowTarget_UsesExplicitDynamicPath()
        {
            FakeNonQueryConnectionFactory factory = CreateFactory();
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                DynamicRow observed = null;

                await gateway.Read<DynamicRow>(SelectAll(), row => observed = row);

                Assert.NotNull(observed);
                Assert.Equal("Alice", observed["Name"]);
                Assert.Equal(1, factory.CreateCalls);
            }
        }

        [Fact]
        public async Task Read_ObjectTarget_ReturnsDynamicRowWithoutUnrelatedDtoCast()
        {
            FakeNonQueryConnectionFactory factory = CreateFactory();
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                object observed = null;

                await gateway.Read<object>(SelectAll(), row => observed = row);

                Assert.IsType<DynamicRow>(observed);
            }
        }

#if NET9_0_OR_GREATER
        [Fact]
        public void StreamData_InvalidDtoTarget_ThrowsBeforeReturningEnumerableOrOpeningConnection()
        {
            FakeNonQueryConnectionFactory factory = CreateFactory();
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                Assert.Throws<InvalidOperationException>(() =>
                    gateway.StreamData<NoDefaultConstructorDto>(SelectAll()));
                Assert.Equal(0, factory.CreateCalls);
            }
        }
#endif

        private static FakeNonQueryConnectionFactory CreateFactory()
        {
            return new FakeNonQueryConnectionFactory(() => new FakeNonQueryCommand
            {
                Reader = new FakeDataReader(
                    new[] { "Name" },
                    new[] { typeof(string) },
                    new object[] { "Alice" })
            });
        }

        private static QueryExecutionContext CreateContext(FakeNonQueryConnectionFactory factory)
        {
            return new QueryExecutionContext(factory, new SqliteQueryTranslator());
        }

        private static QueryBuilder SelectAll()
        {
            return new QueryBuilder().Select().From("Rows");
        }

        public sealed class NoDefaultConstructorDto
        {
            public NoDefaultConstructorDto(string name)
            {
                Name = name;
            }

            public string Name { get; }
        }

        public sealed class ValidDto
        {
            public string Name { get; set; }
        }
    }
}
