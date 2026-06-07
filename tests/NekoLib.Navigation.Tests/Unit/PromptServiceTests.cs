using System.Threading.Tasks;
using NekoLib.Navigation.Runtime.Factories;
using NekoLib.Navigation.Runtime.Services;
using NekoLib.Navigation.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    public class PromptServiceTests
    {
        private static (PromptService svc, FakePageHost host) Build()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(typeof(StubPromptView), () => new StubPromptView());
            return (new PromptService(host, factory, interactionBlocker: null), host);
        }

        [Fact]
        public async Task ShowPrompt_ResolvesWithCompletionPayload()
        {
            var (svc, host) = Build();

            var task = svc.ShowPromptAsync<StubPromptView, string>("payload");
            var view = (StubPromptView)host.AddedViews[0];

            Assert.Equal("payload", view.LastShownPayload);

            view.CompletionCallback("user input");

            Assert.Equal("user input", await task);
            Assert.True(view.IsDisposed);
            Assert.Single(host.RemovedViews);
        }

        /// <summary>
        /// N-3/N-6 for prompts: CloseAll resolves any pending prompt task with
        /// <c>default(TResult)</c> (so a Task&lt;string&gt; resolves to null, etc.)
        /// without leaking the view or hanging the awaiter.
        /// </summary>
        [Fact]
        public async Task CloseAll_WithPendingPrompt_ResolvesWithDefaultResult()
        {
            var (svc, host) = Build();

            var task = svc.ShowPromptAsync<StubPromptView, string>();
            var view = (StubPromptView)host.AddedViews[0];

            svc.CloseAll();

            Assert.Null(await task);
            Assert.True(view.IsDisposed);
            Assert.Single(host.RemovedViews);
        }
    }
}
