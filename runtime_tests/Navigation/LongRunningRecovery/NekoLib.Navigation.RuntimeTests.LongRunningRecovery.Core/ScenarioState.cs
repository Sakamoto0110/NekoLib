#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Metadata.Attributes;

namespace NekoLib.Navigation.RuntimeTests.LongRunningRecovery
{
    public enum PageFaultPoint
    {
        None = 0,
        Creation = 1,
        Load = 2,
        Enter = 3,
        Leave = 4,
        Show = 5,
        Hide = 6
    }

    public enum ScenarioLoadBehavior
    {
        Complete = 0,
        Fail = 1,
        Block = 2
    }

    public sealed class ScenarioState
    {
        private readonly ConcurrentDictionary<Type, PageTypeMetrics> _types =
            new ConcurrentDictionary<Type, PageTypeMetrics>();
        private readonly object _faultSync = new object();
        private readonly object _loadSync = new object();
        private readonly Dictionary<Type, ScenarioLoadBehavior> _loadBehaviors =
            new Dictionary<Type, ScenarioLoadBehavior>();
        private readonly Dictionary<Type, TaskCompletionSource<bool>> _loadGates =
            new Dictionary<Type, TaskCompletionSource<bool>>();

        private Type? _faultType;
        private PageFaultPoint _faultPoint;
        private long _sequence;
        private long _activeBackground;
        private long _apiRequests;
        private long _apiTerminals;
        private long _guardDenials;
        private long _navigationFailures;

        public bool DenyIdle;
        public TimeSpan GuardDelay = TimeSpan.FromSeconds(31);

        public long ActiveBackground => Interlocked.Read(ref _activeBackground);
        public long ApiRequests => Interlocked.Read(ref _apiRequests);
        public long ApiTerminals => Interlocked.Read(ref _apiTerminals);
        public long GuardDenials => Interlocked.Read(ref _guardDenials);
        public long NavigationFailures => Interlocked.Read(ref _navigationFailures);
        public long NextSequence() => Interlocked.Increment(ref _sequence);

        public IReadOnlyCollection<PageTypeMetrics> Types => _types.Values.ToArray();

        public PageProbe CreateProbe(Type pageType)
        {
            ThrowIfFault(pageType, PageFaultPoint.Creation);
            PageTypeMetrics metrics = _types.GetOrAdd(pageType, type => new PageTypeMetrics(type));
            return metrics.Create(this);
        }

        public PageTypeMetrics Metrics(Type pageType) =>
            _types.GetOrAdd(pageType, type => new PageTypeMetrics(type));

        public IDisposable Inject(Type pageType, PageFaultPoint point)
        {
            lock (_faultSync)
            {
                if (_faultPoint != PageFaultPoint.None)
                    throw new InvalidOperationException("A scenario page fault is already active.");
                _faultType = pageType;
                _faultPoint = point;
            }

            return new DelegateDisposable(() =>
            {
                lock (_faultSync)
                {
                    _faultType = null;
                    _faultPoint = PageFaultPoint.None;
                }
            });
        }

        internal void ThrowIfFault(Type pageType, PageFaultPoint point)
        {
            lock (_faultSync)
            {
                if (_faultPoint == point && _faultType == pageType)
                {
                    throw new ScenarioInjectedException(
                        "Injected " + point.ToString().ToLowerInvariant() +
                        " failure for " + pageType.Name + ".");
                }
            }
        }

        public void ConfigureLoad(Type pageType, ScenarioLoadBehavior behavior)
        {
            lock (_loadSync)
            {
                _loadBehaviors[pageType] = behavior;
                if (behavior == ScenarioLoadBehavior.Block)
                {
                    _loadGates[pageType] = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                }
                else
                {
                    _loadGates.Remove(pageType);
                }
            }
        }

        public void ReleaseLoad(Type pageType)
        {
            TaskCompletionSource<bool>? gate;
            lock (_loadSync) _loadGates.TryGetValue(pageType, out gate);
            gate?.TrySetResult(true);
        }

        internal async Task ExecuteLoadAsync(PageProbe probe)
        {
            ScenarioLoadBehavior behavior;
            Task? wait = null;
            lock (_loadSync)
            {
                if (!_loadBehaviors.TryGetValue(probe.PageType, out behavior))
                    behavior = ScenarioLoadBehavior.Complete;
                if (behavior == ScenarioLoadBehavior.Block &&
                    _loadGates.TryGetValue(probe.PageType, out TaskCompletionSource<bool>? gate))
                    wait = gate.Task;
            }

            Interlocked.Increment(ref _activeBackground);
            probe.Record("load-start");
            try
            {
                ThrowIfFault(probe.PageType, PageFaultPoint.Load);
                if (behavior == ScenarioLoadBehavior.Fail)
                    throw new ScenarioInjectedException("Injected background load failure.");
                if (wait != null) await wait;
                probe.Record("load-complete");
                probe.Metrics.Loaded();
            }
            finally
            {
                Interlocked.Decrement(ref _activeBackground);
            }
        }

