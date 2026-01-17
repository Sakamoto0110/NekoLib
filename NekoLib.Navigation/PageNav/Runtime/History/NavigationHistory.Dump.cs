// FILE: PageNav.Core/Services/NavigationHistory.Dump.cs
using System.Text;

namespace NekoLib.Navigation.Runtime.History
{
    public sealed partial class NavigationHistory
    {
        public string Dump()
        {
            var sb = new StringBuilder();

            sb.AppendLine("=== Navigation History ===");

            sb.AppendLine("Back stack:");
            if(_back.Count == 0)
                sb.AppendLine("  (empty)");
            else
            {
                foreach(var e in _back)
                    sb.AppendLine($"  <- {e}");
            }

            sb.AppendLine("Forward stack:");
            if(_forward.Count == 0)
                sb.AppendLine("  (empty)");
            else
            {
                foreach(var e in _forward)
                    sb.AppendLine($"  -> {e}");
            }

            return sb.ToString();
        }
    }
}
