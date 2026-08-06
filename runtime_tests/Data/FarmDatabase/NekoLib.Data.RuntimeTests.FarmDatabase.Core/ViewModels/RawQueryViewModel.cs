#nullable enable
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using NekoLib.Mvvm;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.Providers;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.Core.ViewModels
{
    /// <summary>A ready-made statement offered as a starting point.</summary>
    public sealed class QuerySample
    {
        public QuerySample(string label, string sql)
        {
            Label = label;
            Sql = sql;
        }

        public string Label { get; }
        public string Sql { get; }

        public override string ToString() => Label;
    }

    /// <summary>
    /// Free-form SQL against the open database. Statements are passed through
    /// untouched - no builder, no translator - which makes this the page where the
    /// two dialects visibly diverge.
    /// </summary>
    public sealed class RawQueryViewModel : FarmViewModelBase
    {
        private string _sql = string.Empty;
        private DataTable? _result;
        private string _resultText = string.Empty;

        public RawQueryViewModel(FarmWorkspace workspace) : base(workspace)
        {
            ExecuteCommand = new RelayCommand(
                () => Run(ExecuteAsync),
                () => IsIdle && IsConnected && !string.IsNullOrWhiteSpace(Sql));

            ClearCommand = new RelayCommand(
                () => { Sql = string.Empty; Result = null; ResultText = string.Empty; },
                () => IsIdle);
        }

        public string Sql
        {
            get => _sql;
            set
            {
                if (SetProperty(ref _sql, value))
                    ExecuteCommand.RaiseCanExecuteChanged();
            }
        }

        public DataTable? Result
        {
            get => _result;
            private set => SetProperty(ref _result, value);
        }

        public string ResultText
        {
            get => _resultText;
            private set => SetProperty(ref _resultText, value);
        }

        public RelayCommand ExecuteCommand { get; }
        public RelayCommand ClearCommand { get; }

        /// <summary>
        /// Samples for the connected engine. The row-limit and catalog entries differ
        /// on purpose: they are the shortest demonstration that "provider-neutral SQL"
        /// stops being neutral the moment it is hand-written.
        /// </summary>
        public IReadOnlyList<QuerySample> Samples
        {
            get
            {
                var samples = new List<QuerySample>
                {
                    new QuerySample(
                        "Estoque por categoria",
                        "SELECT [Category], COUNT(*) AS Itens, SUM([Quantity]) AS Total\r\n" +
                        "FROM [Products]\r\nGROUP BY [Category]"),

                    new QuerySample(
                        "Funcionários com cargo",
                        "SELECT e.[Name], e.[Age], e.[Phone], r.[Title]\r\n" +
                        "FROM [Employees] e\r\n" +
                        "INNER JOIN [Roles] r ON e.[RoleId] = r.[Id]\r\n" +
                        "ORDER BY r.[Title], e.[Name]"),

                    new QuerySample(
                        "Animais por espécie e gênero",
                        "SELECT [Species], [Gender], COUNT(*) AS Qtd\r\n" +
                        "FROM [Animals]\r\nGROUP BY [Species], [Gender]"),

                    new QuerySample(
                        "Log mais recente",
                        "SELECT [OccurredAt], [EntityKind], [EntityName], [Operation], [Quantity], [Reason]\r\n" +
                        "FROM [OperationLog]\r\nORDER BY [Id] DESC")
                };

                bool isAccess = Workspace.IsConnected &&
                    Workspace.Require().Profile.Provider == FarmProvider.Access;

                samples.Add(isAccess
                    ? new QuerySample(
                        "Limite de linhas (TOP - Access)",
                        "SELECT TOP 5 [Name], [Quantity], [Unit]\r\nFROM [Products]\r\n" +
                        "ORDER BY [Quantity] DESC")
                    : new QuerySample(
                        "Limite de linhas (LIMIT - SQLite)",
                        "SELECT [Name], [Quantity], [Unit]\r\nFROM [Products]\r\n" +
                        "ORDER BY [Quantity] DESC\r\nLIMIT 5"));

                samples.Add(isAccess
                    ? new QuerySample(
                        "Catálogo (Access não expõe por SQL)",
                        "-- O ACE não tem catálogo consultável sem permissão em MSysObjects.\r\n" +
                        "-- A página de Tabelas usa o schema rowset do OleDb.\r\n" +
                        "SELECT COUNT(*) AS Produtos FROM [Products]")
                    : new QuerySample(
                        "Catálogo (sqlite_master)",
                        "SELECT [name], [sql]\r\nFROM sqlite_master\r\nWHERE type = 'table'"));

                return samples;
            }
        }

        private async Task ExecuteAsync()
        {
            RawQueryResult result =
                await Workspace.Require().ExecuteRawAsync(Sql).ConfigureAwait(true);

            Result = result.Rows;
            ResultText = result.Describe();
            StatusMessage = result.Describe();
        }

        protected override void RaiseCommandStates()
        {
            ExecuteCommand.RaiseCanExecuteChanged();
            ClearCommand.RaiseCanExecuteChanged();
        }

        public override void OnConnectionChanged()
        {
            base.OnConnectionChanged();
            Result = null;
            ResultText = string.Empty;
            OnPropertyChanged(nameof(Samples));
            RaiseCommandStates();
        }
    }
}
