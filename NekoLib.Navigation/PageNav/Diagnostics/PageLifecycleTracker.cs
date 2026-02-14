/// <summary>
/// TODO: Document this type.
/// Describe responsibility, lifecycle expectations,
/// threading guarantees, and ownership rules.
/// </summary>
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NekoLib.Navigation.Diagnostics
{
    
    public sealed class PageLifecycleInfo
    {
        public Type PageType { get; }
        public string Name { get; }
        public PageCachePolicy CachePolicy { get; }
        public PageLifecycleState State { get; internal set; }
        public DateTime CreatedAt { get; } = DateTime.UtcNow;
        public int EnterCount { get; internal set; }

        internal PageLifecycleInfo(Type type, string name, PageCachePolicy policy)
        {
            PageType = type;
            Name = name;
            CachePolicy = policy;
            State = PageLifecycleState.Created;
        }
    }

    public static class PageLifecycleTracker
    {
        private static readonly object _lock = new object();
        private static readonly Dictionary<IPageView, PageLifecycleInfo> _pages = new Dictionary<IPageView, PageLifecycleInfo>();

        // 🔢 Diagnostics counters
        public static int CreatedCount { get; private set; }
        public static int DisposedCount { get; private set; }
        public static int ActiveCount => _pages.Count;

        public static void Register(IPageView page, PageDescriptor desc)
        {
            lock(_lock)
            {
                if(_pages.ContainsKey(page))
                    return;

                _pages[page] = new PageLifecycleInfo(
                    page.GetType(),
                    page.Name,
                    desc.CachePolicy
                );

                CreatedCount++;
            }
        }

        public static void Update(IPageView page, PageLifecycleState state)
        {
            lock(_lock)
            {
                if(_pages.TryGetValue(page, out var info))
                {
                    info.State = state;
                    if(state == PageLifecycleState.Entered)
                        info.EnterCount++;
                }
            }
        }

        public static void Unregister(IPageView page)
        {
            lock(_lock)
            {
                if(_pages.Remove(page))
                    DisposedCount++;
            }
        }

        // 🔍 Leak detection
        public static IEnumerable<PageLifecycleInfo> SuspectedLeaks(TimeSpan minAge)
        {
            lock(_lock)
            {
                var now = DateTime.UtcNow;
                return _pages.Values
                    .Where(p =>
                        p.CachePolicy == PageCachePolicy.Transient &&
                        p.State != PageLifecycleState.Disposed &&
                        now - p.CreatedAt > minAge)
                    .ToList();
            }
        }

        public static IEnumerable<PageLifecycleInfo> Snapshot()
        {
            lock(_lock)
                return _pages.Values.ToList();
        }
    }
}
