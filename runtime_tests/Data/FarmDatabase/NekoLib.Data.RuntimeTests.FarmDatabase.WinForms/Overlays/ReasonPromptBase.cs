using System.ComponentModel;
using NekoLib.Navigation.WinForms.Hosting;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Overlays
{
    /// <summary>
    /// Non-generic, concrete shim over <see cref="PromptViewBase{TResult}"/>.
    /// <para/>
    /// This class exists purely so the WinForms designer can open
    /// <see cref="ReasonPrompt"/>. The designer instantiates the base class of
    /// whatever it loads, and it refuses two shapes: an <c>abstract</c> base and a
    /// <em>generic</em> base. <c>PromptViewBase&lt;TResult&gt;</c> is both, so a
    /// prompt deriving from it directly cannot be laid out visually - the designer
    /// reports that it cannot load the type and falls back to the code view.
    /// <para/>
    /// Closing <c>TResult</c> to <see cref="string"/> and dropping <c>abstract</c>
    /// fixes it, and costs one empty class per prompt result type. The same applies
    /// to <c>DialogViewBase</c>, <c>ToastViewBase</c> and <c>PopoverViewBase</c>,
    /// which are abstract but not generic - those only need the abstract removed by
    /// a shim, not the type argument.
    /// </summary>
    public class ReasonPromptBase : PromptViewBase<string>
    {
    }
}
