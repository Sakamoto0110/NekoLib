using System;
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

        [Fact]
        public async Task CloseAll_DisposeInvokesCompletion_DefaultStillWins()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(
                typeof(DisposeCompletingPromptView),
                () => new DisposeCompletingPromptView());
            var service = new PromptService(host, factory);

            var task = service.ShowPromptAsync<
                DisposeCompletingPromptView,
                string>();
            var view = Assert.IsType<DisposeCompletingPromptView>(
                Assert.Single(host.AddedViews));

            service.CloseAll();

            Assert.Null(await task);
            Assert.True(view.IsDisposed);
            Assert.Single(host.RemovedViews);
        }

        [Fact]
        public async Task ShowPrompt_FocusFailure_RollsBackViewAndBlocker()
        {
            var host = new FakePageHost
            {
                FocusException = new InvalidOperationException("focus failed")
            };
            var factory = new PageFactory();
            factory.Register(typeof(StubPromptView), () => new StubPromptView());
            var blocker = new DialogServiceTests.CountingInteractionBlocker();
            var svc = new PromptService(host, factory, blocker);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.ShowPromptAsync<StubPromptView, string>());

            var view = Assert.IsType<StubPromptView>(Assert.Single(host.AddedViews));
            Assert.Same(view, Assert.Single(host.RemovedViews));
            Assert.True(view.IsDisposed);
            Assert.Equal(0, blocker.Depth);
        }

        [Fact]
        public async Task DialogAndPrompt_SharedBlocker_RemainsBlockedUntilBothClose()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(typeof(StubDialogView), () => new StubDialogView());
            factory.Register(typeof(StubPromptView), () => new StubPromptView());
            var blocker = new DialogServiceTests.CountingInteractionBlocker();
            var dialogService = new DialogService(host, factory, blocker);
            var promptService = new PromptService(host, factory, blocker);

            var dialogTask = dialogService.ShowDialogAsync<StubDialogView>();
            var promptTask = promptService.ShowPromptAsync<StubPromptView, string>();
            var dialog = Assert.IsType<StubDialogView>(host.AddedViews[0]);
            var prompt = Assert.IsType<StubPromptView>(host.AddedViews[1]);

            Assert.Equal(2, blocker.Depth);

            dialog.CompletionCallback(true);
            Assert.True(await dialogTask);
            Assert.Equal(1, blocker.Depth);

            prompt.CompletionCallback("done");
            Assert.Equal("done", await promptTask);
            Assert.Equal(0, blocker.Depth);
        }

        [Fact]
        public async Task ShowPrompt_BlockReentersShow_AcquiresOneReference()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(
                typeof(StubPromptView),
                () => new StubPromptView());
            var blocker =
                new DialogServiceTests.ReentrantInteractionBlocker();
            PromptService service = null;
            Task<string> nestedTask = null;
            blocker.OnBlock = () =>
                nestedTask = service.ShowPromptAsync<
                    StubPromptView,
                    string>();
            service = new PromptService(host, factory, blocker);

            var outerTask =
                service.ShowPromptAsync<StubPromptView, string>();

            Assert.NotNull(nestedTask);
            Assert.Equal(1, blocker.BlockCalls);
            Assert.Equal(1, blocker.Depth);
            Assert.Equal(2, host.AddedViews.Count);

            var nested = Assert.IsType<StubPromptView>(
                host.AddedViews[0]);
            var outer = Assert.IsType<StubPromptView>(
                host.AddedViews[1]);
            nested.CompletionCallback("nested");
            outer.CompletionCallback("outer");

            Assert.Equal("nested", await nestedTask);
            Assert.Equal("outer", await outerTask);
            Assert.Equal(1, blocker.UnblockCalls);
            Assert.Equal(0, blocker.Depth);
        }

        [Fact]
        public async Task Completion_CleanupFails_CallbackDoesNotThrowAndAwaiterFaults()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(
                typeof(ThrowingDisposePromptView),
                () => new ThrowingDisposePromptView());
            var service = new PromptService(host, factory);

            var task = service.ShowPromptAsync<
                ThrowingDisposePromptView,
                string>();
            var view = Assert.IsType<ThrowingDisposePromptView>(
                Assert.Single(host.AddedViews));

            var callbackError = Record.Exception(
                () => view.CompletionCallback("accepted"));
            var taskError = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await task);

            Assert.Null(callbackError);
            Assert.Equal("prompt dispose failed", taskError.Message);
            Assert.True(view.IsDisposed);
        }
    }
}
