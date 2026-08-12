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
        public void AddQuery_EmptyName_Throws()
        {
            var builder = RelativeUriBuilder.Create("images");

            Assert.Throws<ArgumentException>(() => builder.AddQuery("", "value"));
        }
    }
}
