using System;
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

        public Exception AddViewException { get; set; }
        public Exception RemoveViewException { get; set; }
        public Exception BringViewToFrontException { get; set; }
        public Exception FocusException { get; set; }
        public Action<string, IPageView> OperationObserved { get; set; }

        public void Attach(IPageView page)
        {
            Attached.Add(page);
            OperationObserved?.Invoke("attach", page);
        }

        public void Detach(IPageView page)
        {
            Detached.Add(page);
            OperationObserved?.Invoke("detach", page);
        }

        public void BringToFront(IPageView page)
        {
            Fronted.Add(page);
            OperationObserved?.Invoke("front", page);
        }

        public void AddView(object view)
        {
            if (AddViewException != null)
                throw AddViewException;

            AddedViews.Add(view);
        }

        public void RemoveView(object view)
        {
            RemovedViews.Add(view);

            if (RemoveViewException != null)
                throw RemoveViewException;
        }

        public void BringToFront(object view)
        {
            if (BringViewToFrontException != null)
                throw BringViewToFrontException;
        }

        public void Focus(object view)
        {
            if (FocusException != null)
                throw FocusException;
        }
    }
}
