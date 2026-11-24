using System.Collections.Generic;
using Xunit;

namespace NekoLib.Watchdog.Tests.Unit
{
    public sealed class AttachedProcessTerminationTests
    {
        [Fact]
        public void KillOrder_ExcludesSupervisorBranchAndUnrelatedProcesses_AndKillsChildrenFirst()
        {
            var parents = new Dictionary<int, int>
            {
                { 10, 1 }, { 11, 10 }, { 12, 11 }, // target application and its children
                { 20, 10 }, { 21, 20 }, { 22, 21 }, // Host and Host-owned children
                { 30, 1 }, { 31, 30 }
            };
            Assert.Equal(new[] { 12, 11, 10 }, AttachedProcessTermination.SelectKillOrder(10, 20, parents));
        }

        [Fact]
        public void KillOrder_BoundsMalformedCycles_AndNeverSelectsSupervisorAsTarget()
        {
            var parents = new Dictionary<int, int> { { 1, 2 }, { 2, 1 }, { 3, 3 }, { 4, 1 } };
            Assert.Empty(AttachedProcessTermination.SelectKillOrder(1, 1, parents));
            Assert.Equal(new[] { 99 }, AttachedProcessTermination.SelectKillOrder(99, 1, parents));
        }
    }
}
