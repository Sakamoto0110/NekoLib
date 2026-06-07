using System.Linq;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Runtime.History;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    /// <summary>
    /// Unit tests for <see cref="NavigationHistory"/>. These pin the back/forward
    /// stack semantics that the runtime relies on. The interaction bug that became
    /// NEW-7 (history double-push on back-nav) lived in <c>NavigationRuntime</c>,
    /// not here, but having confidence in the underlying storage makes the runtime
    /// fix easier to reason about — and tests around the runtime can lean on this
    /// known-good baseline.
    /// </summary>
    public class NavigationHistoryTests
    {
        private static PageHistoryEntry Entry(string name, object state = null)
            => new PageHistoryEntry(typeof(Stub), name, state);

        // Surface type for the (Type pageType) ctor parameter — anything non-null.
        private sealed class Stub { }

        // -----------------------------------------------------------------
        // Empty state
        // -----------------------------------------------------------------

        [Fact]
        public void Empty_CannotGoBackOrForward()
        {
            var h = new NavigationHistory();

            Assert.False(h.CanGoBack);
            Assert.False(h.CanGoForward);
            Assert.False(h.TryPopBack(out var popped));
            Assert.Null(popped);
        }

        // -----------------------------------------------------------------
        // Record
        // -----------------------------------------------------------------

        [Fact]
        public void Record_AddsToBack_AndEnablesCanGoBack()
        {
            var h = new NavigationHistory();

            h.Record(Entry("A"));

            Assert.True(h.CanGoBack);
            Assert.False(h.CanGoForward);
            Assert.Single(h.HistoryBack);
            Assert.Empty(h.HistoryForward);
        }

        [Fact]
        public void Record_Null_IsIgnored()
        {
            var h = new NavigationHistory();

            h.Record(null);

            Assert.False(h.CanGoBack);
            Assert.Empty(h.HistoryBack);
        }

        [Fact]
        public void Record_ClearsForwardStack_BrowserSemantics()
        {
            // Setup: navigate forward A, B; then go back so forward has [B].
            var h = new NavigationHistory();
            h.Record(Entry("A"));
            h.Record(Entry("B"));
            h.TryPopBack(out var popped);          // back = [A], current implied = B
            h.PushForward(popped);                 // forward = [B's-replacement-here]
            Assert.True(h.CanGoForward);

            // Forward navigation to a NEW page (C) must wipe the forward stack —
            // there is no longer anything to "redo".
            h.Record(Entry("C"));

            Assert.True(h.CanGoBack);
            Assert.False(h.CanGoForward);
            Assert.Empty(h.HistoryForward);
        }

        // -----------------------------------------------------------------
        // TryPopBack
        // -----------------------------------------------------------------

        [Fact]
        public void TryPopBack_ReturnsMostRecent_LIFO()
        {
            var h = new NavigationHistory();
            h.Record(Entry("A"));
            h.Record(Entry("B"));
            h.Record(Entry("C"));

            Assert.True(h.TryPopBack(out var first));
            Assert.Equal("C", first.PageName);

            Assert.True(h.TryPopBack(out var second));
            Assert.Equal("B", second.PageName);

            Assert.True(h.TryPopBack(out var third));
            Assert.Equal("A", third.PageName);

            Assert.False(h.TryPopBack(out var none));
            Assert.Null(none);
        }

        [Fact]
        public void TryPopBack_DrainsBackStack()
        {
            var h = new NavigationHistory();
            h.Record(Entry("A"));
            h.Record(Entry("B"));

            h.TryPopBack(out _);
            h.TryPopBack(out _);

            Assert.False(h.CanGoBack);
            Assert.Empty(h.HistoryBack);
        }

        // -----------------------------------------------------------------
        // PushForward / PopForward
        // -----------------------------------------------------------------

        [Fact]
        public void PushForward_EnablesCanGoForward()
        {
            var h = new NavigationHistory();

            h.PushForward(Entry("A"));

            Assert.True(h.CanGoForward);
            Assert.Single(h.HistoryForward);
        }

        [Fact]
        public void PushForward_Null_IsIgnored()
        {
            var h = new NavigationHistory();

            h.PushForward(null);

            Assert.False(h.CanGoForward);
            Assert.Empty(h.HistoryForward);
        }

        [Fact]
        public void PopForward_ReturnsMostRecent_LIFO()
        {
            var h = new NavigationHistory();
            h.PushForward(Entry("A"));
            h.PushForward(Entry("B"));

            Assert.Equal("B", h.PopForward().PageName);
            Assert.Equal("A", h.PopForward().PageName);
            Assert.False(h.CanGoForward);
        }

        // -----------------------------------------------------------------
        // Clear
        // -----------------------------------------------------------------

        [Fact]
        public void Clear_EmptiesBothStacks()
        {
            var h = new NavigationHistory();
            h.Record(Entry("A"));
            h.Record(Entry("B"));
            h.PushForward(Entry("F1"));
            h.PushForward(Entry("F2"));

            h.Clear();

            Assert.False(h.CanGoBack);
            Assert.False(h.CanGoForward);
            Assert.Empty(h.HistoryBack);
            Assert.Empty(h.HistoryForward);
        }

        // -----------------------------------------------------------------
        // Composite: simulated browser back/forward dance
        // -----------------------------------------------------------------

        /// <summary>
        /// Walks the cycle a runtime should perform around the storage. The runtime
        /// records the page being LEFT — so after navigating HOME → A → B → C,
        /// the back stack holds [HOME, A, B] (the top is "B" because B is what we
        /// left to reach C). Going back pops B (most-recent entry = pop target) and
        /// the runtime pushes the current page (C) onto the forward stack. Then a
        /// fresh forward navigation via Record(...) must wipe the forward stack —
        /// the user took a "different branch", there is nothing to redo.
        /// This shape mirrors NEW-7's territory; pinning the storage half here
        /// lets us reason about <c>NavigationRuntime</c>'s back path independently.
        /// </summary>
        [Fact]
        public void BrowserDance_RecordAfterBack_WipesForwardStack()
        {
            var h = new NavigationHistory();

            // Forward HOME → A → B → C: runtime records the page being LEFT each step.
            h.Record(Entry("HOME"));    // back = [HOME]               (current: A)
            h.Record(Entry("A"));       // back = [HOME, A]            (current: B)
            h.Record(Entry("B"));       // back = [HOME, A, B]         (current: C)

            // Back step 1: pop the top of the back stack (= "B", the page-to-restore).
            // Runtime then pushes the leaving page (C) onto the forward stack.
            Assert.True(h.TryPopBack(out var popped1));
            Assert.Equal("B", popped1.PageName);                    // back = [HOME, A]
            h.PushForward(Entry("C"));                              // forward = [C]

            // Back step 2: pop "A". Push leaving page (B) onto forward.
            Assert.True(h.TryPopBack(out var popped2));
            Assert.Equal("A", popped2.PageName);                    // back = [HOME]
            h.PushForward(Entry("B"));                              // forward = [C, B]

            Assert.Equal(2, h.HistoryForward.Count());

            // From A, the user takes a "different branch" — forward navigation to a
            // new page D via Record(...). The runtime records the page being left
            // (A); Record must also wipe the forward stack — nothing to redo.
            h.Record(Entry("A"));                                    // back = [HOME, A]

            Assert.False(h.CanGoForward);
            Assert.Empty(h.HistoryForward);
            Assert.Equal(2, h.HistoryBack.Count());
        }
    }
}
