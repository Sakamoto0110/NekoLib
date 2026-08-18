using System;
using Xunit;

namespace NekoLib.Http.Tests.Unit
{
    public sealed class RelativeUriTests
    {
        [Fact]
        public void Build_PathAndQueryValues_EscapesEachComponentDeterministically()
        {
            var uri = RelativeUriBuilder
                .Create("images", "cat/id")
                .AddQuery("search term", "white & black")
                .AddQuery("page", 2)
                .AddQuery("active", true)
                .AddQuery("omitted", (string)null)
                .Build();

            Assert.Equal(
                "images/cat%2Fid?search%20term=white%20%26%20black&page=2&active=true",
                uri.Value);
        }

        [Fact]
        public void AppendPathSegment_Empty_Throws()
        {
            var builder = RelativeUriBuilder.Create("images");

            Assert.Throws<ArgumentException>(() => builder.AppendPathSegment(" "));
        }

        [Fact]
        public void AddQuery_RepeatedName_PreservesEveryValueInOrder()
        {
            var uri = RelativeUriBuilder
                .Create("images", "search")
                .AddQuery("tag", "a")
                .AddQuery("tag", "b")
                .Build();

            Assert.Equal("images/search?tag=a&tag=b", uri.Value);
        }

        [Fact]
        public void AddQuery_NullValue_OmitsTheParameter()
        {
            var uri = RelativeUriBuilder
                .Create("images")
                .AddQuery("kept", "1")
                .AddQuery("dropped", (string)null)
                .Build();

            Assert.Equal("images?kept=1", uri.Value);
        }

        [Fact]
        public void AppendPathSegment_SegmentContainingSlash_IsEscapedSoRoutesStayRelative()
        {
            var uri = RelativeUriBuilder.Create("v1/images").Build();

            Assert.Equal("v1%2Fimages", uri.Value);
        }

        [Fact]
        public void FromPathSegments_WithNoSegments_TargetsTheBaseAddress()
        {
            Assert.Equal(string.Empty, RelativeUri.FromPathSegments().Value);
        }

        [Fact]
        public void AddQuery_EmptyName_Throws()
        {
            var builder = RelativeUriBuilder.Create("images");

            Assert.Throws<ArgumentException>(() => builder.AddQuery("", "value"));
        }
    }
}
