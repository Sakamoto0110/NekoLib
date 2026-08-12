using Newtonsoft.Json;
using System;

namespace NekoLib.Http.RuntimeTests.TheCatApi
{
    internal static class TheCatApiEndpoints
    {
        internal static readonly HttpEndpoint<SearchImagesRequest, CatImage[]> SearchImages
            = HttpEndpoint.Get<SearchImagesRequest, CatImage[]>(
                "thecatapi.images.search",
                request => RelativeUriBuilder
                    .Create("images", "search")
                    .AddQuery("limit", request.Limit)
                    .AddQuery("page", request.Page)
                    .AddQuery("order", request.Order)
                    .Build());

        internal static readonly HttpEndpoint<GetImageRequest, CatImage> GetImage
            = HttpEndpoint.Get<GetImageRequest, CatImage>(
                "thecatapi.images.get",
                request => RelativeUriBuilder
                    .Create("images")
                    .AppendPathSegment(request.ImageId)
                    .Build());

        internal static readonly HttpEndpoint<CreateFavouriteRequest, CreateFavouriteResponse>
            CreateFavourite = HttpEndpoint.Post<
                CreateFavouriteRequest,
                CreateFavouriteResponse>(
                    "thecatapi.favourites.create",
                    request => RelativeUri.FromPathSegments("favourites"));

        internal static readonly HttpEndpoint<ListFavouritesRequest, Favourite[]> ListFavourites
            = HttpEndpoint.Get<ListFavouritesRequest, Favourite[]>(
                "thecatapi.favourites.list",
                request => RelativeUriBuilder
                    .Create("favourites")
                    .AddQuery("sub_id", request.SubId)
                    .AddQuery("limit", request.Limit)
                    .Build());

        internal static readonly HttpEndpoint<DeleteFavouriteRequest, HttpNoContent>
            DeleteFavourite = HttpEndpoint.Delete<DeleteFavouriteRequest>(
                "thecatapi.favourites.delete",
                request => RelativeUriBuilder
                    .Create("favourites")
                    .AppendPathSegment(request.FavouriteId.ToString())
                    .Build());

        internal static HttpApiCatalog CreateCatalog()
            => HttpApiCatalog.Create(builder => builder
                .Register(SearchImages)
                .Register(GetImage)
                .Register(CreateFavourite)
                .Register(ListFavourites)
                .Register(DeleteFavourite));
    }

    internal sealed class SearchImagesRequest
    {
        internal int Limit { get; set; } = 1;
        internal int Page { get; set; }
        internal string Order { get; set; } = "RANDOM";
    }

    internal sealed class GetImageRequest
    {
        internal string ImageId { get; set; } = string.Empty;
    }

    internal sealed class CreateFavouriteRequest
    {
        [JsonProperty("image_id")]
        public string ImageId { get; set; } = string.Empty;

        [JsonProperty("sub_id")]
        public string SubId { get; set; } = string.Empty;
    }

    internal sealed class CreateFavouriteResponse
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("message")]
        public string? Message { get; set; }
    }

    internal sealed class ListFavouritesRequest
    {
        internal string SubId { get; set; } = string.Empty;
        internal int Limit { get; set; } = 100;
    }

    internal sealed class DeleteFavouriteRequest
    {
        internal int FavouriteId { get; set; }
    }

    internal sealed class CatImage
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("url")]
        public string Url { get; set; } = string.Empty;

        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }
    }

    internal sealed class Favourite
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("image_id")]
        public string ImageId { get; set; } = string.Empty;

        [JsonProperty("sub_id")]
        public string? SubId { get; set; }
    }
}
