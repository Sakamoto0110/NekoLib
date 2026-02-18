using PageNav.Contracts.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PageNav.Wpf
{
    public abstract class PageView : UserControl, IPageView
    {
        protected IPageHost Host;
        public event Action<object> ChildViewAdded;
        public event Action<object> ChildViewRemoved;
        public event Action OnDetachEvent;

        public abstract string Name { get; }

        public bool IsVisible
        {
            get => Visibility == System.Windows.Visibility.Visible;
            set => Visibility = value ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        public bool IsLocked { get; set; }

        public object NativeView => this;

        public bool DesignMode => false;

        public bool IsDisposed => false;

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            AddLogicalTreeHandlers(this);
        }

        private void AddLogicalTreeHandlers(DependencyObject root)
        {
            this.AddHandler(FrameworkElement.LoadedEvent, new RoutedEventHandler((s, e) => {
                if(e.Source is FrameworkElement fe)
                    ChildViewAdded?.Invoke(fe);
            }));

            this.AddHandler(FrameworkElement.UnloadedEvent, new RoutedEventHandler((s, e) => {
                if(e.Source is FrameworkElement fe)
                    ChildViewRemoved?.Invoke(fe);
            }));
        }
        public virtual void OnAttach(IPageHost host)
        {
            Host = host;
            host.AddView(this);
        }

        public virtual void OnDetach()
        {
            Host?.RemoveView(this);
        }

        public abstract Task ReloadAsync(object args);

        public virtual void Enable() => IsEnabled = true;
        public virtual void Disable() => IsEnabled = false;

        public virtual async Task ReleaseResources()
        {
            // override as needed
        }

        public abstract void Dispose();

        public virtual async Task Reload(object args)
        {
            // override as needed
        }
    }
}
