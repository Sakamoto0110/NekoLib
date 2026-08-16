using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Http.RuntimeTests.TheCatApi
{
    internal static class Program
    {
        private const string ApiKeyEnvironmentVariable = "NEKOLIB_THECATAPI_KEY";
        private static readonly Uri ProviderBaseAddress
            = new Uri("https://api.thecatapi.com/v1/");

        private static async Task<int> Main()
        {
            var started = DateTimeOffset.UtcNow;
            var result = new ScenarioResult
            {
                StartedUtc = started,
                TargetFramework = TargetFramework,
                RunSubId = "nekolib-http-" + Guid.NewGuid().ToString("N")
            };
            var artifactDirectory = CreateArtifactDirectory(started);
            var resultPath = Path.Combine(artifactDirectory, "result.json");
            var apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);
            result.ApiKeyPresent = !string.IsNullOrWhiteSpace(apiKey);

            if (!result.ApiKeyPresent)
            {
                result.ExitCode = ScenarioExitCodes.PrerequisiteMissing;
                AddCheck(
                    result,
                    "api-key-present",
                    false,
                    $"Set {ApiKeyEnvironmentVariable} to a maintainer-owned key.");
                Finish(result, resultPath);
                Console.Error.WriteLine(
                    $"Missing prerequisite: {ApiKeyEnvironmentVariable}. No provider request was sent.");
                Console.WriteLine("Artifact: " + artifactDirectory);
                return result.ExitCode;
            }

            var interrupted = false;
            using (var scenarioCancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2)))
            using (var httpClient = new System.Net.Http.HttpClient())
            {
                ConsoleCancelEventHandler cancelHandler = (sender, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    interrupted = true;
                    scenarioCancellation.Cancel();
                };
                Console.CancelKeyPress += cancelHandler;

                httpClient.BaseAddress = ProviderBaseAddress;
                httpClient.Timeout = TimeSpan.FromSeconds(15);
                httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
                httpClient.DefaultRequestHeaders.UserAgent.Add(
                    new ProductInfoHeaderValue("NekoLib.Http.ProviderProbe", "1.0"));

                var api = new HttpApiClient(
                    httpClient,
                    TheCatApiEndpoints.CreateCatalog(),
                    new HttpApiClientOptions
                    {
                        MaxResponseContentBytes = 512 * 1024
                    });

                var explicitDeleteCompleted = false;
                try
                {
                    await RunChecksAsync(
                        api,
                        result,
                        () => explicitDeleteCompleted = true,
                        scenarioCancellation.Token).ConfigureAwait(false);
                    result.ExitCode = ScenarioExitCodes.Success;
                }
                catch (ScenarioCheckException ex)
                {
                    result.ExitCode = ScenarioExitCodes.CheckFailed;
                    AddCheck(result, "scenario-terminal", false, ex.Message);
                }
                catch (Exception ex) when (
                    ex is HttpResponseDeserializationException ||
                    ex is HttpResponseContentTooLargeException)
                {
                    result.ExitCode = ScenarioExitCodes.CheckFailed;
                    AddCheck(
                        result,
                        "provider-contract",
                        false,
                        ex.GetType().Name + ": the provider response violated the bounded contract.");
                }
                catch (OperationCanceledException)
                {
                    result.ExitCode = interrupted
                        ? ScenarioExitCodes.Interrupted
                        : ScenarioExitCodes.Timeout;
                    AddCheck(
                        result,
                        "scenario-terminal",
                        false,
                        interrupted ? "Interrupted by Ctrl+C." : "A bounded wait expired.");
                }
                catch (HttpRequestException ex)
                {
                    result.ExitCode = ScenarioExitCodes.PrerequisiteMissing;
                    AddCheck(
                        result,
                        "provider-reachable",
                        false,
                        ex.GetType().Name + ": the provider request could not complete.");
                }
                catch (Exception ex)
                {
                    result.ExitCode = ScenarioExitCodes.Unexpected;
                    AddCheck(
                        result,
                        "scenario-terminal",
                        false,
                        ex.GetType().Name + ": unexpected scenario failure.");
                }
                finally
                {
                    using (var cleanupCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                    {
                        await CleanupAsync(
                            api,
                            result,
                            explicitDeleteCompleted,
                            cleanupCancellation.Token).ConfigureAwait(false);
                    }

                    if (result.CleanupProblems.Count > 0)
                        result.ExitCode = ScenarioExitCodes.CleanupFailed;

                    Console.CancelKeyPress -= cancelHandler;
                }
            }

            Finish(result, resultPath);
            Console.WriteLine(
                $"TheCatAPI provider probe exit {result.ExitCode}: " +
                $"{result.Checks.Count(check => check.Passed)}/{result.Checks.Count} checks passed.");
            Console.WriteLine("Artifact: " + artifactDirectory);
            return result.ExitCode;
        }

        private static async Task RunChecksAsync(
            HttpApiClient api,
            ScenarioResult result,
            Action markExplicitDeleteCompleted,
            CancellationToken cancellationToken)
        {
            var search = await api.SendAsync(
                TheCatApiEndpoints.SearchImages,
                new SearchImagesRequest(),
                cancellationToken).ConfigureAwait(false);
            RequireSuccess(result, "search-images-status", search.IsSuccessStatusCode, search.StatusCode);
            var images = search.RequireValue();
            Require(
                result,
                "search-images-shape",
                images.Length == 1 &&
                !string.IsNullOrWhiteSpace(images[0].Id) &&
                Uri.TryCreate(images[0].Url, UriKind.Absolute, out _),
                "Search returned one image with a non-empty id and absolute URL.");

            var selected = images[0];
            var lookup = await api.SendAsync(
                TheCatApiEndpoints.GetImage,
                new GetImageRequest { ImageId = selected.Id },
                cancellationToken).ConfigureAwait(false);
            RequireSuccess(result, "get-image-status", lookup.IsSuccessStatusCode, lookup.StatusCode);
            Require(
                result,
                "get-image-identity",
                string.Equals(lookup.RequireValue().Id, selected.Id, StringComparison.Ordinal),
                "Image lookup returned the searched image id.");

            var create = await api.SendAsync(
                TheCatApiEndpoints.CreateFavourite,
                new CreateFavouriteRequest
                {
                    ImageId = selected.Id,
                    SubId = result.RunSubId
                },
                cancellationToken).ConfigureAwait(false);
            RequireSuccess(
                result,
                "create-favourite-status",
                create.IsSuccessStatusCode,
                create.StatusCode);
            var favouriteId = create.RequireValue().Id;
            Require(
                result,
                "create-favourite-id",
                favouriteId > 0,
                "Favourite creation returned a positive id.");

            var listed = await ListRunFavouritesAsync(api, result.RunSubId, cancellationToken)
                .ConfigureAwait(false);
            Require(
                result,
                "list-favourite",
                listed.Any(item =>
                    item.Id == favouriteId &&
                    string.Equals(item.ImageId, selected.Id, StringComparison.Ordinal) &&
                    string.Equals(item.SubId, result.RunSubId, StringComparison.Ordinal)),
                "The created favourite was queryable by the run sub_id.");

            var delete = await api.SendAsync(
                TheCatApiEndpoints.DeleteFavourite,
                new DeleteFavouriteRequest { FavouriteId = favouriteId },
                cancellationToken).ConfigureAwait(false);
            RequireSuccess(
                result,
                "delete-favourite-status",
                delete.IsSuccessStatusCode,
                delete.StatusCode);
            markExplicitDeleteCompleted();

            var absent = await WaitUntilAbsentAsync(
                api,
                result.RunSubId,
                favouriteId,
                cancellationToken).ConfigureAwait(false);
            Require(
                result,
                "delete-favourite-visible",
                absent,
                "The deleted favourite no longer appears for the run sub_id.");
        }

        private static async Task CleanupAsync(
            HttpApiClient api,
            ScenarioResult result,
            bool explicitDeleteCompleted,
            CancellationToken cancellationToken)
        {
            try
            {
                var leftovers = await ListRunFavouritesAsync(
                    api,
                    result.RunSubId,
                    cancellationToken).ConfigureAwait(false);

                foreach (var leftover in leftovers)
                {
                    var deleted = await api.SendAsync(
                        TheCatApiEndpoints.DeleteFavourite,
                        new DeleteFavouriteRequest { FavouriteId = leftover.Id },
                        cancellationToken).ConfigureAwait(false);
                    if (!deleted.IsSuccessStatusCode)
                    {
                        result.CleanupProblems.Add(
                            $"Favourite {leftover.Id} cleanup returned HTTP {(int)deleted.StatusCode}.");
                    }
                }

                var remaining = await ListRunFavouritesAsync(
                    api,
                    result.RunSubId,
                    cancellationToken).ConfigureAwait(false);
                if (remaining.Length > 0)
                {
                    result.CleanupProblems.Add(
                        $"{remaining.Length} run-owned favourite(s) remained after cleanup.");
                }
                else
                {
                    AddCheck(
                        result,
                        "cleanup-reconciled",
                        true,
                        explicitDeleteCompleted
                            ? "Explicit deletion was confirmed and no run-owned favourite remained."
                            : "Recovery cleanup removed or confirmed absence of every run-owned favourite.");
                }
            }
            catch (Exception ex)
            {
                result.CleanupProblems.Add(
                    ex.GetType().Name + ": cleanup could not reconcile run-owned favourites.");
            }
        }

        private static async Task<Favourite[]> ListRunFavouritesAsync(
            HttpApiClient api,
            string subId,
            CancellationToken cancellationToken)
        {
            var response = await api.SendAsync(
                TheCatApiEndpoints.ListFavourites,
                new ListFavouritesRequest { SubId = subId },
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new ScenarioCheckException(
                    $"Listing favourites returned HTTP {(int)response.StatusCode}.");
            }

            return response.RequireValue();
        }

        private static async Task<bool> WaitUntilAbsentAsync(
            HttpApiClient api,
            string subId,
            int favouriteId,
            CancellationToken cancellationToken)
        {
            for (var attempt = 0; attempt < 4; attempt++)
            {
                var favourites = await ListRunFavouritesAsync(api, subId, cancellationToken)
                    .ConfigureAwait(false);
                if (favourites.All(item => item.Id != favouriteId))
                    return true;

                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken)
                    .ConfigureAwait(false);
            }

            return false;
        }

        private static void RequireSuccess(
            ScenarioResult result,
            string name,
            bool success,
            System.Net.HttpStatusCode statusCode)
            => Require(
                result,
                name,
                success,
                $"Provider returned HTTP {(int)statusCode}.");

        private static void Require(
            ScenarioResult result,
            string name,
            bool condition,
            string detail)
        {
            AddCheck(result, name, condition, detail);
            if (!condition)
                throw new ScenarioCheckException(name + " failed: " + detail);
        }

        private static void AddCheck(
            ScenarioResult result,
            string name,
            bool passed,
            string detail)
            => result.Checks.Add(new ScenarioCheck
            {
                Name = name,
                Passed = passed,
                Detail = detail
            });

        private static string CreateArtifactDirectory(DateTimeOffset started)
        {
            var path = Path.Combine(
                Environment.CurrentDirectory,
                "artifacts",
                "validation",
                "http",
                $"thecatapi-{TargetFramework}-{started:yyyyMMddTHHmmssfffZ}");
            Directory.CreateDirectory(path);
            return path;
        }

        private static void Finish(ScenarioResult result, string resultPath)
        {
            result.FinishedUtc = DateTimeOffset.UtcNow;
            var json = JsonConvert.SerializeObject(result, Formatting.Indented);
            var temporaryPath = resultPath + ".tmp";
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, resultPath);
        }

        private static string TargetFramework
        {
            get
            {
#if NETFRAMEWORK
                return "net481";
#else
                return "net9.0";
#endif
            }
        }
    }
}
