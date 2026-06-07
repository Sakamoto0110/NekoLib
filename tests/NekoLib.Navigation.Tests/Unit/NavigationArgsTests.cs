using NekoLib.Navigation.Metadata;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    /// <summary>
    /// Pins the public factory contract for <see cref="NavigationArgs"/>. Lightweight
    /// (no allocations beyond the args themselves) but the surface area is exactly
    /// where call sites interact with the framework, so even trivial regressions —
    /// e.g. flipping a default <c>LoadMode</c> — would matter.
    /// </summary>
    public class NavigationArgsTests
    {
        // -----------------------------------------------------------------
        // Empty
        // -----------------------------------------------------------------

        [Fact]
        public void Empty_IsWellFormed()
        {
            var args = NavigationArgs.Empty;

            Assert.Null(args.Payload);
            Assert.Equal(NavigationLoadMode.ShowImmediately, args.LoadMode);
            Assert.False(args.IsBackNavigation);
        }

        // -----------------------------------------------------------------
        // Default factory
        // -----------------------------------------------------------------

        [Fact]
        public void Default_NoPayload_ShowImmediately_NotBack()
        {
            var args = NavigationArgs.Default();

            Assert.Null(args.Payload);
            Assert.Equal(NavigationLoadMode.ShowImmediately, args.LoadMode);
            Assert.False(args.IsBackNavigation);
        }

        [Fact]
        public void Default_CarriesPayload()
        {
            var payload = new object();
            var args = NavigationArgs.Default(payload);

            Assert.Same(payload, args.Payload);
            Assert.Equal(NavigationLoadMode.ShowImmediately, args.LoadMode);
            Assert.False(args.IsBackNavigation);
        }

        // -----------------------------------------------------------------
        // Transient factory
        // -----------------------------------------------------------------

        [Fact]
        public void Transient_ShowImmediately_NotBack()
        {
            var args = NavigationArgs.Transient();

            Assert.Equal(NavigationLoadMode.ShowImmediately, args.LoadMode);
            Assert.False(args.IsBackNavigation);
        }

        [Fact]
        public void Transient_CarriesPayload()
        {
            var payload = "x";
            var args = NavigationArgs.Transient(payload);

            Assert.Equal("x", args.Payload);
        }

        // -----------------------------------------------------------------
        // Preload factory
        // -----------------------------------------------------------------

        [Fact]
        public void Preload_LoadBeforeShow_NotBack()
        {
            var args = NavigationArgs.Preload();

            Assert.Equal(NavigationLoadMode.LoadBeforeShow, args.LoadMode);
            Assert.False(args.IsBackNavigation);
        }

        [Fact]
        public void Preload_CarriesPayload()
        {
            var payload = 42;
            var args = NavigationArgs.Preload(payload);

            Assert.Equal(42, args.Payload);
        }

        // -----------------------------------------------------------------
        // Background factory
        // -----------------------------------------------------------------

        [Fact]
        public void Background_LoadInBackground_NotBack()
        {
            var args = NavigationArgs.Background();

            Assert.Equal(NavigationLoadMode.LoadInBackground, args.LoadMode);
            Assert.False(args.IsBackNavigation);
        }

        [Fact]
        public void Background_CarriesPayload()
        {
            var payload = new int[] { 1, 2, 3 };
            var args = NavigationArgs.Background(payload);

            Assert.Same(payload, args.Payload);
        }

        // -----------------------------------------------------------------
        // Back factory  (Pass 4 / N-2)
        // -----------------------------------------------------------------

        [Fact]
        public void Back_NoState_IsBackNavigation_NullPayload()
        {
            var args = NavigationArgs.Back();

            Assert.Null(args.Payload);
            Assert.Equal(NavigationLoadMode.ShowImmediately, args.LoadMode);
            Assert.True(args.IsBackNavigation);
        }

        /// <summary>
        /// Pass 4 / N-2: <c>NavigationArgs.Back(state)</c> is the runtime's channel
        /// for replaying a captured <see cref="Contracts.Pages.IPageStateful"/>
        /// blob during back-navigation. The state MUST end up in
        /// <see cref="NavigationArgs.Payload"/> (for backward compatibility) AND
        /// the <see cref="NavigationArgs.IsBackNavigation"/> flag MUST be true so
        /// the runtime knows to call <c>RestoreState</c> on the target page.
        /// </summary>
        [Fact]
        public void Back_CarriesStateAndSetsIsBackNavigation()
        {
            var state = new { Counter = 3 };
            var args = NavigationArgs.Back(state);

            Assert.Same(state, args.Payload);
            Assert.True(args.IsBackNavigation);
            Assert.Equal(NavigationLoadMode.ShowImmediately, args.LoadMode);
        }
    }
}
