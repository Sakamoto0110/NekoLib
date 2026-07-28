using System;
using System.Linq;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Runtime.Services;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    public sealed class ReferenceCountedInteractionBlockerTests
    {
        [Fact]
        public void Block_OverlappingOwners_TransitionsPlatformOnlyAtOuterEdges()
        {
            var inner = new CountingBlocker();
            var blocker = new ReferenceCountedInteractionBlocker(inner);

            blocker.Block();
            blocker.Block();
            blocker.Unblock();

            Assert.Equal(1, inner.BlockCalls);
            Assert.Equal(0, inner.UnblockCalls);
            Assert.Equal(1, blocker.Depth);

            blocker.Unblock();

            Assert.Equal(1, inner.UnblockCalls);
            Assert.Equal(0, blocker.Depth);
        }

        [Fact]
        public void Block_PlatformThrows_DoesNotRetainPhantomOwner()
        {
            var inner = new CountingBlocker { ThrowOnBlock = true };
            var blocker = new ReferenceCountedInteractionBlocker(inner);

            Assert.Throws<InvalidOperationException>(() => blocker.Block());
            Assert.Equal(0, blocker.Depth);

            inner.ThrowOnBlock = false;
            blocker.Block();

            Assert.Equal(1, blocker.Depth);
            Assert.Equal(2, inner.BlockCalls);
        }

        [Fact]
        public void Block_PlatformReentersBlock_PublishesDepthBeforeCallback()
        {
            var inner = new CountingBlocker();
            ReferenceCountedInteractionBlocker blocker = null;
            inner.OnBlock = () => blocker.Block();
            blocker = new ReferenceCountedInteractionBlocker(inner);

            blocker.Block();

            Assert.Equal(1, inner.BlockCalls);
            Assert.Equal(2, blocker.Depth);

            blocker.Unblock();
            blocker.Unblock();

            Assert.Equal(1, inner.UnblockCalls);
            Assert.Equal(0, blocker.Depth);
        }

        [Fact]
        public void Block_PlatformThrowsAfterReentrantOwner_DoesNotClobberDepth()
        {
            var inner = new CountingBlocker { ThrowOnBlock = true };
            ReferenceCountedInteractionBlocker blocker = null;
            inner.OnBlock = () => blocker.Block();
            blocker = new ReferenceCountedInteractionBlocker(inner);

            Assert.Throws<InvalidOperationException>(() => blocker.Block());

            Assert.Equal(1, inner.BlockCalls);
            Assert.Equal(2, blocker.Depth);
        }

        [Fact]
        public void Unblock_PlatformReentersBlock_PreservesNewOwner()
        {
            var inner = new CountingBlocker();
            var blocker = new ReferenceCountedInteractionBlocker(inner);
            blocker.Block();
            inner.OnUnblock = () => blocker.Block();

            blocker.Unblock();

            Assert.Equal(2, inner.BlockCalls);
            Assert.Equal(1, inner.UnblockCalls);
            Assert.Equal(1, blocker.Depth);

            blocker.Unblock();

            Assert.Equal(2, inner.UnblockCalls);
            Assert.Equal(0, blocker.Depth);
        }

        [Fact]
        public void Unblock_PlatformThrowsWithoutReentrancy_RestoresOwner()
        {
            var inner = new CountingBlocker { ThrowOnUnblock = true };
            var blocker = new ReferenceCountedInteractionBlocker(inner);
            blocker.Block();

            Assert.Throws<InvalidOperationException>(() => blocker.Unblock());
            Assert.Equal(1, blocker.Depth);

            inner.ThrowOnUnblock = false;
            blocker.Unblock();

            Assert.Equal(0, blocker.Depth);
            Assert.Equal(2, inner.UnblockCalls);
        }

        [Fact]
        public void PageAwareNotifications_ForwardThroughReferenceCounting()
        {
            var inner = new CountingPageAwareBlocker();
            var blocker = new ReferenceCountedInteractionBlocker(inner);
            var background = new object();
            var modal = new object();

            blocker.Block();
            blocker.Block();
            blocker.OnViewAdded(background, isModalSurface: false);
            blocker.OnViewAdded(modal, isModalSurface: true);
            blocker.OnViewRemoved(modal);
            blocker.Unblock();

            Assert.Equal(1, inner.BlockCalls);
            Assert.Equal(0, inner.UnblockCalls);
            Assert.Equal(
                new[]
                {
                    (background, false),
                    (modal, true)
                },
                inner.Added.ToArray());
            Assert.Same(modal, Assert.Single(inner.Removed));

            blocker.Unblock();
            Assert.Equal(1, inner.UnblockCalls);
        }

        private sealed class CountingBlocker : IInteractionBlocker
        {
            public int BlockCalls { get; private set; }
            public int UnblockCalls { get; private set; }
            public bool ThrowOnBlock { get; set; }
            public bool ThrowOnUnblock { get; set; }
            public System.Action OnBlock { get; set; }
            public System.Action OnUnblock { get; set; }

            public void Block()
            {
                BlockCalls++;
                var callback = OnBlock;
                OnBlock = null;
                callback?.Invoke();
                if (ThrowOnBlock)
                    throw new InvalidOperationException("block failed");
            }

            public void Unblock()
            {
                UnblockCalls++;
                var callback = OnUnblock;
                OnUnblock = null;
                callback?.Invoke();
                if (ThrowOnUnblock)
                    throw new InvalidOperationException("unblock failed");
            }
        }

        private sealed class CountingPageAwareBlocker :
            IPageAwareInteractionBlocker
        {
            public int BlockCalls { get; private set; }
            public int UnblockCalls { get; private set; }
            public System.Collections.Generic.List<(object, bool)> Added
                { get; } =
                    new System.Collections.Generic.List<(object, bool)>();
            public System.Collections.Generic.List<object> Removed
                { get; } =
                    new System.Collections.Generic.List<object>();

            public void Block() => BlockCalls++;
            public void Unblock() => UnblockCalls++;

            public void OnViewAdded(
                object view,
                bool isModalSurface)
                => Added.Add((view, isModalSurface));

            public void OnViewRemoved(object view)
                => Removed.Add(view);
        }
    }
}
