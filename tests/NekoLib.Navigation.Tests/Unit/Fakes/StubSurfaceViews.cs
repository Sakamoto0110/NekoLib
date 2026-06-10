using System;
using System.Threading.Tasks;
using NekoLib.Navigation.Contracts.Pages;

namespace NekoLib.Navigation.Tests.Unit.Fakes
{
    /// <summary>
    /// Test doubles for the transient-surface views the services manage. Each
    /// exposes its bound callbacks publicly so tests can drive completion without
    /// any UI loop.
    /// </summary>
    public class StubToastView : IToastView
    {
        public string Name => GetType().Name;
        public object NativeView => this;
        public bool IsDisposed { get; private set; }
        public Action DismissCallback { get; private set; }
        public object LastShownPayload { get; private set; }

        public void OnShown(object payload) => LastShownPayload = payload;
        public void BindDismiss(Action dismissCallback) => DismissCallback = dismissCallback;
        public void Dispose() => IsDisposed = true;
    }

    public class StubDialogView : IDialogView
    {
        public string Name => GetType().Name;
        public object NativeView => this;
        public bool IsDisposed { get; private set; }
        public Action<bool> CompletionCallback { get; private set; }
        public object LastShownPayload { get; private set; }

        public Task OnShownAsync(object payload)
        {
            LastShownPayload = payload;
            return Task.CompletedTask;
        }
        public void BindCompletion(Action<bool> completionCallback) => CompletionCallback = completionCallback;
        public void Dispose() => IsDisposed = true;
    }

    public class StubPromptView : IPromptView<string>
    {
        public string Name => GetType().Name;
        public object NativeView => this;
        public bool IsDisposed { get; private set; }
        public Action<string> CompletionCallback { get; private set; }
        public object LastShownPayload { get; private set; }

        public Task OnShownAsync(object payload)
        {
            LastShownPayload = payload;
            return Task.CompletedTask;
        }
        public void BindCompletion(Action<string> completionCallback) => CompletionCallback = completionCallback;
        public void Dispose() => IsDisposed = true;
    }

    /// <summary>
    /// Popover that does NOT implement <see cref="IUnfocusAware"/>. Used to
    /// verify that PopoverService leaves non-aware views alone.
    /// </summary>
    public class StubPopoverView : IPopoverView
    {
        public string Name => GetType().Name;
        public object NativeView => this;
        public bool IsDisposed { get; private set; }
        public Action<bool> CompletionCallback { get; private set; }
        public object LastShownPayload { get; private set; }

        public Task OnShownAsync(object payload)
        {
            LastShownPayload = payload;
            return Task.CompletedTask;
        }
        public void BindCompletion(Action<bool> completionCallback) => CompletionCallback = completionCallback;
        public void Dispose() => IsDisposed = true;
    }

    /// <summary>
    /// Popover that opts into auto-dismissal on unfocus by calling its own
    /// completion callback with <c>false</c>. Mirrors what a real
    /// <c>AutoDismissPopoverBase</c> would do.
    /// </summary>
    public class StubAutoDismissPopoverView : IPopoverView, IUnfocusAware
    {
        public string Name => GetType().Name;
        public object NativeView => this;
        public bool IsDisposed { get; private set; }
        public Action<bool> CompletionCallback { get; private set; }
        public object LastShownPayload { get; private set; }
        public int UnfocusCount { get; private set; }

        public Task OnShownAsync(object payload)
        {
            LastShownPayload = payload;
            return Task.CompletedTask;
        }
        public void BindCompletion(Action<bool> completionCallback) => CompletionCallback = completionCallback;

        public Task OnUnfocusAsync()
        {
            UnfocusCount++;
            CompletionCallback?.Invoke(false);
            return Task.CompletedTask;
        }

        public void Dispose() => IsDisposed = true;
    }
}
