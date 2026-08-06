using System.ComponentModel;
using NekoLib.Navigation.WinForms.Hosting;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Overlays
{
    /// <summary>
    /// Non-generic, concrete shim over <see cref="PromptViewBase{TResult}"/>.
    /// <para/>
    /// This class exists purely so the WinForms designer can open
    /// <see cref="ReasonPrompt"/>. The designer instantiates the base class of the
    /// type it is loading, and it refuses a <em>generic</em> base outright.
    /// <para/>
    /// Closing <c>TResult</c> to <see cref="string"/> is what fixes that, at the
    /// cost of one empty class per prompt result type. The other two blockers this
    /// shim used to work around are gone: the surface bases are no longer
    /// <c>abstract</c>, and they no longer schedule work on a handle that does not
    /// exist yet. Only the type argument is left, so the dialog, toast and popover
    /// bases need no shim at all - this one stays until <c>IPromptView</c> stops
    /// carrying its result type on the view.
    /// </summary>
    public class ReasonPromptBase : PromptViewBase<string>
    {
    }
}
