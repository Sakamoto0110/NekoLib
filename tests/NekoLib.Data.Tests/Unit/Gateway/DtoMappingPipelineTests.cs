using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NekoLib.Data.Gateway;
using NekoLib.Data.Mapping;
using NekoLib.Data.Query;
using Xunit;

namespace NekoLib.Data.Tests.Unit.Gateway
{
    public class DtoMappingPipelineTests
    {
        [Fact]
        public async Task GetDto_ReaderValues_MapsCanonicalConversionMatrix()
        {
            using (QueryExecutionContext context = CreateContext(SuccessReader))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                List<MappingDto> rows = await gateway.GetDto<MappingDto>(SelectAll());

                MappingDto row = Assert.Single(rows);
                AssertMapped(row);
            }
        }

        [Fact]
        public async Task ReadDto_ReaderValues_UsesSameMappingContractAsBufferedRead()
        {
            using (QueryExecutionContext context = CreateContext(SuccessReader))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                MappingDto observed = null;

                await gateway.ReadDto<MappingDto>(SelectAll(), row => observed = row);

                AssertMapped(observed);
            }
        }

#if NET9_0_OR_GREATER
        [Fact]
        public async Task StreamDto_ReaderValues_UsesSameMappingContractAsBufferedRead()
        {
            using (QueryExecutionContext context = CreateContext(SuccessReader))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                List<MappingDto> rows = new List<MappingDto>();

                await foreach (MappingDto row in gateway.StreamDto<MappingDto>(SelectAll()))
                    rows.Add(row);

                AssertMapped(Assert.Single(rows));
            }
        }
#endif

        [Fact]
        public async Task GetDto_NumericOverflowInStrictMode_ThrowsStructuredException()
        {
            using (QueryExecutionContext context = CreateContext(OverflowReader))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                DataMappingException exception = await Assert.ThrowsAsync<DataMappingException>(
                    () => gateway.GetDto<OverflowDto>(SelectAll()));

                Assert.Equal("Count", exception.ColumnName);
                Assert.Equal("Count", exception.PropertyName);
                Assert.Equal(typeof(long), exception.SourceType);
                Assert.Equal(typeof(int), exception.TargetType);
                Assert.IsType<OverflowException>(exception.InnerException);
            }
        }

        [Fact]
        public async Task ReadDto_UnsupportedConversionInStrictMode_ThrowsStructuredException()
        {
            using (QueryExecutionContext context = CreateContext(UnsupportedReader))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                DataMappingException exception = await Assert.ThrowsAsync<DataMappingException>(
                    () => gateway.ReadDto<UnsupportedDto>(SelectAll(), _ => { }));

                Assert.Equal(typeof(string), exception.SourceType);
                Assert.Equal(typeof(Uri), exception.TargetType);
                Assert.IsType<InvalidCastException>(exception.InnerException);
            }
        }

#if NET9_0_OR_GREATER
        [Fact]
        public async Task StreamDto_NullForNonNullableProperty_ThrowsStructuredException()
        {
            using (QueryExecutionContext context = CreateContext(NonNullableNullReader))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                DataMappingException exception = await Assert.ThrowsAsync<DataMappingException>(async () =>
                {
                    await foreach (NonNullableDto _ in gateway.StreamDto<NonNullableDto>(SelectAll()))
                    {
                    }
                });

                Assert.Equal(typeof(int), exception.TargetType);
                Assert.IsType<InvalidCastException>(exception.InnerException);
            }
        }
#endif

        [Fact]
        public async Task GetDto_NumericOverflowInLenientMode_LeavesDefaultAndContinues()
        {
            DatabaseGatewayOptions options = new DatabaseGatewayOptions
            {
                MappingFailureMode = DataMappingFailureMode.Lenient
            };
            using (QueryExecutionContext context = CreateContext(OverflowReader, options))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                OverflowDto row = Assert.Single(await gateway.GetDto<OverflowDto>(SelectAll()));

                Assert.Equal(0, row.Count);
                Assert.Equal("preserved", row.Name);
            }
        }

        private static QueryExecutionContext CreateContext(
            Func<FakeDataReader> readerFactory,
            DatabaseGatewayOptions options = null)
        {
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand { Reader = readerFactory() });
            return new QueryExecutionContext(
                factory,
                new SqliteQueryTranslator(),
                options);
        }

        private static QueryBuilder SelectAll()
        {
            return new QueryBuilder().Select().From("Rows");
        }

        private static FakeDataReader SuccessReader()
        {
            return new FakeDataReader(
                new[] { "OptionalCount", "Status", "Payload", "OccurredAt", "SmallNumber" },
                new[] { typeof(int), typeof(string), typeof(byte[]), typeof(DateTime), typeof(long) },
                new object[]
                {
                    DBNull.Value,
                    "Ready",
                    new byte[] { 1, 2, 3 },
                    new DateTime(2026, 8, 2, 14, 0, 0, DateTimeKind.Utc),
                    7L
                });
        }

        private static FakeDataReader OverflowReader()
        {
            return new FakeDataReader(
                new[] { "Count", "Name" },
                new[] { typeof(long), typeof(string) },
                new object[] { long.MaxValue, "preserved" });
        }

        private static FakeDataReader UnsupportedReader()
        {
            return new FakeDataReader(
                new[] { "Address" },
                new[] { typeof(string) },
                new object[] { "https://example.invalid" });
        }

        private static FakeDataReader NonNullableNullReader()
        {
            return new FakeDataReader(
                new[] { "Count" },
                new[] { typeof(int) },
                new object[] { DBNull.Value });
        }

        private static void AssertMapped(MappingDto row)
        {
            Assert.NotNull(row);
            Assert.Null(row.OptionalCount);
            Assert.Equal(MappingStatus.Ready, row.Status);
            Assert.Equal(new byte[] { 1, 2, 3 }, row.Payload);
            Assert.Equal(
                new DateTime(2026, 8, 2, 14, 0, 0, DateTimeKind.Utc),
                row.OccurredAt);
            Assert.Equal(7, row.SmallNumber);
        }

        public sealed class MappingDto
        {
            public int? OptionalCount { get; set; }
            public MappingStatus Status { get; set; }
            public byte[] Payload { get; set; }
            public DateTime OccurredAt { get; set; }
            public int SmallNumber { get; set; }
        }

        public sealed class OverflowDto
        {
            public int Count { get; set; }
            public string Name { get; set; }
        }

        public sealed class UnsupportedDto
        {
            public Uri Address { get; set; }
        }

        public sealed class NonNullableDto
        {
            public int Count { get; set; }
        }

        public enum MappingStatus
        {
            Unknown,
            Ready
        }
    }
}
