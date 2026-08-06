using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Forms;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Metadata.Attributes;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.Model;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.ViewModels;
using NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Theme;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Pages
{
    /// <summary>The audit trail, newest first.</summary>
    [PageMetadata(Name = "Log de operações", Role = PageRole.Normal, Tags = new[] { "dados" })]
    [PageReuse(PageReusePolicy.StrongSingleton)]
    public partial class LogPage : FarmPageBase
    {
        private LogViewModel _vm;

        public LogPage()
        {
            InitializeComponent();
            ApplyTheme();

            if (IsInert) return;

            _vm = App.Log;
            _grid.AutoGenerateColumns = false;
            BuildColumns();
            _grid.DataSource = _vm.Entries;

            Bind(_refreshButton, _vm.RefreshCommand);
            Bind(_vm, _status, ApplyViewModel);
        }

        private void ApplyTheme()
        {
            FarmTheme.StyleGrid(_grid);
            _toolbar.BackColor = FarmTheme.Surface;
        }

        /// <summary>
        /// Explicit columns rather than auto-generation: the DTO carries a raw
        /// ISO-8601 string plus a parsed convenience property, and only the parsed one
        /// belongs on screen.
        /// </summary>
        private void BuildColumns()
        {
            _grid.Columns.Clear();

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Quando",
                DataPropertyName = nameof(OperationLogEntry.OccurredAtLocal),
                DefaultCellStyle = { Format = "dd/MM HH:mm:ss" },
                FillWeight = 90
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Tipo",
                DataPropertyName = nameof(OperationLogEntry.EntityKind),
                FillWeight = 60
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Item",
                DataPropertyName = nameof(OperationLogEntry.EntityName),
                FillWeight = 150
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Operação",
                DataPropertyName = nameof(OperationLogEntry.Operation),
                FillWeight = 70
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Qtd",
                DataPropertyName = nameof(OperationLogEntry.Quantity),
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight },
                FillWeight = 45
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Motivo",
                DataPropertyName = nameof(OperationLogEntry.Reason),
                FillWeight = 220
            });

            // Removals read as the exceptional event, so they get the accent colour.
            _grid.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= _vm.Entries.Count) return;

                OperationLogEntry entry = _vm.Entries[e.RowIndex];
                e.CellStyle.ForeColor = entry.Operation == Operations.Remove
                    ? FarmTheme.Warn
                    : FarmTheme.TextPrimary;
            };
        }

        public override Task OnNavigatedToAsync(NavigationArgs args)
        {
            if (!IsInert && _vm.RefreshCommand.CanExecute(null))
                _vm.RefreshCommand.Execute(null);

            return Task.CompletedTask;
        }

        private void ApplyViewModel()
        {
            _summaryLabel.Text = _vm.SummaryText;
        }
    }
}
