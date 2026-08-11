#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NekoLib.Inspection;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Bootstrap;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Runtime.Core;

namespace NekoLib.Navigation.RuntimeTests.LongRunningRecovery
{
    public interface IScenarioProbedPage
    {
        PageProbe Probe { get; }
    }

    public sealed class ScenarioPageCatalog
    {
        public Type Idle = null!;
        public Type Transient = null!;
        public Type Strong = null!;
        public Type Weak = null!;
        public Type KeepAttached = null!;
        public Type LoadBefore = null!;
        public Type ShowImmediately = null!;
        public Type Background = null!;
        public Type Authenticated = null!;
        public Type Role = null!;
        public Type Permission = null!;
        public Type RedirectToIdle = null!;
        public Type ControlledIdle = null!;
        public Type Fault = null!;
        public Type GuardTimeout = null!;
        public Type GuardThrow = null!;
        public Type CycleA = null!;
        public Type DepthStart = null!;

        public IReadOnlyList<Type> All = new Type[0];
    }

    public static class ScenarioPageRegistration
    {
        public static void Register(PageMetadataBuilder builder, ScenarioPageCatalog pages)
        {
            foreach (Type page in pages.All) builder.RegisterType(page);

            builder.RegisterType(pages.Idle, descriptor => descriptor.Role = PageRole.Idle);
            builder.RegisterType(pages.Strong,
                descriptor => descriptor.ReusePolicy = PageReusePolicy.StrongSingleton);
            builder.RegisterType(pages.Weak,
                descriptor => descriptor.ReusePolicy = PageReusePolicy.WeakSingleton);
            builder.RegisterType(pages.KeepAttached,
                descriptor => descriptor.ReusePolicy = PageReusePolicy.StrongSingleton);
            builder.RegisterType(pages.LoadBefore,
                descriptor => descriptor.LoadMode = NavigationLoadMode.LoadBeforeShow);
            builder.RegisterType(pages.ShowImmediately,
                descriptor => descriptor.LoadMode = NavigationLoadMode.ShowImmediately);
            builder.RegisterType(pages.Background,
                descriptor => descriptor.LoadMode = NavigationLoadMode.LoadInBackground);
            builder.RegisterType(pages.Fault,
                descriptor => descriptor.LoadMode = NavigationLoadMode.LoadBeforeShow);
        }
    }

    public sealed class SurfaceDirective
    {
        public bool Complete = true;
        public bool ThrowOnShow;
        public bool BooleanResult = true;
        public string? TextResult = "scenario-result";
    }

    public interface IScenarioPlatform
    {
        string PlatformId { get; }
        string DisplayName { get; }
        Type AdapterMarkerType { get; }
        ScenarioPageCatalog Pages { get; }
        ScenarioPlatformControls Controls { get; }
        int NativeChildCount { get; }

        NavigationContext Start(
            ScenarioState state,
            InspectionRuntime inspection,
            ScenarioOptions options);

        Task<bool> ShowDialogAsync(SurfaceDirective directive);
        Task<string?> ShowPromptAsync(SurfaceDirective directive);
        Task<bool> ShowPopoverAsync(SurfaceDirective directive);
        void ShowToast(SurfaceDirective directive, int durationMilliseconds);
        Task<Exception?> ShowBindingFailureAsync();
        Task YieldUiAsync();
        void ReleaseScenarioGlobals();
    }

    public sealed class ScenarioPlatformMetrics
    {
        private long _viewsAdded;
        private long _viewsRemoved;
        private long _viewsLive;
        private long _modalViewsLive;
        private long _maxModalViews;
        private long _timerStarts;
        private long _timerStops;
        private long _timerTicks;
        private long _interactionPulses;
        private long _dispatcherRejections;

