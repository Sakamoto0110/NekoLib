using System;
using System.Collections.Generic; // Added for HashSet
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BundlerTool
{
    public partial class MainForm : Form
    {
        private string _defaultPath;
        private string DefaultPath
        {
            get => _defaultPath;
            set
            {
                _defaultPath = value;
                txtWorkingDir.Text = DefaultPath;

                bool isValidDir = !string.IsNullOrWhiteSpace(DefaultPath) && Directory.Exists(DefaultPath);
                btnCreateBundles.Enabled = isValidDir;
                btnCreateAstBundles.Enabled = isValidDir;

                if (isValidDir)
                {
                    RunPreScan(DefaultPath);
                }
                else
                {
                    lblStatus.Text = "Ready";
                    if (lblAstWarning != null) lblAstWarning.Visible = false;
                }
            }
        }

        public MainForm()
        {
            InitializeComponent();
            ApplyDarkTheme();
            DefaultPath = GetDefaultDirectory();
        }

        private void ApplyDarkTheme()
        {
            // Dark base with that minimal drift to purple 
            this.BackColor = Color.FromArgb(35, 31, 40);
            this.ForeColor = Color.White;

            // Set styles for controls dynamically
            foreach (Control control in this.Controls)
            {
                if (control is Button btn)
                {
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderSize = 0;
                    btn.BackColor = Color.FromArgb(50, 45, 60);
                    btn.ForeColor = Color.White;
                    btn.MouseEnter += delegate { btn.BackColor = Color.FromArgb(70, 60, 85); };
                    btn.MouseLeave += delegate { btn.BackColor = Color.FromArgb(50, 45, 60); };
                }
            }

            txtWorkingDir.BackColor = Color.FromArgb(50, 45, 60);
            txtWorkingDir.ForeColor = Color.White;
            txtWorkingDir.BorderStyle = BorderStyle.FixedSingle;

            statusStrip1.BackColor = Color.FromArgb(50, 45, 60);
            statusStrip1.ForeColor = Color.White;
        }

        private void RunPreScan(string directory)
        {
            // 1. Solution & Unloaded Project Scan
            var scanResult = BundleEngine.FastSolutionScan(directory);

            if (scanResult.SolutionCount > 0)
            {
                string msg = $"Found {scanResult.SolutionCount} .sln(s), {scanResult.TotalProjects} projects.";

                if (scanResult.UnloadedOrMissingProjects > 0)
                {
                    msg += $" ({scanResult.UnloadedOrMissingProjects} unavailable auto-ignored)";

                    string ignoreFilePath = Path.Combine(directory, ".bundleignore");
                    var existingIgnores = File.Exists(ignoreFilePath)
                        ? new HashSet<string>(File.ReadAllLines(ignoreFilePath), StringComparer.OrdinalIgnoreCase)
                        : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    bool addedNew = false;
                    foreach (var dir in scanResult.DirectoriesToIgnore)
                    {
                        if (existingIgnores.Add(dir))
                        {
                            addedNew = true;
                        }
                    }

                    if (addedNew)
                    {
                        File.WriteAllLines(ignoreFilePath, existingIgnores);
                    }
                }
                lblStatus.Text = msg;
            }
            else
            {
                lblStatus.Text = "Ready (No .sln files found)";
            }

            // 2. AST Language Heuristic Scan for the ToolTip
            var langStats = BundleEngine.AnalyzeTargetDirectory(directory);

            // Safety check in case the designer controls aren't wired up yet
            if (langStats.HasHeuristicTargets && lblAstWarning != null && astWarningToolTip != null)
            {
                lblAstWarning.Visible = true;

                var tooltipMsg = new System.Text.StringBuilder();
                tooltipMsg.AppendLine("Non-C# code detected. AST modes vary in accuracy:");
                tooltipMsg.AppendLine();

                if (langStats.HasCSharp) tooltipMsg.AppendLine("• C#: 100% accurate (Roslyn AST)");
                if (langStats.HasCpp) tooltipMsg.AppendLine("• C/C++: Heuristic (Macros/preprocessors ignored)");
                if (langStats.HasPython) tooltipMsg.AppendLine("• Python: Heuristic (Indentation based)");
                if (langStats.HasJava) tooltipMsg.AppendLine("• Java: Heuristic (Block-depth based)");

                astWarningToolTip.SetToolTip(lblAstWarning, tooltipMsg.ToString().TrimEnd());
            }
            else if (lblAstWarning != null && astWarningToolTip != null)
            {
                lblAstWarning.Visible = false;
                astWarningToolTip.SetToolTip(lblAstWarning, "");
            }
        }

        private void btnSelectDir_Click(object sender, EventArgs e)
        {
            string dir = txtWorkingDir.Text;
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.SelectedPath = DefaultPath;
                if (fbd.ShowDialog() == DialogResult.OK)
                    DefaultPath = fbd.SelectedPath;
            }
        }

        public string GetDefaultDirectory()
        {
            string currentPath = AppDomain.CurrentDomain.BaseDirectory;
            DirectoryInfo dir = new DirectoryInfo(currentPath);

            while (dir != null)
            {
                // 1. Check if we are currently inside NekoLib or NekoTools directly
                if (dir.Name.Equals("NekoLib", StringComparison.OrdinalIgnoreCase) ||
                    dir.Name.Equals("NekoTools", StringComparison.OrdinalIgnoreCase) ||
                    dir.GetFiles("NekoLib.sln").Any())
                {
                    return dir.FullName;
                }

                // 2. Check if we hit the parent 'dev' workspace that contains both of them
                bool hasNekoLib = dir.GetDirectories("NekoLib").Any();
                bool hasNekoTools = dir.GetDirectories("NekoTools").Any();

                if (hasNekoLib && hasNekoTools)
                {
                    // Returns the parent 'dev' directory itself so you can easily select either
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            // Fallback if we aren't inside the dev tree at all
            return currentPath;
        }

        private async void btnCreateBundle_Click(object sender, EventArgs e)
        {
            string targetDir = txtWorkingDir.Text;
            if (string.IsNullOrWhiteSpace(targetDir) || !System.IO.Directory.Exists(targetDir))
            {
                MessageBox.Show("Please select a valid directory first.");
                return;
            }

            btnCreateBundles.Enabled = false;
            btnCreateAstBundles.Enabled = false;
            btnSelectDir.Enabled = false;
            lblStatus.Text = "Processing...";

            string outputDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bundles");

            var progress = new Progress<string>(msg =>
            {
                lblStatus.Text = msg;
            });

            try
            {
                await Task.Run(() => BundleEngine.ProcessDirectory(targetDir, outputDir, progress));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Processing Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error during processing.";
            }
            finally
            {
                btnCreateBundles.Enabled = true;
                btnCreateAstBundles.Enabled = true;
                btnSelectDir.Enabled = true;
             }
        }

        private async void btnCreateAstBundles_Click(object sender, EventArgs e)
        {
            string targetDir = txtWorkingDir.Text;
            if (string.IsNullOrWhiteSpace(targetDir) || !System.IO.Directory.Exists(targetDir))
            {
                MessageBox.Show("Please select a valid directory first.");
                return;
            }

            btnCreateBundles.Enabled = false;
            btnCreateAstBundles.Enabled = false;
            btnSelectDir.Enabled = false;
            lblStatus.Text = "Processing AST Bundles...";

            string outputDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ATS_Bundles");

            var progress = new Progress<string>(msg =>
            {
                lblStatus.Text = msg;
            });

            try
            {
                await Task.Run(() => BundleEngine.ProcessDirectory(targetDir, outputDir, progress, astMode: true));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Processing Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error during processing.";
            }
            finally
            {
                btnCreateBundles.Enabled = true;
                btnCreateAstBundles.Enabled = true;
                btnSelectDir.Enabled = true;
             }
        }
        private void btnBundleIgnore_Click(object sender, EventArgs e)
        {
            using (var config = new ConfigForm(DefaultPath))
                config.ShowDialog();
        }
        private void btnOpenFolder_Click(object sender, EventArgs e)
        {
            string outputDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bundles");

            if (System.IO.Directory.Exists(outputDir))
            {
                System.Diagnostics.Process.Start("explorer.exe", outputDir);
            }
            else
            {
                MessageBox.Show("The standard 'bundles' folder does not exist yet.", "Folder Not Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

       

        private void btnOpenAtsFolder_Click(object sender, EventArgs e)
        {
            string astOutputDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ATS_Bundles");

            if (System.IO.Directory.Exists(astOutputDir))
            {
                System.Diagnostics.Process.Start("explorer.exe", astOutputDir);
            }
            else
            {
                MessageBox.Show("The 'ATS_Bundles' folder does not exist yet.", "Folder Not Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

        }
    }
}