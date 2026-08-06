using NekoLib.Navigation.Contracts.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    /// <summary>
    /// Guards the two properties that decide whether an application can lay a surface
    /// out in a visual designer at all.
    /// <para>
    /// Both were found by building a real consumer against the module rather than by
    /// reading it: the WinForms designer refuses to open a view whose base class is
    /// abstract, and it refuses again — with
    /// <c>"Invoke or BeginInvoke cannot be called on a control until the window handle
    /// has been created"</c> — when the base reacts to being parented by scheduling
    /// work on a handle that does not exist yet. The second one is not a design-time
    /// quirk: <c>BeginInvoke</c> throws on any handle-less control, so the same call
    /// was a latent runtime fault.
    /// </para>
    /// </summary>
    public class SurfaceBaseDesignTimeTests
    {
        /// <summary>
        /// Every public base type that implements a surface contract, on both
        /// platforms. These are the types a designer has to instantiate in order to
        /// render a subclass.
        /// </summary>
        public static IEnumerable<object[]> SurfaceBases()
        {
            Type[] contracts =
            {
                typeof(IDialogView),
                typeof(IPopoverView),
                typeof(IToastView),
                typeof(IPromptView)
            };

            Assembly[] platforms =
            {
                typeof(WinForms.Hosting.DialogViewBase).Assembly,
                typeof(Wpf.Hosting.DialogViewBase).Assembly
            };

            foreach (Assembly platform in platforms)
                foreach (Type type in platform.GetTypes())
                {
                    if (!type.IsPublic || !type.IsClass) continue;

                    // A generic type's reflected name carries an arity suffix
                    // ("PromptViewBase`1"), so match on the part before the backtick.
                    // Comparing the raw name silently skipped both prompt bases - the
                    // very types this test exists for.
                    string bareName = type.Name.Split('`')[0];
                    if (!bareName.EndsWith("Base", StringComparison.Ordinal)) continue;

                    if (contracts.Any(c => c.IsAssignableFrom(type) ||
                                           type.GetInterfaces().Any(i => i.Name == c.Name)))
                    {
                        yield return new object[] { type };
                    }
                }
        }

        [Theory]
        [MemberData(nameof(SurfaceBases))]
        public void SurfaceBase_IsNotAbstract_SoDesignersCanInstantiateIt(Type surfaceBase)
        {
            Assert.False(
                surfaceBase.IsAbstract,
                surfaceBase.FullName + " is abstract. A visual designer instantiates the " +
                "base class of the type it is opening, so an abstract base makes every " +
                "subclass undesignable. None of these bases declares an abstract member, " +
                "so the modifier buys nothing.");
        }

        // The host parents a surface before it is displayed, so the control can still
        // be handle-less when the parenting notification arrives. Deferring through
        // BeginInvoke at that moment throws instead of queueing.
        [Fact]
        public void WinFormsSurfaces_ParentedBeforeHandleExists_DoNotThrow()
        {
            RunSta(() =>
            {
                using (var host = new System.Windows.Forms.Panel { Width = 400, Height = 300 })
                {
                    Assert.False(host.IsHandleCreated,
                        "the host must stay handle-less for this test to mean anything");

                    // Derived rather than constructed directly: the bases keep
                    // protected constructors, which is what a consumer subclasses and
                    // what a designer reaches through reflection.
                    using (var dialog = new ProbeDialog())
                    using (var popover = new ProbePopover())
                    using (var prompt = new ProbePrompt())
                    {
                        host.Controls.Add(dialog);
                        host.Controls.Add(popover);
                        host.Controls.Add(prompt);
                    }
                }
            });
        }

        private sealed class ProbeDialog : WinForms.Hosting.DialogViewBase { }
        private sealed class ProbePopover : WinForms.Hosting.PopoverViewBase { }
        private sealed class ProbePrompt : WinForms.Hosting.PromptViewBase<string> { }

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