        public long ViewsAdded => System.Threading.Interlocked.Read(ref _viewsAdded);
        public long ViewsRemoved => System.Threading.Interlocked.Read(ref _viewsRemoved);
        public long ViewsLive => System.Threading.Interlocked.Read(ref _viewsLive);
        public long ModalViewsLive => System.Threading.Interlocked.Read(ref _modalViewsLive);
        public long MaxModalViews => System.Threading.Interlocked.Read(ref _maxModalViews);
        public long TimerStarts => System.Threading.Interlocked.Read(ref _timerStarts);
        public long TimerStops => System.Threading.Interlocked.Read(ref _timerStops);
        public long TimerTicks => System.Threading.Interlocked.Read(ref _timerTicks);
        public long InteractionPulses => System.Threading.Interlocked.Read(ref _interactionPulses);
        public long DispatcherRejections => System.Threading.Interlocked.Read(ref _dispatcherRejections);

        public void ViewAdded()
        {
            System.Threading.Interlocked.Increment(ref _viewsAdded);
            System.Threading.Interlocked.Increment(ref _viewsLive);
        }

        public void ViewRemoved()
        {
            System.Threading.Interlocked.Increment(ref _viewsRemoved);
            DecrementNonNegative(ref _viewsLive);
        }

        internal void ModalAdded()
        {
            long live = System.Threading.Interlocked.Increment(ref _modalViewsLive);
            while (true)
            {
                long maximum = System.Threading.Interlocked.Read(ref _maxModalViews);
                if (live <= maximum ||
                    System.Threading.Interlocked.CompareExchange(ref _maxModalViews, live, maximum) == maximum)
                    break;
            }
        }

        internal void ModalRemoved() => DecrementNonNegative(ref _modalViewsLive);
        internal void TimerStarted() => System.Threading.Interlocked.Increment(ref _timerStarts);
        internal void TimerStopped() => System.Threading.Interlocked.Increment(ref _timerStops);
        internal void TimerTicked() => System.Threading.Interlocked.Increment(ref _timerTicks);
        internal void InteractionPulsed() => System.Threading.Interlocked.Increment(ref _interactionPulses);
        internal void DispatcherRejected() => System.Threading.Interlocked.Increment(ref _dispatcherRejections);

        private static void DecrementNonNegative(ref long field)
        {
            while (true)
            {
                long value = System.Threading.Interlocked.Read(ref field);
                if (value <= 0) return;
                if (System.Threading.Interlocked.CompareExchange(ref field, value - 1, value) == value) return;
            }
        }
    }

    public sealed class ScenarioPlatformControls
    {
        private ScenarioTimer? _timer;
        private ScenarioInteractionObserver? _observer;
        private int _failNextViewRemoval;

        public ScenarioPlatformMetrics Metrics { get; } = new ScenarioPlatformMetrics();
        public bool RejectDispatch { get; set; }

        public IEventDispatcherAdapter WrapDispatcher(IEventDispatcherAdapter inner) =>
            new ScenarioDispatcher(inner, this);

        public ITimerAdapter WrapTimer(ITimerAdapter inner)
        {
            _timer = new ScenarioTimer(inner, Metrics);
            return _timer;
        }

        public NekoLib.Navigation.Contracts.Runtime.IInteractionObserverService WrapObserver(
            NekoLib.Navigation.Contracts.Runtime.IInteractionObserverService inner)
        {
            _observer = new ScenarioInteractionObserver(inner, Metrics);
            return _observer;
        }

        public IInteractionBlocker WrapBlocker(IInteractionBlocker inner) =>
            new ScenarioInteractionBlocker(inner, Metrics);

        public void PulseInteraction() => _observer?.Pulse();
        public void FireIdleTick() => _timer?.FireNow();
        public bool TimerIsRunning => _timer?.IsRunning == true;

        public void FailNextViewRemoval() =>
            System.Threading.Interlocked.Exchange(ref _failNextViewRemoval, 1);

        public bool ConsumeViewRemovalFailure() =>
            System.Threading.Interlocked.Exchange(ref _failNextViewRemoval, 0) != 0;
    }

    internal sealed class ScenarioDispatcher : IEventDispatcherAdapter
    {
        private readonly IEventDispatcherAdapter _inner;
        private readonly ScenarioPlatformControls _controls;

