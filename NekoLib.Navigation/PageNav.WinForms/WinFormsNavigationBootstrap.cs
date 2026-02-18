//using NekoLib.Navigation.Adapters;
//using NekoLib.Navigation.Contracts.Platform;
//using NekoLib.Navigation.Contracts.Runtime;
//using NekoLib.Navigation.Hosting;
//using NekoLib.Navigation.Runtime.Core;

//using System;
//using System.Windows.Forms;

//namespace NekoLib.Navigation
//{
//    public static class WinFormsNavigationBootstrap
//    {
//        public static NavigationContext UseContext(
//            Control root,
//            int timeoutSeconds = 10)
//        {
//            if(root == null)
//                throw new ArgumentNullException(nameof(root));

//            // Platform services
//            var interactionObserver = new WinFormsInteractionObserver(root);
//            var dispatcher = new WinFormsEventDispatcherAdapter(root);
//            var subscriptions = new WinFormsEventSubscriptionAdapter();
//            var timer = new WinFormsTimerAdapter();
//            var blocker = new WinFormsInteractionBlocker(root);
//            var host = new PanelPageHost((Panel)root);

//            var timeoutAdapter = new WinFormsPageTimeoutAdapter();

//            var timeoutService = new Runtime.Lifecycle.PageTimeoutController(timeoutAdapter, timeoutSeconds);
             
       
//            // Core builder
//            var builder = new NavigationContextBuilder()
//                .UseHost(host)
//                .UseTimeout(timeoutSeconds)
//                .UseService<IEventDispatcherAdapter>(dispatcher)
//                .UseService<ITimerAdapter>(timer)
//                .UseService<IInteractionBlocker>(blocker)
//                .UseService<IEventDispatcherAdapter>(dispatcher)
//                .UseService<IEventSubscriptionAdapter>(subscriptions)
//                .UseService<IInteractionObserverService>(interactionObserver)
//                .UseService< IPageTimeoutService>(timeoutService);

           

//            var context = builder.Build();

//            // Static bind
//            return context;
//        }
//    }
//}
