using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Runtime.Core;
using NekoLib.Navigation.Runtime.Factories;
using NekoLib.Navigation.Runtime.Registry;
using NekoLib.Navigation.Runtime.Services;
using NekoLib.Navigation.Tests.Unit.Fakes;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    [Collection("NavigationServiceFacade")]
    public sealed class SurfaceDiagnosticsFacadeTests
    {
        [Fact]
        public async Task Runtime_ResetAndShutdown_UseStableSurfaceCloseReasons()
        {
            await NavigationService.Shutdown();

            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(
                typeof(StubDialogView),
                () => new StubDialogView());
            var dialogService = new DialogService(host, factory);
            var services = new ServiceLocator();
            services.Register<IEventDispatcherAdapter>(
                new SyncEventDispatcherAdapter());
            services.Register<PageFactory>(factory);
            services.Register<IDialogService>(dialogService);
            services.Lock();
            var registry = PageRegistry.Create(_ => { });
            var context = new NavigationContext(
                host,
                services,
                registry,
                new FakePlatformAdapter());
            var traces = new List<NavigationTraceEvent>();
            context.Events.NavigationTrace += traces.Add;
            NavigationService.UseContext(context);

            try
            {
                var resetDialog =
                    NavigationService.ShowDialogAsync<StubDialogView>();
                await NavigationService.ResetAsync();
                Assert.False(await resetDialog);

                var shutdownDialog =
                    NavigationService.ShowDialogAsync<StubDialogView>();
                await NavigationService.Shutdown();
                Assert.False(await shutdownDialog);

                Assert.Equal(
                    new[]
                    {
                        NavigationTraceCloseReasons.Reset,
                        NavigationTraceCloseReasons.RuntimeTeardown
                    },
                    traces
                        .Where(e =>
                            e.Kind ==
                            NavigationTraceKind.SurfaceClosed)
                        .Select(e => e.CloseReason)
                        .ToArray());
            }
            finally
            {
                await NavigationService.Shutdown();
            }
        }
    }
}
