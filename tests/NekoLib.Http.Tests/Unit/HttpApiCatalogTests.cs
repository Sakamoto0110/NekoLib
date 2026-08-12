using System;
using Xunit;

namespace NekoLib.Http.Tests.Unit
{
    public sealed class HttpApiCatalogTests
    {
        [Fact]
        public void Create_DuplicateNamesIgnoringCase_Throws()
        {
            var first = HttpEndpoint.Get<string>(
                "cats.search",
                RelativeUri.FromPathSegments("images", "search"));
            var second = HttpEndpoint.Get<string>(
                "CATS.SEARCH",
                RelativeUri.FromPathSegments("breeds"));

            var error = Assert.Throws<InvalidOperationException>(() =>
                HttpApiCatalog.Create(builder =>
                {
                    builder.Register(first);
                    builder.Register(second);
                }));

            Assert.Contains("already registered", error.Message);
        }

        [Fact]
        public void Create_AfterBuild_RejectsFurtherRegistration()
        {
            HttpApiCatalogBuilder captured = null;
            var endpoint = HttpEndpoint.Get<string>(
                "cats.search",
                RelativeUri.FromPathSegments("images", "search"));

            var catalog = HttpApiCatalog.Create(builder =>
            {
                captured = builder;
                builder.Register(endpoint);
            });

            Assert.Same(endpoint, catalog.Get("CATS.SEARCH"));
            Assert.Single(catalog.Endpoints);
            Assert.Throws<InvalidOperationException>(() => captured.Register(
                HttpEndpoint.Get<string>(
                    "cats.other",
                    RelativeUri.FromPathSegments("other"))));
        }
    }
}
