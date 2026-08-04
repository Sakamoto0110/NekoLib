using NekoLib.Navigation.Bootstrap;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Tests.Unit.Fakes;
using NekoLib.Navigation.WinForms.Adapters;
using NekoLib.Navigation.Wpf.Adapters;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    /// <summary>
    /// NAV-008. Coverage the item asks for explicitly: the timer interval (a) and
    /// the tolerant assembly scan (b).
    /// </summary>
    public class BootstrapSmallDefectTests
    {
        // NAV-008(a): the constructor parameter was never assigned, so a directly
        // constructed WinForms timer kept the WinForms default of 100 ms while the
        // WPF one honoured the argument.
        [Fact]
        public void WinFormsTimerAdapter_ExplicitInterval_IsHonoured()
        {
            RunSta(() =>
            {
                using (var timer = new WinFormsTimerAdapter(4200))
                    Assert.Equal(4200, timer.IntervalMilliseconds);
            });
        }

        [Fact]
        public void WinFormsTimerAdapter_DefaultInterval_MatchesTheDeclaredDefault()
        {
            RunSta(() =>
            {
                using (var timer = new WinFormsTimerAdapter())
                    Assert.Equal(15000, timer.IntervalMilliseconds);
            });
        }

        [Fact]
        public void TimerAdapters_ExplicitInterval_AgreeAcrossPlatforms()
        {
            RunSta(() =>
            {
                using (var winForms = new WinFormsTimerAdapter(2500))
                using (var wpf = new WpfTimerAdapter(2500))
                    Assert.Equal(wpf.IntervalMilliseconds, winForms.IntervalMilliseconds);
            });
        }

        // NAV-008(b): the custom-loading-mask probe ran raw Assembly.GetTypes() on
        // the first line of Start(), so one unloadable type aborted bootstrap before
        // any of the tolerance the rest of it advertises could apply.
        [Fact]
        public void Start_AssemblyWithUnloadableTypes_DoesNotAbortOnTheTypeLoadFailure()
        {
            var assembly = new PartiallyLoadableAssembly();

            var failure = Record.Exception(() => PageNavBootstrap
                .Use<FakePlatformAdapter>(new object())
                .RegisterPagesFromAssembly(assembly)
                .Start());

            Assert.NotNull(failure);
            Assert.IsNotType<ReflectionTypeLoadException>(failure);
        }

        [Fact]
        public void GetLoadableTypes_PartiallyLoadableAssembly_ReturnsTheTypesThatLoaded()
        {
            var loaded = new List<Type>(
                AssemblyTypeScanner.GetLoadableTypes(new PartiallyLoadableAssembly()));

            Assert.Equal(new[] { typeof(UnloadableProbePage) }, loaded);
        }

        /// <summary>
        /// Stands in for an assembly holding a type whose dependency is missing:
        /// <c>GetTypes()</c> throws, but the exception still carries the types that
        /// did load, interleaved with nulls for the ones that did not.
        /// </summary>
        private sealed class PartiallyLoadableAssembly : Assembly
        {
            public override Type[] GetTypes()
                => throw new ReflectionTypeLoadException(
                    new[] { typeof(UnloadableProbePage), null },
                    new Exception[] { new TypeLoadException("missing dependency") });

            public override string FullName => "PartiallyLoadable, Version=0.0.0.0";
        }

        private sealed class UnloadableProbePage : IPageView
        {
            public string Name => nameof(UnloadableProbePage);
            public object NativeView => this;
            public bool IsDisposed => false;
            public void Dispose() { }
        }

        private static void RunSta(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { failure = ex; }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
                ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
