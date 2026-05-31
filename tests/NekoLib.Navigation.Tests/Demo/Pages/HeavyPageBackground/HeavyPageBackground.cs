using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata.Attributes;
using NekoLib.Navigation.WinForms.Hosting;

namespace NavigationDemo.Pages.HeavyPageBackground
{
    /// <summary>
    /// LoadInBackground variant of HeavyPage. Used specifically by the [A-5]
    /// runtime-repro scenario: the page is shown immediately, its 2 s load runs
    /// after, and if the user navigates away during the load the runtime's
    /// stale-apply guard must prevent <see cref="ApplyBackgroundResultAsync"/>
    /// from touching the disposed/abandoned page.
    ///
    /// The Debug.WriteLine in ApplyBackgroundResultAsync makes the guard's effect
    /// observable: if you see the line ~2 s after navigating away, A-5 has
    /// regressed; if you don't see it, the guard held.
    /// </summary>
    [PageLoad(NekoLib.Navigation.Metadata.NavigationLoadMode.LoadInBackground)]
    public sealed class HeavyPageBackground : PageView, IBackgroundLoadable
    {
        /// <summary>
        /// Static probe hook. <see cref="TestToolsForm"/> subscribes so the A-5 guard's
        /// effect is visible inside the demo's own log without needing DebugView.
        /// Fires only when <see cref="ApplyBackgroundResultAsync"/> actually runs — i.e.
        /// the framework's stale-apply guard let it through. Silent = guard held.
        /// </summary>
        public static event Action<string> ApplyFired;

        private readonly Label _label;
        private string _statusText = "Loading in background…";

        public HeavyPageBackground()
        {
            _label = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft Sans Serif", 14F, FontStyle.Bold),
                ForeColor = Color.White,
                Text = _statusText,
            };

            BackColor = Color.SaddleBrown;
            Name = nameof(HeavyPageBackground);
            Controls.Add(_label);
        }

        public async Task LoadInBackgroundAsync(object args)
        {
            await Task.Delay(2000);
        }

        public Task ApplyBackgroundResultAsync()
        {
            // If the user navigated away during the 2 s delay, the framework's
            // A-5 guard skips this call. If it still fires after a mid-load
            // navigate-away, the guard regressed.
            const string msg = "[HeavyPageBackground] ApplyBackgroundResultAsync fired.";
            Debug.WriteLine(msg);
            try { ApplyFired?.Invoke(msg); } catch { /* never let a probe break the runtime */ }

            _statusText = "Heavy background page loaded ✓";
            if (!IsDisposed && _label != null)
                _label.Text = _statusText;

            return Task.CompletedTask;
        }
    }
}
