using System;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms
{
    /// <summary>
    /// Process-wide access to the workspace and the view-models.
    /// <para/>
    /// This is a static locator rather than constructor injection, and that is forced
    /// by two requirements pulling in opposite directions. The WinForms designer can
    /// only instantiate a type through a parameterless constructor, so a page cannot
    /// take its dependencies as constructor arguments and still be designable.
    /// Navigation's <c>PageFactory</c> agrees - its fallback path is exactly a
    /// default-constructor call. Pages therefore pull what they need, and they pull
    /// it lazily so that a designer-hosted instance never touches a database.
    /// </summary>
    public static class AppServices
    {
        private static FarmWorkspace _workspace;

        /// <summary>
        /// True once <see cref="Start"/> has run. Pages check this before touching
        /// anything: at design time it is false, and it must stay harmless.
        /// </summary>
        public static bool IsRunning => _workspace != null;

        public static FarmWorkspace Workspace =>
            _workspace ?? throw new InvalidOperationException(
                "AppServices.Start() não foi chamado.");

        public static ViewModels ViewModelsBundle { get; private set; }

        /// <summary>Creates the workspace and the shared view-models.</summary>
        public static IDisposable Start()
        {
            if (_workspace != null)
                throw new InvalidOperationException("AppServices já foi iniciado.");

            _workspace = new FarmWorkspace();
            ViewModelsBundle = new ViewModels(_workspace);

            // Every view-model re-reads connection-derived state from one place, so a
            // connect or disconnect on the Connection page is immediately visible to
            // pages that are not even attached yet.
            _workspace.ConnectionChanged += ViewModelsBundle.NotifyConnectionChanged;

            return new Shutdown();
        }

        private sealed class Shutdown : IDisposable
        {
            public void Dispose()
            {
                if (_workspace == null) return;

                _workspace.ConnectionChanged -= ViewModelsBundle.NotifyConnectionChanged;
                _workspace.Dispose();
                _workspace = null;
                ViewModelsBundle = null;
            }
        }
    }

    /// <summary>
    /// The view-models, created once and shared by the pages that display them.
    /// They outlive individual page instances on purpose: a page can be transient and
    /// still show the same table selection when the user navigates back to it.
    /// </summary>
    public sealed class ViewModels
    {
        public ViewModels(FarmWorkspace workspace)
        {
            Connection = new Core.ViewModels.ConnectionViewModel(workspace);
            Browse = new Core.ViewModels.BrowseViewModel(workspace);
            RawQuery = new Core.ViewModels.RawQueryViewModel(workspace);
            Stock = new Core.ViewModels.StockViewModel(workspace);
            Log = new Core.ViewModels.LogViewModel(workspace);
        }

        public Core.ViewModels.ConnectionViewModel Connection { get; }
        public Core.ViewModels.BrowseViewModel Browse { get; }
        public Core.ViewModels.RawQueryViewModel RawQuery { get; }
        public Core.ViewModels.StockViewModel Stock { get; }
        public Core.ViewModels.LogViewModel Log { get; }

        public void NotifyConnectionChanged()
        {
            Connection.OnConnectionChanged();
            Browse.OnConnectionChanged();
            RawQuery.OnConnectionChanged();
            Stock.OnConnectionChanged();
            Log.OnConnectionChanged();
        }
    }
}
