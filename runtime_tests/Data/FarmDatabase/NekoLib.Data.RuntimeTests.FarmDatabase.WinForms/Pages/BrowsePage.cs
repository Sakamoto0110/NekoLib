using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Metadata.Attributes;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.ViewModels;
using NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Theme;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Pages
{
    /// <summary>
    /// Catalog browser. Reads whichever table is selected through the raw path, so
    /// the grid's columns come from the reader rather than from a DTO.
    /// </summary>
    [PageMetadata(Name = "Tabelas", Role = PageRole.Normal, Tags = new[] { "dados" })]
    [PageReuse(PageReusePolicy.StrongSingleton)]
    public partial class BrowsePage : FarmPageBase
    {
        private BrowseViewModel _vm;
        private bool _suppressListEvent;

        public BrowsePage()
        {
            InitializeComponent();
            ApplyTheme();

            if (IsInert) return;

            _vm = App.Browse;
            WireUp();
            Bind(_vm, _status, ApplyViewModel);
        }

        private void ApplyTheme()
        {
            FarmTheme.StyleGrid(_grid);

            _tablesList.BackColor = FarmTheme.Surface;
            _tablesList.ForeColor = FarmTheme.TextPrimary;
            _tablesList.Font = FarmTheme.FontBody;

            // ListBox has no selection-colour property, so the selected row would keep
            // the system highlight blue and break the palette. Owner-drawing is the
            // only way to theme it.
            _tablesList.DrawMode = DrawMode.OwnerDrawFixed;
            _tablesList.ItemHeight = 24;
            _tablesList.DrawItem += OnDrawTableItem;

            _limitValue.BackColor = FarmTheme.SurfaceAlt;
            _limitValue.ForeColor = FarmTheme.TextPrimary;
            _limitCheck.BackColor = FarmTheme.Surface;
            _toolbar.BackColor = FarmTheme.Surface;
        }

        private void OnDrawTableItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            using (var back = new SolidBrush(selected ? FarmTheme.AccentSoft : FarmTheme.Surface))
                e.Graphics.FillRectangle(back, e.Bounds);

            if (selected)
            {
                using (var bar = new SolidBrush(FarmTheme.Accent))
                    e.Graphics.FillRectangle(bar, e.Bounds.X, e.Bounds.Y + 4, 3, e.Bounds.Height - 8);
            }

            TextRenderer.DrawText(
                e.Graphics,
                Convert.ToString(_tablesList.Items[e.Index]),
                FarmTheme.FontBody,
                new Rectangle(e.Bounds.X + 12, e.Bounds.Y, e.Bounds.Width - 14, e.Bounds.Height),
                selected ? FarmTheme.Accent : FarmTheme.TextPrimary,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void WireUp()
        {
            _tablesList.SelectedIndexChanged += (s, e) =>
            {
                if (_suppressListEvent) return;
                _vm.SelectedTable = _tablesList.SelectedItem as string;
            };

            _limitCheck.CheckedChanged += (s, e) =>
            {
                _vm.LimitRows = _limitCheck.Checked;
                _limitValue.Enabled = _limitCheck.Checked;
            };

            _limitValue.ValueChanged += (s, e) => _vm.RowLimit = (int)_limitValue.Value;

            Bind(_reloadTablesButton, _vm.LoadTablesCommand);
            Bind(_loadRowsButton, _vm.LoadRowsCommand);

            // Double-clicking a table is the fast path: pick and read in one gesture.
            _tablesList.DoubleClick += (s, e) =>
            {
                if (_vm.LoadRowsCommand.CanExecute(null))
                    _vm.LoadRowsCommand.Execute(null);
            };
        }

        /// <summary>
        /// Reloads the catalog on entry. The page is a strong singleton, so this
        /// also picks up a provider switch that happened while it was detached.
        /// </summary>
        public override Task OnNavigatedToAsync(NavigationArgs args)
        {
            if (!IsInert && _vm.LoadTablesCommand.CanExecute(null) && _vm.Tables.Count == 0)
                _vm.LoadTablesCommand.Execute(null);

            return Task.CompletedTask;
        }

        private void ApplyViewModel()
        {
            SyncTableList();

            _dataCard.Title = _vm.HeaderText;
            _rowCountLabel.Text = _vm.RowCountText;

            if (!ReferenceEquals(_grid.DataSource, _vm.Rows))
                _grid.DataSource = _vm.Rows;

            if (_limitCheck.Checked != _vm.LimitRows)
                _limitCheck.Checked = _vm.LimitRows;

            if ((int)_limitValue.Value != _vm.RowLimit)
                _limitValue.Value = Math.Min(_limitValue.Maximum, _vm.RowLimit);

            _dataCard.Invalidate();
        }

        private void SyncTableList()
        {
            if (_tablesList.Items.Count == _vm.Tables.Count)
            {
                bool same = true;
                for (int i = 0; i < _vm.Tables.Count; i++)
                {
                    if (!string.Equals((string)_tablesList.Items[i], _vm.Tables[i], StringComparison.Ordinal))
                    {
                        same = false;
                        break;
                    }
                }
                if (same)
                {
                    SelectCurrentTable();
                    return;
                }
            }

            _suppressListEvent = true;
            _tablesList.BeginUpdate();
            _tablesList.Items.Clear();
            foreach (string table in _vm.Tables)
                _tablesList.Items.Add(table);
            _tablesList.EndUpdate();
            _suppressListEvent = false;

            SelectCurrentTable();
        }

        private void SelectCurrentTable()
        {
            string target = _vm.SelectedTable;
            if (target == null)
            {
                if (_tablesList.SelectedIndex != -1)
                {
                    _suppressListEvent = true;
                    _tablesList.SelectedIndex = -1;
                    _suppressListEvent = false;
                }
                return;
            }

            int index = _tablesList.Items.IndexOf(target);
            if (index >= 0 && _tablesList.SelectedIndex != index)
            {
                _suppressListEvent = true;
                _tablesList.SelectedIndex = index;
                _suppressListEvent = false;
            }
        }
    }
}
