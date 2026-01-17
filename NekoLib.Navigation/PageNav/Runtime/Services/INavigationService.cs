using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;
using System;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Runtime.Services
{

    
    public interface INavigationService
    {
        IPageView Current { get; }
        // -------------------------------------------------------------------------
        // PUBLIC EVENTS (forwarded from context)
        // -------------------------------------------------------------------------

        event Action<IPageView, Type, NavigationArgs> Navigating;
        event Action<IPageView, IPageView, NavigationArgs> Navigated;
        event Action<IPageView, Type, Exception> NavigationFailed;
        event Action<IPageView> CurrentChanged;
        event Action HistoryChanged;




        // -------------------------------------------------------------------------
        // PUBLIC API
        // -------------------------------------------------------------------------

        Task NavigateAsync(Type pageType, NavigationArgs args);
        Task<bool> GoBackAsync();
        Task ResetAsync();




    }
    internal interface INavigationShellEvents
    {
        event Action<IPageView> FirstPageAttached;
        event Action NoPageAttached;
        event Action NoPageVisible;
    }

    public static class NavigationServiceExtensions
    {
        public static Task NavigateAsync<TPage>(
            this INavigationService nav,
            NavigationArgs args)
            => nav.NavigateAsync(typeof(TPage), args);
    }
}