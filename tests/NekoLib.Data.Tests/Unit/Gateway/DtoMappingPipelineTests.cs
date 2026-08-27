using System;
using System.Collections.Generic;
using System.Globalization;
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

        [Fact]
        public async Task StreamDto_RoundTripTemporalText_UsesSameReadAdaptationPolicy()
        {
            using (QueryExecutionContext context = CreateContext(RoundTripTemporalReader))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                List<TypeAdaptationEventArgs> events = new List<TypeAdaptationEventArgs>();
                gateway.OnTypeAdaptation += events.Add;
                List<TemporalOffsetDto> rows = new List<TemporalOffsetDto>();

                await foreach (TemporalOffsetDto row in
                    gateway.StreamDto<TemporalOffsetDto>(SelectAll()))
                {
                    rows.Add(row);
                }

                Assert.Equal(
                    new DateTimeOffset(2026, 8, 27, 10, 30, 0, TimeSpan.FromHours(-3)),
                    Assert.Single(rows).OccurredAt);
                Assert.Single(events);
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

        [Fact]
        public async Task GetDto_RoundTripTemporalText_UsesLosslessBuiltInRuleAndReports()
        {
            using (QueryExecutionContext context = CreateContext(RoundTripTemporalReader))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                List<TypeAdaptationEventArgs> events = new List<TypeAdaptationEventArgs>();
                gateway.OnTypeAdaptation += events.Add;

                TemporalOffsetDto row = Assert.Single(
                    await gateway.GetDto<TemporalOffsetDto>(SelectAll()));

                Assert.Equal(
                    new DateTimeOffset(2026, 8, 27, 10, 30, 0, TimeSpan.FromHours(-3)),
                    row.OccurredAt);
                TypeAdaptationEventArgs adaptation = Assert.Single(events);
                Assert.Equal(TypeAdaptationDirection.Read, adaptation.Direction);
                Assert.Equal(TypeAdaptationKind.Materialization, adaptation.Kind);
                Assert.Equal(TypeAdaptationLoss.Lossless, adaptation.Loss);
                Assert.Equal(TypeAdaptationReasonCode.BuiltInRule, adaptation.ReasonCode);
                Assert.Equal("OccurredAt", adaptation.Column);
                Assert.Equal("OccurredAt", adaptation.PropertyName);
                Assert.Null(adaptation.ParameterName);
                Assert.Equal("O", adaptation.Format);
                Assert.Single(adaptation.Attempts);
            }
        }

        [Fact]
        public async Task GetDto_TemporalPropertySetterFails_DoesNotReportCompletedAdaptation()
        {
            using (QueryExecutionContext context = CreateContext(RoundTripTemporalReader))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                List<TypeAdaptationEventArgs> events = new List<TypeAdaptationEventArgs>();
                gateway.OnTypeAdaptation += events.Add;

                DataMappingException exception = await Assert.ThrowsAsync<DataMappingException>(
                    () => gateway.GetDto<ThrowingTemporalDto>(SelectAll()));

                Assert.IsType<InvalidOperationException>(exception.InnerException);
                Assert.Empty(events);
            }
        }

        [Fact]
        public async Task GetDto_LossyTemporalReadWithoutAuthorization_ThrowsEvenWhenLenient()
        {
            DatabaseGatewayOptions options = new DatabaseGatewayOptions
            {
                MappingFailureMode = DataMappingFailureMode.Lenient
            };
            options.ReadTypeAdaptationRules.Add(
                ReadTypeAdaptationRule.For<TemporalDateTimeDto>(
                    nameof(TemporalDateTimeDto.OccurredAt),
                    TypeMaterializations.DateTimeOffsetToUtcDateTime));

            using (QueryExecutionContext context = CreateContext(OffsetTemporalReader, options))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                DataMappingException exception = await Assert.ThrowsAsync<DataMappingException>(
                    () => gateway.GetDto<TemporalDateTimeDto>(SelectAll()));

                TypeAdaptationException failure = Assert.IsType<TypeAdaptationException>(
                    exception.AdaptationFailure);
                Assert.Equal(
                    TypeAdaptationReasonCode.LossyAdaptationNotAuthorized,
                    failure.ReasonCode);
                Assert.Equal(TypeAdaptationLoss.PotentiallyLossy, failure.Loss);
                Assert.Single(failure.Attempts);
            }
        }

        [Fact]
        public async Task GetDto_ExplicitLossyTemporalRead_AllowsAndReportsOnce()
        {
            DatabaseGatewayOptions options = new DatabaseGatewayOptions
            {
                TypeLossPolicy = TypeLossPolicy.AllowExplicitAndReport
            };
            options.ReadTypeAdaptationRules.Add(
                ReadTypeAdaptationRule.For<TemporalDateTimeDto>(
                    nameof(TemporalDateTimeDto.OccurredAt),
                    TypeMaterializations.DateTimeOffsetToUtcDateTime));

            using (QueryExecutionContext context = CreateContext(OffsetTemporalReader, options))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                List<TypeAdaptationEventArgs> events = new List<TypeAdaptationEventArgs>();
                gateway.OnTypeAdaptation += events.Add;

                TemporalDateTimeDto row = Assert.Single(
                    await gateway.GetDto<TemporalDateTimeDto>(SelectAll()));

                Assert.Equal(
                    new DateTime(2026, 8, 27, 13, 30, 0, DateTimeKind.Utc),
                    row.OccurredAt);
                TypeAdaptationEventArgs adaptation = Assert.Single(events);
                Assert.Equal(TypeAdaptationReasonCode.ExplicitRule, adaptation.ReasonCode);
                Assert.Equal(TypeAdaptationLoss.PotentiallyLossy, adaptation.Loss);
                Assert.Equal(nameof(TemporalDateTimeDto.OccurredAt), adaptation.PropertyName);
                Assert.Single(adaptation.Attempts);
            }
        }

        [Fact]
        public async Task ReadDto_CustomTemporalParser_ReportsFormatAndCulture()
        {
            const string format = "yyyy/MM/dd HH:mm:ss:fff";
            DatabaseGatewayOptions options = new DatabaseGatewayOptions
            {
                TypeLossPolicy = TypeLossPolicy.AllowExplicitAndReport
            };
            options.ReadTypeAdaptationRules.Add(
                ReadTypeAdaptationRule.For<TemporalDateTimeDto>(
                    nameof(TemporalDateTimeDto.OccurredAt),
                    TypeMaterializations.StringToDateTimeRoundTrip));
            options.ReadTypeAdaptationRules.Add(
                ReadTypeAdaptationRule.For<TemporalDateTimeDto>(
                    nameof(TemporalDateTimeDto.OccurredAt),
                    TypeMaterializations.CreateStringToDateTime(
                        format,
                        CultureInfo.InvariantCulture),
                    "OccurredAt"));

            using (QueryExecutionContext context = CreateContext(FormattedTemporalReader, options))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                List<TypeAdaptationEventArgs> events = new List<TypeAdaptationEventArgs>();
                gateway.OnTypeAdaptation += events.Add;
                TemporalDateTimeDto observed = null;

                await gateway.ReadDto<TemporalDateTimeDto>(
                    SelectAll(),
                    row => observed = row);

                Assert.Equal(new DateTime(2026, 8, 27, 10, 30, 0), observed.OccurredAt);
                TypeAdaptationEventArgs adaptation = Assert.Single(events);
                Assert.Equal(format, adaptation.Format);
                Assert.Equal(CultureInfo.InvariantCulture.Name, adaptation.CultureName);
                Assert.Equal(TypeAdaptationLoss.PotentiallyLossy, adaptation.Loss);
            }
        }

        [Fact]
        public void Options_PotentiallyLossyAutomaticMaterializationRule_RejectsConfiguration()
        {
            DatabaseGatewayOptions options = new DatabaseGatewayOptions();
            options.AutomaticMaterializationRules.Add(
                TypeMaterializations.DateTimeOffsetToUtcDateTime);

            Assert.Throws<ArgumentException>(() => options.Validate());
        }

        [Fact]
        public void Options_ReadRuleTargetDoesNotMatchProperty_RejectsConfiguration()
        {
            DatabaseGatewayOptions options = new DatabaseGatewayOptions();
            options.ReadTypeAdaptationRules.Add(
                ReadTypeAdaptationRule.For<TemporalDateTimeDto>(
                    nameof(TemporalDateTimeDto.OccurredAt),
                    TypeMaterializations.StringToDateTimeOffsetRoundTrip));

            Assert.Throws<ArgumentException>(() => options.Validate());
        }

        [Fact]
        public void Options_ReadRuleTargetsStaticProperty_RejectsConfiguration()
        {
            DatabaseGatewayOptions options = new DatabaseGatewayOptions();
            options.ReadTypeAdaptationRules.Add(
                ReadTypeAdaptationRule.For<StaticTemporalDto>(
                    nameof(StaticTemporalDto.OccurredAt),
                    TypeMaterializations.StringToDateTimeRoundTrip));

            Assert.Throws<ArgumentException>(() => options.Validate());
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

        private static FakeDataReader RoundTripTemporalReader()
        {
            return new FakeDataReader(
                new[] { "OccurredAt" },
                new[] { typeof(string) },
                new object[] { "2026-08-27T10:30:00.0000000-03:00" });
        }

        private static FakeDataReader OffsetTemporalReader()
        {
            return new FakeDataReader(
                new[] { "OccurredAt" },
                new[] { typeof(DateTimeOffset) },
                new object[]
                {
                    new DateTimeOffset(2026, 8, 27, 10, 30, 0, TimeSpan.FromHours(-3))
                });
        }

        private static FakeDataReader FormattedTemporalReader()
        {
            return new FakeDataReader(
                new[] { "OccurredAt" },
                new[] { typeof(string) },
                new object[] { "2026/08/27 10:30:00:000" });
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

        public sealed class TemporalOffsetDto
        {
            public DateTimeOffset OccurredAt { get; set; }
        }

        public sealed class TemporalDateTimeDto
        {
            public DateTime OccurredAt { get; set; }
        }

        public sealed class ThrowingTemporalDto
        {
            public DateTimeOffset OccurredAt
            {
                get { return default(DateTimeOffset); }
                set { throw new InvalidOperationException("setter failure"); }
            }
        }

        public sealed class StaticTemporalDto
        {
            public static DateTime OccurredAt { get; set; }
        }

        public enum MappingStatus
        {
            Unknown,
            Ready
        }
    }
}
