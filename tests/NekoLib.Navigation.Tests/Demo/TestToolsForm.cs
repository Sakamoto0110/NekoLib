using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using NekoLib.Navigation;
using BottomLeftToastView = NavigationDemo.Pages.BottomLeftToast.BottomLeftToastView;
using ConfirmDialogView = NavigationDemo.Pages.ConfirmDialog.ConfirmDialogView;
using HeavyPageView = NavigationDemo.Pages.HeavyPage.HeavyPage;
using HeavyPageBackgroundView = NavigationDemo.Pages.HeavyPageBackground.HeavyPageBackground;
using PageAView = NavigationDemo.Pages.PageA.PageA;
using PageBView = NavigationDemo.Pages.PageB.PageB;
using PageCView = NavigationDemo.Pages.PageC.PageC;
using PageDView = NavigationDemo.Pages.PageD.PageD;
using PageEView = NavigationDemo.Pages.PageE.PageE;
using SimpleToastView = NavigationDemo.Pages.SimpleToast.SimpleToastView;
using TextInputPromptView = NavigationDemo.Pages.TextInputPrompt.TextInputPromptView;

namespace NavigationDemo
{
    /// <summary>
    /// Secondary tools window. Each section is a one-click reproduction of a
    /// §2.8 runtime-repro scenario from the audit. The designer owns the empty
    /// layout containers (table, scroll panel, flow panel, log); this file
    /// populates the flow panel with the scenario buttons at runtime — each
    /// button is dynamically tagged with the finding ID it validates, which the
    /// WinForms designer can't represent naturally.
    /// </summary>
    public partial class TestToolsForm : Form
    {
        private static readonly Type[] _stressPages =
        {
            typeof(PageAView), typeof(PageBView), typeof(PageCView),
            typeof(PageDView), typeof(PageEView),
        };

        public TestToolsForm()
        {
            // Subscribe to the A-5 probe so an unexpected stale-apply lands in
            // the demo's own log (silent = guard held; line printed = regression).
            HeavyPageBackgroundView.ApplyFired += OnApplyFired;

            InitializeComponent();
            PopulateButtons();
        }

        private void OnApplyFired(string msg) => Log("[A-5] " + msg);

        private void PopulateButtons()
        {
            // ---- TOASTS ---------------------------------------------------
            buttonPanel.Controls.Add(MakeHeader("Toasts"));
            buttonPanel.Controls.Add(MakeButton("Spawn bottom-left toast",
                () => SpawnBottomLeftToast("Hello from the tools window")));
            buttonPanel.Controls.Add(MakeButton("Spawn bottom-left toast (long)",
                () => SpawnBottomLeftToast("This is a longer toast message used to verify text fit inside the bottom-left rectangle.")));
            buttonPanel.Controls.Add(MakeButton("Rapid-fire (5 toasts)",
                SpawnRapidFireBottomLeft));
            buttonPanel.Controls.Add(MakeButton("Dismiss current toast",
                () => { NavigationService.DismissCurrentToast(); Log("Dismiss requested."); }));
            buttonPanel.Controls.Add(MakeButton("Default toast (dock-fill SimpleToast)",
                () => { NavigationService.ShowToast<SimpleToastView>("Saved successfully!"); Log("SimpleToast shown."); }));

            // ---- MODALS ---------------------------------------------------
            buttonPanel.Controls.Add(MakeHeader("Modal stress (A-1 / N-3 / N-6)"));
            buttonPanel.Controls.Add(MakeButton("Show confirm dialog",
                ShowConfirmDialog));
            buttonPanel.Controls.Add(MakeButton("Show text-input prompt",
                ShowTextPrompt));
            buttonPanel.Controls.Add(MakeButton("ShowDialog from Task.Run (A-1)",
                ShowDialogFromBackgroundThread));
            buttonPanel.Controls.Add(MakeButton("Two dialogs, close 2nd first (N-3)",
                ShowTwoStackedDialogs));
            buttonPanel.Controls.Add(MakeButton("Shutdown WHILE dialog open (N-3/N-6)",
                ShutdownWhileDialogOpen));

            // ---- NAV STRESS ----------------------------------------------
            buttonPanel.Controls.Add(MakeHeader("Navigation stress (A-6 / N-1)"));
            buttonPanel.Controls.Add(MakeButton("Go Home",
                async () => { await NavigationService.GoHomeAsync(); Log("Navigated home."); }));
            buttonPanel.Controls.Add(MakeButton("Go Back",
                async () => { bool ok = await NavigationService.GoBackAsync(); Log("GoBack -> " + ok); }));
            buttonPanel.Controls.Add(MakeButton("Rapid 20 forward navs (random)",
                RapidForwardStress));
            buttonPanel.Controls.Add(MakeButton("Rapid alternating fwd/back (20)",
                RapidAlternatingStress));
            buttonPanel.Controls.Add(MakeButton("Concurrent SwitchPage from Task.Run",
                ConcurrentFromTaskRun));

            // ---- LOAD MODES ----------------------------------------------
            buttonPanel.Controls.Add(MakeHeader("Load modes (A-5)"));
            buttonPanel.Controls.Add(MakeButton("Show HeavyPage (LoadBeforeShow 2 s)",
                async () => { await NavigationService.SwitchPage<HeavyPageView>(); Log("HeavyPage requested."); }));
            buttonPanel.Controls.Add(MakeButton("Show HeavyPageBackground (LoadInBackground 2 s)",
                async () => { await NavigationService.SwitchPage<HeavyPageBackgroundView>(); Log("HeavyPageBackground requested (background load)."); }));
            buttonPanel.Controls.Add(MakeButton("Navigate-away-mid-load (A-5)",
                NavigateAwayMidLoad));
        }

