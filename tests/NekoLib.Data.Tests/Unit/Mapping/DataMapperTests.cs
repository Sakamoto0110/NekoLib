using System;
using System.Collections.Generic;
using NekoLib.Data.Mapping;
using Xunit;

namespace NekoLib.Data.Tests.Unit.Mapping
{
    public class DataMapperTests
    {
        [Fact]
        public void Map_ValidTextValues_UsesInvariantConversionMatrix()
        {
            Dictionary<string, RecordItem> row = new Dictionary<string, RecordItem>
            {
                { "Count", Item("Count", "42") },
                { "Enabled", Item("Enabled", "true") },
                { "Status", Item("Status", "Ready") },
                { "OccurredAt", Item("OccurredAt", "2026-08-02T12:30:00Z") }
            };

            MappingDto result = DataMapper.Map<MappingDto>(row);

            Assert.Equal(42, result.Count);
            Assert.True(result.Enabled);
            Assert.Equal(MappingStatus.Ready, result.Status);
            Assert.Equal(
                new DateTimeOffset(2026, 8, 2, 12, 30, 0, TimeSpan.Zero),
                result.OccurredAt);
        }

        [Fact]
        public void Map_InvalidValueInStrictMode_ThrowsStructuredException()
        {
            Dictionary<string, RecordItem> row = new Dictionary<string, RecordItem>
            {
                { "Count", Item("provider_count", "not-an-integer") }
            };

            DataMappingException exception = Assert.Throws<DataMappingException>(
                () => DataMapper.Map<MappingDto>(row));

            Assert.Equal("provider_count", exception.ColumnName);
            Assert.Equal("Count", exception.PropertyName);
            Assert.Equal(typeof(string), exception.SourceType);
            Assert.Equal(typeof(int), exception.TargetType);
            Assert.DoesNotContain("not-an-integer", exception.Message);
        }

        [Fact]
        public void Map_NumericOverflowInStrictMode_ThrowsDataMappingException()
        {
            Dictionary<string, RecordItem> row = new Dictionary<string, RecordItem>
            {
                { "Count", Item("Count", "999999999999999999999999") }
            };

            DataMappingException exception = Assert.Throws<DataMappingException>(
                () => DataMapper.Map<MappingDto>(row));

            Assert.IsType<OverflowException>(exception.InnerException);
        }

        [Fact]
        public void Map_InvalidValueInLenientMode_LeavesPropertyDefaultAndContinues()
        {
            Dictionary<string, RecordItem> row = new Dictionary<string, RecordItem>
            {
                { "Count", Item("Count", "invalid") },
                { "Enabled", Item("Enabled", "true") }
            };

            MappingDto result = DataMapper.Map<MappingDto>(
                row,
                DataMappingFailureMode.Lenient);

            Assert.Equal(0, result.Count);
            Assert.True(result.Enabled);
        }

        private static RecordItem Item(string name, string value)
        {
            return new RecordItem
            {
                Name = name,
                Type = typeof(string).FullName,
                Value = value
            };
        }

        private sealed class MappingDto
        {
            public int Count { get; set; }
            public bool Enabled { get; set; }
            public MappingStatus Status { get; set; }
            public DateTimeOffset OccurredAt { get; set; }
        }

        private enum MappingStatus
        {
            Unknown,
            Ready
        }
    }
}
