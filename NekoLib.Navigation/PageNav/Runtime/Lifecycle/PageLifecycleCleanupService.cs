using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Plataform;
using NekoLib.Navigation.Metadata;
using System;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Runtime.Lifecycle
{

    internal sealed class PageLifecycleCleanupService
    {
        private readonly IEventDispatcherAdapter _dispatcher;

        public PageLifecycleCleanupService(IEventDispatcherAdapter dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        /// <summary>
        /// Cleanup a page according to its cache policy.
        /// This method NEVER navigates and NEVER touches UI directly.
        /// </summary>
        public async Task CleanupAsync(
            IPageView page,
            PageDescriptor descriptor,
            bool forceDispose)
        {
            if (page == null || page.IsDisposed)
                return;

            // ------------------------------------------------------------
            // Force dispose always wins (Reset / Shutdown / Fatal paths)
            // ------------------------------------------------------------
            if (forceDispose)
            {
                DisposePage(page);
                return;
            }

            if (descriptor == null)
            {
#if DEBUG
                throw new InvalidOperationException(
                    "PageDescriptor is required unless forceDispose == true.");
#else
            return;
#endif
            }

            // ------------------------------------------------------------
            // Cache policy decision
            // ------------------------------------------------------------
            switch (descriptor.CachePolicy)
            {
                case PageCachePolicy.Disabled:
                    DisposePage(page);
                    break;

                case PageCachePolicy.WeakSingleton:
                    // WeakSingleton: allow GC / idle cleanup to decide later
                    // Do nothing here
                    break;

                case PageCachePolicy.StrongSingleton:
                    // StrongSingleton: never dispose automatically
                    break;

                case PageCachePolicy.Stackable:
                    // Stackable pages are disposed when popped from stack,
                    // not during normal navigation replacement.
                    break;

                default:
#if DEBUG
                    throw new InvalidOperationException(
                        $"Unknown CachePolicy: {descriptor.CachePolicy}");
#endif
                    break;
            }

            await Task.CompletedTask;
        }

        // ------------------------------------------------------------
        // Disposal boundary (single exit point)
        // ------------------------------------------------------------

        private void DisposePage(IPageView page)
        {
            if (page == null || page.IsDisposed)
                return;

            try
            {
                // Dispatch onto UI thread if required by platform
                _dispatcher.Invoke(() =>
                {
                    try
                    {
                        page.Dispose();
                    }
                    catch
                    {
                        // Never throw during cleanup
                    }
                });
            }
            catch
            {
                // Absolute last line of defense
            }
        }
    }
}