        public ScenarioDispatcher(IEventDispatcherAdapter inner, ScenarioPlatformControls controls)
        {
            _inner = inner;
            _controls = controls;
        }

        public void Invoke(Action action)
        {
            RejectIfRequested();
            _inner.Invoke(action);
        }

        public void BeginInvoke(Action action)
        {
            RejectIfRequested();
            _inner.BeginInvoke(action);
        }

        private void RejectIfRequested()
        {
            if (!_controls.RejectDispatch) return;
            _controls.Metrics.DispatcherRejected();
            throw new InvalidOperationException("The scenario made the UI dispatcher unavailable.");
        }
    }

    internal sealed class ScenarioTimer : ITimerAdapter
    {
        private readonly ITimerAdapter _inner;
        private readonly ScenarioPlatformMetrics _metrics;
        private readonly Action _onInnerTick;
        private bool _running;

        public ScenarioTimer(ITimerAdapter inner, ScenarioPlatformMetrics metrics)
        {
            _inner = inner;
            _metrics = metrics;
            _onInnerTick = RaiseTick;
            _inner.Tick += _onInnerTick;
        }

        public int IntervalMilliseconds
        {
            get => _inner.IntervalMilliseconds;
            set => _inner.IntervalMilliseconds = value;
        }

        public event Action? Tick;
        public bool IsRunning => _running;

        public void Start()
        {
            _running = true;
            _metrics.TimerStarted();
            _inner.Start();
        }

        public void Stop()
        {
            _running = false;
            _metrics.TimerStopped();
            _inner.Stop();
        }

        public void FireNow()
        {
            if (_running) RaiseTick();
        }

        private void RaiseTick()
        {
            if (!_running) return;
            _metrics.TimerTicked();
            Tick?.Invoke();
        }

        public void Dispose()
        {
            _running = false;
            _inner.Tick -= _onInnerTick;
            _inner.Dispose();
        }
    }

    internal sealed class ScenarioInteractionObserver :
        NekoLib.Navigation.Contracts.Runtime.IInteractionObserverService,
        IDisposable
    {
        private readonly NekoLib.Navigation.Contracts.Runtime.IInteractionObserverService _inner;
        private readonly ScenarioPlatformMetrics _metrics;
        private readonly Action _forward;

        public ScenarioInteractionObserver(
            NekoLib.Navigation.Contracts.Runtime.IInteractionObserverService inner,
            ScenarioPlatformMetrics metrics)
        {
            _inner = inner;
            _metrics = metrics;
            _forward = () => InteractionDetected?.Invoke();
            _inner.InteractionDetected += _forward;
        }

        public event Action? InteractionDetected;

        public void Pulse()
        {
            _metrics.InteractionPulsed();
            InteractionDetected?.Invoke();
        }

        public void Dispose()
        {
            _inner.InteractionDetected -= _forward;
            if (_inner is IDisposable disposable) disposable.Dispose();
            InteractionDetected = null;
        }
    }

    internal sealed class ScenarioInteractionBlocker : IPageAwareInteractionBlocker
    {
        private readonly IInteractionBlocker _inner;
        private readonly IPageAwareInteractionBlocker? _aware;
        private readonly ScenarioPlatformMetrics _metrics;
        private readonly HashSet<object> _modal = new HashSet<object>();

        public ScenarioInteractionBlocker(IInteractionBlocker inner, ScenarioPlatformMetrics metrics)
        {
            _inner = inner;
            _aware = inner as IPageAwareInteractionBlocker;
            _metrics = metrics;
        }

        public void Block() => _inner.Block();
        public void Unblock() => _inner.Unblock();

        public void OnViewAdded(object view, bool isModalSurface)
        {
            _aware?.OnViewAdded(view, isModalSurface);
            if (isModalSurface && _modal.Add(view)) _metrics.ModalAdded();
        }

        public void OnViewRemoved(object view)
        {
            _aware?.OnViewRemoved(view);
            if (_modal.Remove(view)) _metrics.ModalRemoved();
        }
    }
}
