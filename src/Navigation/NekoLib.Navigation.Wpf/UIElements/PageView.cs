using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace NekoLib.Navigation.Wpf
{
    /// <summary>
    /// Base class for WPF pages. Provides the minimal <see cref="IPageView"/> surface plus
    /// optional lifecycle callbacks. Navigation owns attach/detach via <see cref="IPageHost"/>;
    /// the page does not manage its own hosting.
    /// </summary>
    public abstract class PageView : UserControl, IPageView, IPageLifecycle
    {
        public object NativeView => this;

        public bool IsDisposed { get; private set; }

        public virtual Task OnNavigatedToAsync(NavigationArgs args) => Task.CompletedTask;

        public virtual Task OnNavigatedFromAsync() => Task.CompletedTask;

        public virtual void Dispose() => IsDisposed = true;
    }
}