        public void ClearFaultsAndReleaseLoads()
        {
            lock (_faultSync)
            {
                _faultType = null;
                _faultPoint = PageFaultPoint.None;
                DenyIdle = false;
            }

            lock (_loadSync)
            {
                foreach (TaskCompletionSource<bool> gate in _loadGates.Values)
                    gate.TrySetResult(true);
                _loadGates.Clear();
                _loadBehaviors.Clear();
            }
        }

        public void RequestStarted() => Interlocked.Increment(ref _apiRequests);
        public void RequestCompleted() => Interlocked.Increment(ref _apiTerminals);
        public void GuardDeniedObserved() => Interlocked.Increment(ref _guardDenials);
        public void NavigationFailureObserved() => Interlocked.Increment(ref _navigationFailures);

        public int AlivePageCount => _types.Values.Sum(metrics => metrics.AliveCount);
        public long ConstructedPageCount => _types.Values.Sum(metrics => metrics.Constructed);
        public long DisposedPageCount => _types.Values.Sum(metrics => metrics.Disposed);
        public long AttachedPageCount => _types.Values.Sum(metrics => metrics.Attached);
        public long VisiblePageCount => _types.Values.Sum(metrics => metrics.Visible);

        public void ForceCollection()
        {
            for (int i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                if (AlivePageCount == 0) break;
                Thread.Sleep(20);
            }
        }
    }

    public sealed class PageTypeMetrics
    {
        private readonly object _sync = new object();
        private readonly List<WeakReference> _instances = new List<WeakReference>();
        private long _constructed;
        private long _disposed;
        private long _attached;
        private long _visible;
        private long _loaded;
        private long _applied;
        private int _nextId;

        internal PageTypeMetrics(Type pageType) { PageType = pageType; }

        public Type PageType { get; }
        public long Constructed => Interlocked.Read(ref _constructed);
        public long Disposed => Interlocked.Read(ref _disposed);
        public long Attached => Interlocked.Read(ref _attached);
        public long Visible => Interlocked.Read(ref _visible);
        public long LoadedCount => Interlocked.Read(ref _loaded);
        public long AppliedCount => Interlocked.Read(ref _applied);

        public int AliveCount
        {
            get
            {
                lock (_sync)
                {
                    int alive = 0;
                    for (int i = _instances.Count - 1; i >= 0; i--)
                    {
                        if (_instances[i].IsAlive) alive++;
                        else _instances.RemoveAt(i);
                    }
                    return alive;
                }
            }
        }

        internal PageProbe Create(ScenarioState state)
        {
            PageProbe probe = new PageProbe(state, this, Interlocked.Increment(ref _nextId));
            lock (_sync) _instances.Add(new WeakReference(probe));
            Interlocked.Increment(ref _constructed);
            return probe;
        }

        internal void DisposedOne() => Interlocked.Increment(ref _disposed);
        internal void AttachedOne() => Interlocked.Increment(ref _attached);
        internal void DetachedOne() => Decrement(ref _attached);
        internal void ShownOne() => Interlocked.Increment(ref _visible);
        internal void HiddenOne() => Decrement(ref _visible);
        internal void Loaded() => Interlocked.Increment(ref _loaded);
        internal void Applied() => Interlocked.Increment(ref _applied);

        private static void Decrement(ref long value)
        {
            while (true)
            {
                long current = Interlocked.Read(ref value);
                if (current <= 0) return;
                if (Interlocked.CompareExchange(ref value, current - 1, current) == current) return;
            }
        }
    }

    public sealed class PageProbe
    {
        private readonly ScenarioState _state;
        private readonly object _sync = new object();
        private readonly List<KeyValuePair<long, string>> _events =
            new List<KeyValuePair<long, string>>();
        private int _disposed;
        private int _attached;
        private int _visible;
        private int _stateValue;

        internal PageProbe(ScenarioState state, PageTypeMetrics metrics, int instanceId)
        {
            _state = state;
            Metrics = metrics;
            InstanceId = instanceId;
            Record("constructed");
        }

        public Type PageType => Metrics.PageType;
        public PageTypeMetrics Metrics { get; }
        public int InstanceId { get; }
        public int StateValue { get => _stateValue; set => _stateValue = value; }
        public int RestoredStateValue { get; private set; }

        public void Record(string name)
        {
            lock (_sync)
                _events.Add(new KeyValuePair<long, string>(_state.NextSequence(), name));
        }

        public long FirstSequence(string name)
        {
            lock (_sync)
            {
                foreach (KeyValuePair<long, string> item in _events)
                    if (string.Equals(item.Value, name, StringComparison.Ordinal)) return item.Key;
            }
            return -1;
        }

