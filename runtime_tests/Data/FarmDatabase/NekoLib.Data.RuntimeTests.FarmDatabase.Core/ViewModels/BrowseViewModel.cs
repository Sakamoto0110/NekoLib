#nullable enable
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using NekoLib.Mvvm;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.Core.ViewModels
{
    /// <summary>
    /// Table browser: discovers the catalog, then reads whichever table is selected.
    /// Both halves are provider-sensitive - the catalog because Access has no
    /// queryable one, the read because <c>Top(n)</c> renders differently per dialect.
    /// </summary>
    public sealed class BrowseViewModel : FarmViewModelBase
    {
        private readonly List<string> _tables = new List<string>();
        private string? _selectedTable;
        private DataTable? _rows;
        private bool _limitRows = true;
        private int _rowLimit = 100;

        public BrowseViewModel(FarmWorkspace workspace) : base(workspace)
        {
            LoadTablesCommand = new RelayCommand(
                () => Run(LoadTablesAsync),
                () => IsIdle && IsConnected);

            LoadRowsCommand = new RelayCommand(
                () => Run(LoadRowsAsync),
                () => IsIdle && IsConnected && SelectedTable != null);
        }

        public IReadOnlyList<string> Tables => _tables;

        public string? SelectedTable
        {
            get => _selectedTable;
            set
            {
                if (SetProperty(ref _selectedTable, value))
                {
                    LoadRowsCommand.RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(HeaderText));
                }
            }
        }

        public DataTable? Rows
        {
            get => _rows;
            private set
            {
                if (SetProperty(ref _rows, value))
                {
                    OnPropertyChanged(nameof(RowCountText));
                    OnPropertyChanged(nameof(HeaderText));
                }
            }
        }

        /// <summary>Whether to cap the read with <c>Top(n)</c>.</summary>
        public bool LimitRows
        {
            get => _limitRows;
            set => SetProperty(ref _limitRows, value);
        }

        public int RowLimit
        {
            get => _rowLimit;
            set => SetProperty(ref _rowLimit, value < 1 ? 1 : value);
        }

        public RelayCommand LoadTablesCommand { get; }
        public RelayCommand LoadRowsCommand { get; }

        public string HeaderText => SelectedTable == null
            ? "Selecione uma tabela"
            : SelectedTable;

        public string RowCountText => Rows == null
            ? string.Empty
            : Rows.Rows.Count + " linha(s) · " + Rows.Columns.Count + " coluna(s)";

        private async Task LoadTablesAsync()
        {
            IReadOnlyList<string> tables =
                await Workspace.Require().ListTablesAsync().ConfigureAwait(true);

            _tables.Clear();
            _tables.AddRange(tables);
            OnPropertyChanged(nameof(Tables));

            if (_selectedTable == null || !_tables.Contains(_selectedTable))
                SelectedTable = _tables.Count > 0 ? _tables[0] : null;

            StatusMessage = _tables.Count + " tabela(s) no catálogo.";
        }

        private async Task LoadRowsAsync()
        {
            string? table = SelectedTable;
            if (table == null) return;

            Rows = await Workspace.Require()
                .ReadTableAsync(table, LimitRows ? RowLimit : (int?)null)
                .ConfigureAwait(true);

            StatusMessage = "Lido de " + table + ".";
        }

        protected override void RaiseCommandStates()
        {
            LoadTablesCommand.RaiseCanExecuteChanged();
            LoadRowsCommand.RaiseCanExecuteChanged();
        }

        public override void OnConnectionChanged()
        {
            base.OnConnectionChanged();

            _tables.Clear();
            OnPropertyChanged(nameof(Tables));
            SelectedTable = null;
            Rows = null;

            RaiseCommandStates();
        }
    }
}
