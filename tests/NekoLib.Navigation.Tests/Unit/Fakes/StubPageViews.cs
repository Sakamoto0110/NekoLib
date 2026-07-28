using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Metadata.Attributes;

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

        public virtual void Dispose() => IsDisposed = true;
    }

    public sealed class StubIdle : StubPageView { }
    public sealed class StubA    : StubPageView { }
    public sealed class StubB    : StubPageView { }
    public sealed class StubC    : StubPageView { }
    public sealed class StubD    : StubPageView { }

    [RequireAuthenticated]
    public sealed class StubAuthenticated : StubPageView { }

    [AllowAnonymous]
    [RequireAuthenticated]
    public sealed class StubAnonymousGuarded : StubPageView { }

    [RequireRole("admin", RedirectTo = typeof(StubIdle))]
    public sealed class StubRoleRedirect : StubPageView { }

    [RequireRole("admin", RedirectTo = typeof(StubFailingTransientLoadBefore))]
    public sealed class StubRoleRedirectToFailing : StubPageView { }

    [PageMetadata(Name = "alias")]
    public sealed class StubAliased : StubPageView { }

    [PageLoad(NavigationLoadMode.LoadBeforeShow)]
    public sealed class StubFailingTransientLoadBefore : StubPageView, IBackgroundLoadable
    {
        public Task LoadInBackgroundAsync(object args)
            => Task.FromException(new InvalidOperationException("load failed"));

        public Task ApplyBackgroundResultAsync() => Task.CompletedTask;
    }

    [PageLoad(NavigationLoadMode.LoadBeforeShow)]
    [PageReuse(PageReusePolicy.StrongSingleton)]
    public sealed class StubConditionalLoadBefore : StubPageView, IBackgroundLoadable
    {
        public bool FailLoad { get; set; }

        public Task LoadInBackgroundAsync(object args)
            => FailLoad
                ? Task.FromException(new InvalidOperationException("load failed"))
                : Task.CompletedTask;

        public Task ApplyBackgroundResultAsync() => Task.CompletedTask;
    }

    public sealed class StubLoadingMask : StubPageView, IGlobalLoadingMask
    {
        public int OpenCount { get; private set; }
        public int CloseCount { get; private set; }

        public Task OnOverlayOpenedAsync(object payload)
        {
            OpenCount++;
            return Task.CompletedTask;
        }

        public Task OnOverlayClosingAsync()
        {
            CloseCount++;
            return Task.CompletedTask;
        }
    }

    [PageLoad(NavigationLoadMode.LoadInBackground)]
    public sealed class StubControllableBackground : StubPageView, IBackgroundLoadable
    {
        private readonly TaskCompletionSource<object> _started =
            new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<object> _completion =
            new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        private int _applyCount;

        public Task Started => _started.Task;
        public int ApplyCount => Volatile.Read(ref _applyCount);

        public Task LoadInBackgroundAsync(object args)
        {
            _started.TrySetResult(null);
            return _completion.Task;
        }

        public Task ApplyBackgroundResultAsync()
        {
            Interlocked.Increment(ref _applyCount);
            return Task.CompletedTask;
        }

        public void CompleteLoad() => _completion.TrySetResult(null);

        public void FailLoad(Exception error)
            => _completion.TrySetException(error);
    }

    [PageReuse(PageReusePolicy.StrongSingleton)]
    public class StubTeardownPage : StubPageView, IPageVisibility
    {
        public List<string> TeardownCalls { get; } = new List<string>();
        public Action<string> Observer { get; set; }

        public virtual void ShowPage()
        {
        }

        public virtual void HidePage()
        {
            TeardownCalls.Add("hide");
            Observer?.Invoke("hide");
        }

        public override Task OnNavigatedFromAsync()
        {
            TeardownCalls.Add("leave");
            Observer?.Invoke("leave");
            return base.OnNavigatedFromAsync();
        }

        public override void Dispose()
        {
            TeardownCalls.Add("dispose");
            Observer?.Invoke("dispose");
            base.Dispose();
        }
    }

    [KeepAttached]
    [PageReuse(PageReusePolicy.StrongSingleton)]
    public sealed class StubKeepAttachedTeardown : StubTeardownPage
    {
    }

    [PageReuse(PageReusePolicy.StrongSingleton)]
    public sealed class StubThrowingTeardown : StubTeardownPage
    {
        public override void HidePage()
        {
            base.HidePage();
            throw new InvalidOperationException("hide failed");
        }

        public override Task OnNavigatedFromAsync()
        {
            base.OnNavigatedFromAsync();
            throw new InvalidOperationException("leave failed");
        }
    }

    [KeepAttached]
    [PageReuse(PageReusePolicy.StrongSingleton)]
    public sealed class StubKeepAttached : StubPageView, IPageVisibility
    {
        public int ShowCount { get; private set; }
        public int HideCount { get; private set; }

        public void ShowPage() => ShowCount++;
        public void HidePage() => HideCount++;
    }

    [KeepAttached]
    [PageReuse(PageReusePolicy.StrongSingleton)]
    public sealed class StubKeepAttachedWithoutVisibility : StubPageView
    {
    }

    public sealed class StubThrowingShow : StubPageView, IPageVisibility
    {
        public int HideCount { get; private set; }

        public void ShowPage()
            => throw new InvalidOperationException("show failed");

        public void HidePage() => HideCount++;
    }

    public sealed class StubConditionalVisibility :
        StubPageView,
        IPageVisibility
    {
        public int ShowCount { get; private set; }
        public int HideCount { get; private set; }
        public bool ThrowOnShow { get; set; }

        public void ShowPage()
        {
            ShowCount++;
            if (ThrowOnShow)
                throw new InvalidOperationException("conditional show failed");
        }

        public void HidePage() => HideCount++;
    }

    [PageLoad(NavigationLoadMode.ShowImmediately)]
    public sealed class StubThrowingLoadAfter :
        StubPageView,
        IPageVisibility,
        IBackgroundLoadable
    {
        public int HideCount { get; private set; }

        public void ShowPage()
        {
        }

        public void HidePage() => HideCount++;

        public Task LoadInBackgroundAsync(object args)
            => Task.FromException(new InvalidOperationException("load after show failed"));

        public Task ApplyBackgroundResultAsync() => Task.CompletedTask;
    }

    public sealed class StubThrowingEnter : StubPageView, IPageVisibility
    {
        public int HideCount { get; private set; }

        public void ShowPage()
        {
        }

        public void HidePage() => HideCount++;

        public override Task OnNavigatedToAsync(NavigationArgs args)
            => Task.FromException(new InvalidOperationException("enter failed"));
    }

    [PageReuse(PageReusePolicy.StrongSingleton)]
    public sealed class StubThrowingDispose : StubPageView, IPageVisibility
    {
        public int DisposeCallCount { get; private set; }

        public void ShowPage()
        {
        }

        public void HidePage()
        {
        }

        public override void Dispose()
        {
            DisposeCallCount++;
            throw new InvalidOperationException("dispose failed");
        }
    }

    public abstract class StubLifecycleRecordingPage :
        StubPageView,
        IPageVisibility,
        IBackgroundLoadable
    {
        public Action<string> Observer { get; set; }

        protected void Record(string operation) => Observer?.Invoke(operation);

        public void ShowPage() => Record("show");

        public void HidePage() => Record("hide");

        public override Task OnNavigatedToAsync(NavigationArgs args)
        {
            Record("enter");
            return base.OnNavigatedToAsync(args);
        }

        public override Task OnNavigatedFromAsync()
        {
            Record("leave");
            return base.OnNavigatedFromAsync();
        }

        public virtual Task LoadInBackgroundAsync(object args)
        {
            Record("load");
            return Task.CompletedTask;
        }

        public virtual Task ApplyBackgroundResultAsync()
        {
            Record("apply");
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            Record("dispose");
            base.Dispose();
        }
    }

    public sealed class StubLifecycleSource : StubLifecycleRecordingPage
    {
    }

    [PageLoad(NavigationLoadMode.ShowImmediately)]
    public sealed class StubLifecycleShowImmediately :
        StubLifecycleRecordingPage
    {
    }

    [PageLoad(NavigationLoadMode.LoadBeforeShow)]
    public sealed class StubLifecycleLoadBeforeShow :
        StubLifecycleRecordingPage
    {
    }

    [PageMetadata(Name = "background-alias")]
    [PageLoad(NavigationLoadMode.LoadInBackground)]
    public sealed class StubLifecycleBackground :
        StubLifecycleRecordingPage
    {
        private readonly TaskCompletionSource<object> _started =
            new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<object> _completion =
            new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        public override async Task LoadInBackgroundAsync(object args)
        {
            Record("load");
            _started.TrySetResult(null);
            await _completion.Task;
        }

        public void CompleteLoad() => _completion.TrySetResult(null);
    }

    [KeepAttached]
    [PageReuse(PageReusePolicy.StrongSingleton)]
    public sealed class StubLifecycleKeepAttached :
        StubLifecycleRecordingPage
    {
    }

    [PageReuse(PageReusePolicy.StrongSingleton)]
    public sealed class StubBlockingShutdownPage :
        StubPageView,
        IPageVisibility
    {
        private readonly TaskCompletionSource<object> _leaveStarted =
            new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<object> _leaveReleased =
            new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public Task LeaveStarted => _leaveStarted.Task;

        public void ShowPage()
        {
        }

        public void HidePage()
        {
        }

        public override async Task OnNavigatedFromAsync()
        {
            _leaveStarted.TrySetResult(null);
            await _leaveReleased.Task;
            await base.OnNavigatedFromAsync();
        }

        public void ReleaseLeave() => _leaveReleased.TrySetResult(null);
    }

    /// <summary>
    /// Stub for state-restore tests. Captures whatever <see cref="Counter"/> is
    /// when leaving; on back-navigation the runtime's RestoreState wiring (Pass 4
    /// N-2) pushes that value back. Used by the IPageStateful runtime test.
    /// </summary>
    public sealed class StubStateful : StubPageView, IPageStateful
    {
        public int Counter { get; set; }
        public int CaptureCallCount { get; private set; }
        public int RestoreCallCount { get; private set; }
        public object LastRestoredState { get; private set; }

        public object CaptureState()
        {
            CaptureCallCount++;
            return Counter;
        }

        public void RestoreState(object state)
        {
            RestoreCallCount++;
            LastRestoredState = state;
            if (state is int restored)
                Counter = restored;
        }
    }
}
