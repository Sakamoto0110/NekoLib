using System.Collections.Generic;
using NekoLib.Navigation.Contracts.Pages;

namespace NekoLib.Navigation.Tests.Unit.Fakes
{
    /// <summary>
    /// Records every attach/detach/addView/removeView call so tests can assert on
    /// the host-level side-effects of navigation without touching real WinForms.
    /// Implements both <see cref="IPageHost"/> and <see cref="IViewHost"/> because
    /// the runtime's host is the same object satisfying both contracts.
    /// </summary>
    public sealed class FakePageHost : IPageHost, IViewHost
    {
        public List<IPageView> Attached { get; } = new();
        public List<IPageView> Detached { get; } = new();
        public List<IPageView> Fronted  { get; } = new();
        public List<object> AddedViews   { get; } = new();
        public List<object> RemovedViews { get; } = new();

        public void Attach(IPageView page)
        {
            Attached.Add(page);
        }

        public void Detach(IPageView page)
        {
            Detached.Add(page);
        }

        public void BringToFront(IPageView page)
        {
            Fronted.Add(page);
        }

        public void AddView(object view)         => AddedViews.Add(view);
        public void RemoveView(object view)      => RemovedViews.Add(view);
        public void BringToFront(object view)    { /* no-op */ }
        public void Focus(object view)           { /* no-op */ }
    }
}
