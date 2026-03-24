// FILE: NekoLib.Navigation.Winforms.Hosting/PopupView.cs
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.WinForms.Hosting;
using System.Threading.Tasks;

namespace NekoLib.Navigation.WinForms.Hosting
{
    public class PopupView : PageView, IPageOverlay
    {
        // 1. Single, empty constructor. The VS Designer loves this.
        public PopupView()
        {
        }

        public virtual Task OnOverlayOpenedAsync(object payload) => Task.CompletedTask;

        public virtual Task OnOverlayClosingAsync() => Task.CompletedTask;

        protected void ClosePopup()
        {
            // 2. Use the static facade we just built instead of DI
            NavigationService.CloseTopOverlay();
        }
    }
}