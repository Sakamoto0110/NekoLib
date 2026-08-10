using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BundlerTool
{
    public partial class ConfigForm : Form
    {
        private string _rootPath;
        private string _ignoreFilePath;

        public ConfigForm(string rootPath)
        {
            InitializeComponent();
            _rootPath = rootPath;
            
            // Only assign if rootPath is valid, else fallback
            if (!string.IsNullOrEmpty(_rootPath))
            {
                _ignoreFilePath = Path.Combine(_rootPath, ".bundleignore");
            }

            ApplyDarkTheme();
            this.Load += ConfigForm_Load;
        }

        private void ApplyDarkTheme()
        {
            this.BackColor = Color.FromArgb(35, 31, 40);
            this.ForeColor = Color.White;

            // Purple drift for the TreeView background
            treeView1.BackColor = Color.FromArgb(45, 40, 52);
            treeView1.ForeColor = Color.White;
            treeView1.BorderStyle = BorderStyle.FixedSingle;

            StyleButton(btnSave);
            StyleButton(btnRestore);
            StyleButton(btnCheckAll);
            StyleButton(btnUncheckAll);
        }

        private void StyleButton(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = Color.FromArgb(50, 45, 60);
            btn.ForeColor = Color.White;
            btn.MouseEnter += delegate { btn.BackColor = Color.FromArgb(70, 60, 85); };
            btn.MouseLeave += delegate { btn.BackColor = Color.FromArgb(50, 45, 60); };
        }

     

        private void ConfigForm_Load(object sender, EventArgs e)
        {
            lblRootPath.Text = $"Path: {_rootPath}";
            lblInstructions.Text = "Checked paths are ignored by every bundle mode.";
            PopulateTreeView();
            LoadIgnoreList();
            AdjustFormLayout();
            this.Resize += (s, ev) => PositionButtons();
        }

        private void AdjustFormLayout()
        {
            int maxNodeWidth = 0;
            using (Graphics g = treeView1.CreateGraphics())
            {
                CalculateMaxNodeWidth(treeView1.Nodes, g, ref maxNodeWidth);
            }
            
            int minWidthForButtons = 500; // Ensuring 4 buttons look nice
            int requiredTreeWidth = maxNodeWidth + 60; // padding, treeview plus scrollbar
            int requiredFormWidth = requiredTreeWidth + 40; // margins
            
            int newWidth = Math.Max(minWidthForButtons, requiredFormWidth);
            newWidth = Math.Min(newWidth, 1200); // cap max width
            
            this.Width = newWidth;
            PositionButtons();
        }

        private void PositionButtons()
        {
            int btnSpacing = 10;
            // Left padding 12, right padding 12 => 24
            int totalBtnWidth = this.ClientSize.Width - 24 - (3 * btnSpacing);
            int singleBtnWidth = totalBtnWidth / 4;

            int y = this.ClientSize.Height - 45; // 35 button height + 10 margin
            
            btnCheckAll.Bounds = new Rectangle(12, y, singleBtnWidth, 35);
            btnUncheckAll.Bounds = new Rectangle(12 + singleBtnWidth + btnSpacing, y, singleBtnWidth, 35);
            btnRestore.Bounds = new Rectangle(12 + (singleBtnWidth * 2) + (btnSpacing * 2), y, singleBtnWidth, 35);
            btnSave.Bounds = new Rectangle(12 + (singleBtnWidth * 3) + (btnSpacing * 3), y, singleBtnWidth, 35);
        }

        private void CalculateMaxNodeWidth(TreeNodeCollection nodes, Graphics g, ref int maxWidth)
        {
            foreach (TreeNode node in nodes)
            {
                int nodeWidth = (node.Level * 20) + (int)g.MeasureString(node.Text, treeView1.Font).Width;
                if (nodeWidth > maxWidth)
                {
                    maxWidth = nodeWidth;
                }
                CalculateMaxNodeWidth(node.Nodes, g, ref maxWidth);
            }
        }

        private void PopulateTreeView()
        {
            treeView1.Nodes.Clear();
            if (!string.IsNullOrEmpty(_rootPath) && Directory.Exists(_rootPath))
            {
                TreeNode rootNode = new TreeNode(Path.GetFileName(_rootPath));
                rootNode.Tag = _rootPath;
                rootNode.Checked = false;
                treeView1.Nodes.Add(rootNode);
                
                BuildTree(rootNode, _rootPath);
                rootNode.Expand();
            }
        }

        private void BuildTree(TreeNode rootNode, string path)
        {
            try
            {
                string[] dirs = Directory.GetDirectories(path);
                foreach (string dir in dirs.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
                {
                    var directoryInfo = new DirectoryInfo(dir);
                    if (BundleEngine.IsIgnoredDirectoryName(directoryInfo.Name) ||
                        (directoryInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }

                    TreeNode node = new TreeNode(Path.GetFileName(dir));
                    node.Tag = dir;
                    node.Checked = false;
                    BuildTree(node, dir);
                    if (node.Nodes.Count > 0)
                    {
                        rootNode.Nodes.Add(node);
                    }
                }

                string[] files = Directory.GetFiles(path);
                foreach (string file in files
                    .Where(BundleEngine.IsBundledFile)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
                {
                    TreeNode node = new TreeNode(Path.GetFileName(file));
                    node.Tag = file;
                    node.Checked = false;
                    rootNode.Nodes.Add(node);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (Exception) { }
        }

        private void LoadIgnoreList()
        {
            if (!string.IsNullOrEmpty(_ignoreFilePath) && File.Exists(_ignoreFilePath))
            {
                var ignoredPaths = BundleEngine.ReadIgnoredPaths(_rootPath);
                CheckIgnoredNodes(treeView1.Nodes, ignoredPaths);
            }
        }

        private void CheckIgnoredNodes(TreeNodeCollection nodes, HashSet<string> ignoredPaths)
        {
            foreach (TreeNode node in nodes)
            {
                string path = node.Tag as string;
                if (path != null && ignoredPaths.Contains(path))
                {
                    node.Checked = true;
                    CheckAllChildNodes(node, true);
                    continue;
                }
                CheckIgnoredNodes(node.Nodes, ignoredPaths);
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_ignoreFilePath))
            {
                MessageBox.Show("Invalid root path.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            List<string> ignoredPaths = new List<string>();
            FindCheckedNodes(treeView1.Nodes, ignoredPaths);

            try
            {
                File.WriteAllLines(_ignoreFilePath, ignoredPaths);
                MessageBox.Show(".bundleignore saved successfully.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void FindCheckedNodes(TreeNodeCollection nodes, List<string> ignoredPaths)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Checked)
                {
                    string path = node.Tag as string;
                    if (!string.IsNullOrEmpty(path))
                    {
                        ignoredPaths.Add(path);
                    }
                    continue;
                }
                FindCheckedNodes(node.Nodes, ignoredPaths);
            }
        }

        private void treeView1_AfterCheck(object sender, TreeViewEventArgs e)
        {
            // If the user actively changed the checkbox (not done by code)
            if (e.Action != TreeViewAction.Unknown)
            {
                CheckAllChildNodes(e.Node, e.Node.Checked);
                if (!e.Node.Checked)
                {
                    UncheckParentNodes(e.Node.Parent);
                }
            }
        }

        private static void UncheckParentNodes(TreeNode node)
        {
            while (node != null)
            {
                node.Checked = false;
                node = node.Parent;
            }
        }

        private void CheckAllChildNodes(TreeNode treeNode, bool nodeChecked)
        {
            foreach (TreeNode node in treeNode.Nodes)
            {
                node.Checked = nodeChecked;
                if (node.Nodes.Count > 0)
                {
                    CheckAllChildNodes(node, nodeChecked);
                }
            }
        }

        private void btnCheckAll_Click(object sender, EventArgs e)
        {
            SetAllNodesChecked(treeView1.Nodes, true);
        }

        private void btnUncheckAll_Click(object sender, EventArgs e)
        {
            SetAllNodesChecked(treeView1.Nodes, false);
        }

        private void SetAllNodesChecked(TreeNodeCollection nodes, bool isChecked)
        {
            foreach (TreeNode node in nodes)
            {
                node.Checked = isChecked;
                SetAllNodesChecked(node.Nodes, isChecked);
            }
        }

        private void btnRestore_Click(object sender, EventArgs e)
        {
            // Restore exact state as when loaded, without closing
            SetAllNodesChecked(treeView1.Nodes, false);
            LoadIgnoreList();
        }
    }
}
