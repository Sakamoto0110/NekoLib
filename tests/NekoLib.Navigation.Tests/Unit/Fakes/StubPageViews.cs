using System.Threading.Tasks;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;

namespace NekoLib.Navigation.Tests.Unit.Fakes
{
    /// <summary>
    /// Test-double <see cref="IPageView"/>. Tracks lifecycle hook invocations so
    /// runtime tests can assert "enter/leave/restore" were called in the expected
    /// sequence. Concrete subclasses below give each page its own type identity
    /// (the runtime keys descriptors by <c>Type</c>).
    /// </summary>
    public abstract class StubPageView : IPageView, IPageLifecycle
    {
        public string Name => GetType().Name;
        public object NativeView => this;
        public bool IsDisposed { get; private set; }

        public int OnNavigatedToCount { get; private set; }
        public int OnNavigatedFromCount { get; private set; }
        public NavigationArgs LastNavArgs { get; private set; }

        public virtual Task OnNavigatedToAsync(NavigationArgs args)
        {
            OnNavigatedToCount++;
            LastNavArgs = args;
            return Task.CompletedTask;
        }

        public virtual Task OnNavigatedFromAsync()
        {
            OnNavigatedFromCount++;
            return Task.CompletedTask;
        }

        public void Dispose() => IsDisposed = true;
    }

    public sealed class StubIdle : StubPageView { }
    public sealed class StubA    : StubPageView { }
    public sealed class StubB    : StubPageView { }
    public sealed class StubC    : StubPageView { }
    public sealed class StubD    : StubPageView { }

    /// <summary>
    /// Stub for state-restore tests. Captures whatever <see cref="Counter"/> is
    /// when leaving; on back-navigation the runtime's RestoreState wiring (Pass 4
    /// N-2) pushes that value back. Used by the IPageStateful runtime test.
    /// </summary>
    public sealed class StubStateful : StubPageView, IPageStateful
    {
        public int Counter { get; set; }
        public int RestoreCallCount { get; private set; }
        public object LastRestoredState { get; private set; }

        public object CaptureState() => Counter;

        public void RestoreState(object state)
        {
            RestoreCallCount++;
            LastRestoredState = state;
            if (state is int restored)
                Counter = restored;
        }
    }
}