        public long LastSequence(string name)
        {
            lock (_sync)
            {
                for (int i = _events.Count - 1; i >= 0; i--)
                    if (string.Equals(_events[i].Value, name, StringComparison.Ordinal))
                        return _events[i].Key;
            }
            return -1;
        }

        public Task EnterAsync(NavigationArgs args)
        {
            _state.ThrowIfFault(PageType, PageFaultPoint.Enter);
            Record("enter");
            return CompletedTask;
        }

        public Task LeaveAsync()
        {
            _state.ThrowIfFault(PageType, PageFaultPoint.Leave);
            Record("leave");
            return CompletedTask;
        }

        public Task LoadAsync() => _state.ExecuteLoadAsync(this);

        public Task ApplyAsync()
        {
            Record("apply");
            Metrics.Applied();
            return CompletedTask;
        }

        public object CaptureState()
        {
            Record("capture");
            return _stateValue;
        }

        public void RestoreState(object? value)
        {
            RestoredStateValue = value is int integer ? integer : -1;
            Record("restore");
        }

        public void Attached()
        {
            if (Interlocked.Exchange(ref _attached, 1) == 0) Metrics.AttachedOne();
            Record("attach");
        }

        public void Detached()
        {
            if (Interlocked.Exchange(ref _attached, 0) != 0) Metrics.DetachedOne();
            Record("detach");
        }

        public void Shown()
        {
            _state.ThrowIfFault(PageType, PageFaultPoint.Show);
            if (Interlocked.Exchange(ref _visible, 1) == 0) Metrics.ShownOne();
            Record("show");
        }

        public void Hidden()
        {
            _state.ThrowIfFault(PageType, PageFaultPoint.Hide);
            if (Interlocked.Exchange(ref _visible, 0) != 0) Metrics.HiddenOne();
            Record("hide");
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            if (Interlocked.Exchange(ref _visible, 0) != 0) Metrics.HiddenOne();
            if (Interlocked.Exchange(ref _attached, 0) != 0) Metrics.DetachedOne();
            Record("dispose");
            Metrics.DisposedOne();
        }

        private static Task CompletedTask
        {
            get
            {
#if NET481
                return Task.FromResult(0);
#else
                return Task.CompletedTask;
#endif
            }
        }
    }

    public static class ScenarioStateSlot
    {
        private static ScenarioState? _current;
        public static ScenarioState Current => _current ??
            throw new InvalidOperationException("The E3-NAV scenario state is not installed.");
        public static void Install(ScenarioState state) => _current = state ??
            throw new ArgumentNullException(nameof(state));
        public static void Clear() => _current = null;
        public static bool IsInstalled => _current != null;
    }

    public enum ScenarioGuardBehavior
    {
        AllowUnlessIdleDenied = 0,
        Timeout = 1,
        Throw = 2,
        Redirect = 3
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public sealed class ScenarioGuardAttribute : GuardAttribute
    {
        private readonly ScenarioGuardBehavior _behavior;
        private readonly Type? _redirect;

        public ScenarioGuardAttribute(ScenarioGuardBehavior behavior)
        {
            _behavior = behavior;
        }

        public ScenarioGuardAttribute(ScenarioGuardBehavior behavior, Type redirect)
        {
            _behavior = behavior;
            _redirect = redirect;
        }

        public override IGuard CreateGuard() => new ScenarioGuard(_behavior, _redirect);
    }

    internal sealed class ScenarioGuard : IGuard
    {
        private readonly ScenarioGuardBehavior _behavior;
        private readonly Type? _redirect;

        public ScenarioGuard(ScenarioGuardBehavior behavior, Type? redirect)
        {
            _behavior = behavior;
            _redirect = redirect;
        }

        public async Task<GuardResult> EvaluateAsync(GuardContext context)
        {
            switch (_behavior)
            {
                case ScenarioGuardBehavior.AllowUnlessIdleDenied:
                    return ScenarioStateSlot.Current.DenyIdle
                        ? GuardResult.Deny("The scenario denied the idle transition.")
                        : GuardResult.Allow();

                case ScenarioGuardBehavior.Timeout:
                    await Task.Delay(ScenarioStateSlot.Current.GuardDelay);
                    return GuardResult.Allow();

                case ScenarioGuardBehavior.Throw:
                    throw new ScenarioInjectedException("The scenario guard threw.");

                case ScenarioGuardBehavior.Redirect:
                    if (_redirect == null)
                        throw new InvalidOperationException("A redirect guard requires a target.");
                    return GuardResult.Redirect(_redirect, "Scenario redirect.");

                default:
                    return GuardResult.Allow();
            }
        }
    }

    public sealed class ScenarioInjectedException : Exception
    {
        public ScenarioInjectedException(string message) : base(message) { }
    }

    internal sealed class DelegateDisposable : IDisposable
    {
        private Action? _dispose;
        public DelegateDisposable(Action dispose) { _dispose = dispose; }
        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}
