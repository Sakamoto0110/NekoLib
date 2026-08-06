using NekoLib.Navigation.WinForms.Hosting;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.Model;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Overlays
{
    /// <summary>
    /// The second non-generic shim in this scenario, over
    /// <c>PromptViewBase&lt;NewAnimalRequest&gt;</c>.
    /// <para/>
    /// It exists for the same single reason as <see cref="ReasonPromptBase"/>: the
    /// WinForms designer refuses a generic base class, so a prompt cannot be laid out
    /// visually until the type argument is closed. One empty class per result type.
    /// <para/>
    /// Two result types, two shims — which is the point of writing this one down.
    /// The cost of keeping <c>TResult</c> on the view scales with the number of
    /// distinct prompt results an application has, and this is the file that makes
    /// that concrete instead of theoretical.
    /// </summary>
    public class NewAnimalPromptBase : PromptViewBase<NewAnimalRequest>
    {
    }
}
