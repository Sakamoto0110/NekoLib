using PageNav.Contracts.Pages;
using PageNav.Contracts.Plataform;
using PageNav.Core;
using PageNav.Wpf.Adapters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace PageNav.Wpf
{
    public class PlatformAdapter : Contracts.Plataform.IPlatformAdapter
    {
        public bool CanHandle(object host) => host is System.Windows.Controls.Panel;

        public IPageHost CreateHost(object host) =>
            new PageHost((Panel)host);

        public IPageMask CreateMask(object host) =>
            new PageMaskAdapter((Panel)host);

        public IEventDispatcherAdapter CreateEventDispatcher(object host) =>
            new EventDispatcherAdapter();

        public IInteractionBlocker CreateInteractionBlocker(object host) =>
            new InteractionBlocker();

        public ITimerAdapter CreateTimerAdapter() =>
            new TimerAdapter();

        public IDialogService CreateDialogService(object host)
        {
            throw new NotImplementedException();
        }
    }


    public static class WpfBootstrap
    {
        public static void Register() =>
            PlatformRegistry.Register(new PlatformAdapter());
    }
}
