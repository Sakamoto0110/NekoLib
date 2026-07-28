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

        public virtual void OnShown(object payload) => LastShownPayload = payload;
        public virtual void BindDismiss(Action dismissCallback) => DismissCallback = dismissCallback;
        public virtual void Dispose() => IsDisposed = true;
    }

    public class StubDialogView : IDialogView
    {
        public string Name => GetType().Name;
        public object NativeView => this;
        public bool IsDisposed { get; private set; }
        public Action<bool> CompletionCallback { get; private set; }
        public object LastShownPayload { get; private set; }

        public virtual Task OnShownAsync(object payload)
        {
            LastShownPayload = payload;
            return Task.CompletedTask;
        }
        public virtual void BindCompletion(Action<bool> completionCallback) => CompletionCallback = completionCallback;
        public virtual void Dispose() => IsDisposed = true;
    }

    public class StubPromptView : IPromptView<string>
    {
        public string Name => GetType().Name;
        public object NativeView => this;
        public bool IsDisposed { get; private set; }
        public Action<string> CompletionCallback { get; private set; }
        public object LastShownPayload { get; private set; }

        public virtual Task OnShownAsync(object payload)
        {
            LastShownPayload = payload;
            return Task.CompletedTask;
        }
        public virtual void BindCompletion(Action<string> completionCallback) => CompletionCallback = completionCallback;
        public virtual void Dispose() => IsDisposed = true;
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

        public virtual Task OnShownAsync(object payload)
        {
            LastShownPayload = payload;
            return Task.CompletedTask;
        }
        public virtual void BindCompletion(Action<bool> completionCallback) => CompletionCallback = completionCallback;
        public virtual void Dispose() => IsDisposed = true;
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

    public sealed class ThrowingToastView : StubToastView
    {
        public override void OnShown(object payload)
            => throw new InvalidOperationException("toast setup failed");
    }

    public sealed class SelfDismissingToastView : StubToastView
    {
        public override void OnShown(object payload)
        {
            base.OnShown(payload);
            DismissCallback?.Invoke();
        }
    }

    public sealed class ThrowingDialogView : StubDialogView
    {
        public override Task OnShownAsync(object payload)
            => Task.FromException(new InvalidOperationException("dialog setup failed"));
    }

    public sealed class ThrowingPromptView : StubPromptView
    {
        public override Task OnShownAsync(object payload)
            => Task.FromException(new InvalidOperationException("prompt setup failed"));
    }

    public sealed class ThrowingPopoverView : StubPopoverView
    {
        public override Task OnShownAsync(object payload)
            => Task.FromException(new InvalidOperationException("popover setup failed"));
    }

    public sealed class DisposeCompletingDialogView : StubDialogView
    {
        public override void Dispose()
        {
            CompletionCallback?.Invoke(true);
            base.Dispose();
        }
    }

    public sealed class DisposeCompletingPromptView : StubPromptView
    {
        public override void Dispose()
        {
            CompletionCallback?.Invoke("dispose result");
            base.Dispose();
        }
    }

    public sealed class DisposeCompletingPopoverView : StubPopoverView
    {
        public override void Dispose()
        {
            CompletionCallback?.Invoke(true);
            base.Dispose();
        }
    }

    public sealed class ThrowingDisposeDialogView : StubDialogView
    {
        public override void Dispose()
        {
            base.Dispose();
            throw new InvalidOperationException("dialog dispose failed");
        }
    }

    public sealed class ThrowingDisposePromptView : StubPromptView
    {
        public override void Dispose()
        {
            base.Dispose();
            throw new InvalidOperationException("prompt dispose failed");
        }
    }

    public sealed class ThrowingDisposePopoverView : StubPopoverView
    {
        public override void Dispose()
        {
            base.Dispose();
            throw new InvalidOperationException("popover dispose failed");
        }
    }

    public sealed class ThrowingDisposeToastView : StubToastView
    {
        public override void Dispose()
        {
            base.Dispose();
            throw new InvalidOperationException("toast dispose failed");
        }
    }
}