        // -----------------------------------------------------------------
        // FACTORIES
        // -----------------------------------------------------------------

        private static Label MakeHeader(string text) => new Label
        {
            Text = text,
            Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold),
            ForeColor = Color.DimGray,
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 4)
        };

        private static Button MakeButton(string text, Action onClick)
        {
            var b = new Button
            {
                Text = text,
                Width = 310,
                Height = 28,
                Margin = new Padding(0, 0, 0, 3),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
            };
            b.Click += (s, e) => onClick();
            return b;
        }

        // -----------------------------------------------------------------
        // TOAST SCENARIOS
        // -----------------------------------------------------------------

        private void SpawnBottomLeftToast(string message)
        {
            NavigationService.ShowToast<BottomLeftToastView>(message);
            Log("Bottom-left toast shown: \"" + Truncate(message, 40) + "\"");
        }

        private void SpawnRapidFireBottomLeft()
        {
            for (int i = 1; i <= 5; i++)
            {
                int n = i;
                NavigationService.ShowToast<BottomLeftToastView>("Rapid #" + n);
            }
            Log("Rapid-fire 5 toasts requested.");
        }

        // -----------------------------------------------------------------
        // MODAL SCENARIOS
        // -----------------------------------------------------------------

        private async void ShowConfirmDialog()
        {
            try
            {
                bool result = await NavigationService.ShowDialogAsync<ConfirmDialogView>();
                Log("Confirm dialog -> " + result);
            }
            catch (Exception ex) { Log("Dialog error: " + ex.Message); Debug.WriteLine(ex); }
        }

        private async void ShowTextPrompt()
        {
            try
            {
                string result = await NavigationService.ShowPromptAsync<TextInputPromptView, string>();
                Log("Prompt -> \"" + (result ?? "<null>") + "\"");
            }
            catch (Exception ex) { Log("Prompt error: " + ex.Message); Debug.WriteLine(ex); }
        }

        /// <summary>A-1: ShowDialog called from a thread-pool thread must still reach UI.</summary>
        private void ShowDialogFromBackgroundThread()
        {
            Log("[A-1] Spawning Task.Run that calls ShowDialogAsync...");
            _ = Task.Run(async () =>
            {
                try
                {
                    bool result = await NavigationService.ShowDialogAsync<ConfirmDialogView>();
                    Log("[A-1] Dialog from Task.Run -> " + result);
                }
                catch (Exception ex) { Log("[A-1] Error: " + ex.Message); Debug.WriteLine(ex); }
            });
        }

        /// <summary>N-3: Open two dialogs back-to-back; verify both complete cleanly.</summary>
        private void ShowTwoStackedDialogs()
        {
            Log("[N-3] Launching two dialogs back-to-back...");
            _ = Task.Run(async () =>
            {
                var t1 = NavigationService.ShowDialogAsync<ConfirmDialogView>();
                await Task.Delay(150);
                var t2 = NavigationService.ShowDialogAsync<ConfirmDialogView>();
                try
                {
                    bool r2 = await t2;
                    Log("[N-3] 2nd dialog -> " + r2);
                    bool r1 = await t1;
                    Log("[N-3] 1st dialog -> " + r1);
                }
                catch (Exception ex) { Log("[N-3] Error: " + ex.Message); Debug.WriteLine(ex); }
            });
        }

        /// <summary>N-3/N-6: Shutdown while a dialog is open — pending TCS should not hang.</summary>
        private async void ShutdownWhileDialogOpen()
        {
            Log("[N-3/N-6] Open dialog, then trigger Shutdown after 800 ms...");
            var dialogTask = NavigationService.ShowDialogAsync<ConfirmDialogView>();
            await Task.Delay(800);
            try
            {
                await NavigationService.Shutdown();
                Log("[N-3/N-6] Shutdown completed.");
            }
            catch (Exception ex) { Log("[N-3/N-6] Shutdown error: " + ex.Message); Debug.WriteLine(ex); }

            try
            {
                bool result = await dialogTask;
                Log("[N-3/N-6] Dialog task resolved -> " + result);
            }
            catch (Exception ex) { Log("[N-3/N-6] Dialog task threw -> " + ex.GetType().Name + ": " + ex.Message); }
        }

        // -----------------------------------------------------------------
        // NAV STRESS SCENARIOS
        // -----------------------------------------------------------------

        private async void RapidForwardStress()
        {
            Log("[Stress] 20 rapid forward navs across random pages...");
            var rand = new Random(0xC0FFEE);
            int ok = 0, errors = 0;
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    Type page = _stressPages[rand.Next(_stressPages.Length)];
                    await NavigationService.SwitchPage(page);
                    ok++;
                }
                catch (Exception ex) { errors++; Debug.WriteLine(ex); }
            }
            Log("[Stress] forward done: ok=" + ok + " errors=" + errors);
        }

        private async void RapidAlternatingStress()
        {
            Log("[Stress] 20 alternating forward/back navs...");
            int ok = 0, errors = 0;
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    Type page = _stressPages[i % _stressPages.Length];
                    await NavigationService.SwitchPage(page);
                    ok++;
                    await NavigationService.GoBackAsync();
                    ok++;
                }
                catch (Exception ex) { errors++; Debug.WriteLine(ex); }
            }
            Log("[Stress] alternating done: ok=" + ok + " errors=" + errors);
        }

        /// <summary>
        /// A-6 / N-1: fire 5 SwitchPage calls from thread-pool threads without awaiting
        /// each. The runtime's _navGate semaphore must serialize them on the UI thread
        /// without deadlocking.
        /// </summary>
        private void ConcurrentFromTaskRun()
        {
            Log("[A-6/N-1] Launching 5 concurrent SwitchPage calls from Task.Run...");
            var tasks = new Task[5];
            for (int i = 0; i < 5; i++)
            {
                Type page = _stressPages[i % _stressPages.Length];
                tasks[i] = Task.Run(() => NavigationService.SwitchPage(page));
            }
            _ = Task.WhenAll(tasks).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Log("[A-6/N-1] FAULTED: " + t.Exception?.GetBaseException().Message);
                else
                    Log("[A-6/N-1] All 5 concurrent navs completed cleanly.");
            });
        }

        // -----------------------------------------------------------------
        // LOAD-MODE SCENARIOS
        // -----------------------------------------------------------------

        /// <summary>
        /// A-5: kick off HeavyPageBackground (LoadInBackground, 2 s load) and
        /// immediately switch elsewhere. The page is shown before its load runs,
        /// so the apply phase fires asynchronously — the runtime's stale-apply
        /// guard must skip <c>ApplyBackgroundResultAsync</c> because the page is
        /// no longer current. Watch the log: if
        /// "[A-5] [HeavyPageBackground] ApplyBackgroundResultAsync fired."
        /// prints after the jump, A-5 regressed; if it stays silent, the guard held.
        /// </summary>
        private async void NavigateAwayMidLoad()
        {
            Log("[A-5] Switching to HeavyPageBackground then jumping to PageA mid-load...");
            try
            {
                _ = NavigationService.SwitchPage<HeavyPageBackgroundView>();
                await Task.Delay(200);
                await NavigationService.SwitchPage<PageAView>();
                Log("[A-5] Jumped to PageA. Watch log for ~2s — apply must NOT fire.");
            }
            catch (Exception ex) { Log("[A-5] Error: " + ex.Message); Debug.WriteLine(ex); }
        }

        // -----------------------------------------------------------------
        // LOG
        // -----------------------------------------------------------------

        private void Log(string message)
        {
            if (IsDisposed || logBox == null) return;
            if (InvokeRequired)
            {
                try { BeginInvoke((Action<string>)Log, message); } catch { }
                return;
            }
            logBox.AppendText(DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message + Environment.NewLine);
            logBox.SelectionStart = logBox.TextLength;
            logBox.ScrollToCaret();
        }

        private static string Truncate(string s, int max)
            => s == null || s.Length <= max ? s : s.Substring(0, max) + "…";
    }
}
