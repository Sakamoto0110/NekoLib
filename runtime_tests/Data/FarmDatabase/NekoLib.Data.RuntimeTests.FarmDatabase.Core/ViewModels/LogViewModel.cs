#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using NekoLib.Mvvm;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.Model;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.Core.ViewModels
{
    /// <summary>
    /// The audit trail. Every entry here was written inside the same transaction as
    /// the change it describes, so a row appearing without its stock movement (or the
    /// reverse) would be a real defect rather than a display artifact.
    /// </summary>
    public sealed class LogViewModel : FarmViewModelBase
    {
        private readonly BindingList<OperationLogEntry> _entries =
            new BindingList<OperationLogEntry>();

        public LogViewModel(FarmWorkspace workspace) : base(workspace)
        {
            RefreshCommand = new RelayCommand(
                () => Run(RefreshAsync),
                () => IsIdle && IsConnected);
        }

        public BindingList<OperationLogEntry> Entries => _entries;

        public RelayCommand RefreshCommand { get; }

        public string SummaryText
        {
            get
            {
                if (_entries.Count == 0)
                    return "Nenhuma operação registrada ainda.";

                int additions = 0;
                int removals = 0;
                foreach (OperationLogEntry entry in _entries)
                {
                    if (entry.Operation == Operations.Add) additions++;
                    else removals++;
                }

                return _entries.Count + " operação(ões)  ·  " +
                    additions + " entrada(s)  ·  " + removals + " saída(s)";
            }
        }

        private async Task RefreshAsync()
        {
            List<OperationLogEntry> entries =
                await Workspace.Require().GetOperationLogAsync().ConfigureAwait(true);

            _entries.RaiseListChangedEvents = false;
            _entries.Clear();
            foreach (OperationLogEntry entry in entries)
                _entries.Add(entry);
            _entries.RaiseListChangedEvents = true;
            _entries.ResetBindings();

            OnPropertyChanged(nameof(SummaryText));
            StatusMessage = entries.Count + " registro(s) carregado(s).";
        }

        protected override void RaiseCommandStates()
        {
            RefreshCommand.RaiseCanExecuteChanged();
        }

        public override void OnConnectionChanged()
        {
            base.OnConnectionChanged();

            _entries.Clear();
            OnPropertyChanged(nameof(SummaryText));
            RaiseCommandStates();
        }
    }
}
