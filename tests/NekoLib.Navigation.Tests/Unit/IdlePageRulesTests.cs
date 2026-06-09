using System;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Runtime.Registry;
using NekoLib.Navigation.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    /// <summary>
    /// Pins the single idle-page rule set shared by the runtime (idle/timeout
    /// navigation) and the bootstrap ([PageTimeout] placement + idle-timer wiring).
    /// Any one of role / "idle" tag / name-contains-"idle" qualifies a page as idle;
    /// resolution priority is role, then tag, then name.
    /// </summary>
    public class IdlePageRulesTests
    {
        [Fact]
        public void IsIdleCandidate_true_for_PageRole_Idle()
            => Assert.True(IdlePageRules.IsIdleCandidate(Describe<StubA>(d => d.Role = PageRole.Idle)));

        [Fact]
        public void IsIdleCandidate_true_for_idle_tag_case_insensitive()
            => Assert.True(IdlePageRules.IsIdleCandidate(Describe<StubA>(d => d.AddTag("IDLE"))));

        [Fact]
        public void IsIdleCandidate_true_when_name_contains_idle()
            // StubIdle has no role/tag; only its name ("StubIdle") carries "idle".
            => Assert.True(IdlePageRules.IsIdleCandidate(Describe<StubIdle>()));

        [Fact]
        public void IsIdleCandidate_false_for_plain_non_idle_page()
            => Assert.False(IdlePageRules.IsIdleCandidate(Describe<StubA>()));

        [Fact]
        public void Resolve_prefers_role_over_name_convention()
        {
            var registry = PageRegistry.Create(b =>
            {
                b.RegisterType(typeof(StubA), d => d.Role = PageRole.Idle);
                b.RegisterType(typeof(StubIdle)); // name-only candidate
            });

            var idle = IdlePageRules.Resolve(registry.AllDescriptors());

            Assert.Equal(typeof(StubA), idle.PageType);
        }

        [Fact]
        public void Resolve_prefers_tag_over_name_convention()
        {
            var registry = PageRegistry.Create(b =>
            {
                b.RegisterType(typeof(StubA), d => d.AddTag("idle"));
                b.RegisterType(typeof(StubIdle)); // name-only candidate
            });

            var idle = IdlePageRules.Resolve(registry.AllDescriptors());

            Assert.Equal(typeof(StubA), idle.PageType);
        }

        [Fact]
        public void Resolve_falls_back_to_name_convention()
        {
            var registry = PageRegistry.Create(b => b.RegisterType(typeof(StubIdle)));

            var idle = IdlePageRules.Resolve(registry.AllDescriptors());

            Assert.Equal(typeof(StubIdle), idle.PageType);
        }

        [Fact]
        public void Resolve_returns_null_when_no_page_qualifies()
        {
            var registry = PageRegistry.Create(b => b.RegisterType(typeof(StubA)));

            Assert.Null(IdlePageRules.Resolve(registry.AllDescriptors()));
        }

        private static PageDescriptor Describe<T>(Action<PageDescriptorBuilder> cfg = null)
            where T : IPageView
        {
            var registry = PageRegistry.Create(b => b.RegisterType(typeof(T), cfg));
            return registry.GetDescriptor(typeof(T));
        }
    }
}
