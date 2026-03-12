using System;
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
            get=> _defaultPath;
            set
            {
                _defaultPath = value;
                txtWorkingDir.Text = DefaultPath;
                // Bonus: Automatically enable the button if the path is valid
                btnCreateBundle.Enabled = !string.IsNullOrWhiteSpace(DefaultPath) && Directory.Exists(DefaultPath);
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
            this.BackColor = Color.FromArgb(30, 30, 30); // #1E1E1E
            this.ForeColor = Color.White;
            
            // Set styles for controls dynamically
            foreach (Control control in this.Controls)
            {
                if (control is Button btn)
                {
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderSize = 0;
                    btn.BackColor = Color.FromArgb(45, 45, 48);
                    btn.ForeColor = Color.White;
                    btn.MouseEnter += delegate { btn.BackColor = Color.FromArgb(62, 62, 66); };
                    btn.MouseLeave += delegate { btn.BackColor = Color.FromArgb(45, 45, 48); };
                }
            }

            txtWorkingDir.BackColor = Color.FromArgb(45, 45, 48);
            txtWorkingDir.ForeColor = Color.White;
            txtWorkingDir.BorderStyle = BorderStyle.FixedSingle;

            statusStrip1.BackColor = Color.FromArgb(45, 45, 48);
            statusStrip1.ForeColor = Color.White;
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
                // Check if the current folder is 'NekoLib' 
                // OR contains a unique marker like 'NekoLib.sln'
                if (dir.Name.Equals("NekoLib", StringComparison.OrdinalIgnoreCase) ||
                    dir.GetFiles("NekoLib.sln").Any())
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }

            // Fallback if we aren't inside the NekoLib tree
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

            btnCreateBundle.Enabled = false;
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
                btnCreateBundle.Enabled = true;
                btnSelectDir.Enabled = true;
            }
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
                MessageBox.Show("Bundles folder does not exist yet.");
            }
        }
    }
}
