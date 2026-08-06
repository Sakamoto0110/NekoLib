#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using NekoLib.Mvvm;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.Providers;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.Core.ViewModels
{
    /// <summary>Describes one provider for the connection page.</summary>
    public sealed class ProviderChoice
    {
        public ProviderChoice(IFarmProviderProfile profile, ProviderAvailability availability)
        {
            Profile = profile;
            Availability = availability;
        }

        public IFarmProviderProfile Profile { get; }
        public ProviderAvailability Availability { get; }

        public FarmProvider Provider => Profile.Provider;
        public string DisplayName => Profile.DisplayName;
        public bool IsAvailable => Availability.IsAvailable;

        public override string ToString() =>
            IsAvailable ? DisplayName : DisplayName + "  (indisponível)";
    }

    public sealed class ConnectionViewModel : FarmViewModelBase
    {
        private ProviderChoice? _selected;
        private bool _recreate;

        public ConnectionViewModel(FarmWorkspace workspace) : base(workspace)
        {
            var choices = new List<ProviderChoice>();
            foreach (IFarmProviderProfile profile in workspace.Profiles)
                choices.Add(new ProviderChoice(profile, profile.Probe()));

            Choices = choices;
            _selected = choices.Count > 0 ? choices[0] : null;

            ConnectCommand = new RelayCommand(
                () => Run(ConnectAsync, "Conectado."),
                () => IsIdle && Selected != null && Selected.IsAvailable);

            DisconnectCommand = new RelayCommand(
                () => { Workspace.Disconnect(); StatusMessage = "Desconectado."; },
                () => IsIdle && IsConnected);
        }

        public IReadOnlyList<ProviderChoice> Choices { get; }

        public ProviderChoice? Selected
        {
            get => _selected;
            set
            {
                if (SetProperty(ref _selected, value))
                {
                    OnPropertyChanged(nameof(DatabasePath));
                    OnPropertyChanged(nameof(DialectNotes));
                    OnPropertyChanged(nameof(UnavailableReason));
                    OnPropertyChanged(nameof(ConnectionString));
                    ConnectCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Recreate the file from scratch on the next connect.</summary>
        public bool Recreate
        {
            get => _recreate;
            set => SetProperty(ref _recreate, value);
        }

        public RelayCommand ConnectCommand { get; }
        public RelayCommand DisconnectCommand { get; }

        public string DatabasePath => Selected?.Profile.DatabasePath ?? string.Empty;

        public string DialectNotes => Selected?.Profile.DialectNotes ?? string.Empty;

        public string ConnectionString => Selected?.Profile.ConnectionString ?? string.Empty;

        /// <summary>Reason plus remedy when the selected provider cannot be used.</summary>
        public string UnavailableReason
        {
            get
            {
                ProviderAvailability? availability = Selected?.Availability;
                if (availability == null || availability.IsAvailable)
                    return string.Empty;

                return availability.Reason +
                    (availability.Remedy == null ? string.Empty : "\r\n\r\n" + availability.Remedy);
            }
        }

        public string ConnectedSummary => Workspace.IsConnected
            ? Workspace.Require().Profile.DisplayName + "  ·  " + Workspace.Require().Profile.DatabasePath
            : "Nenhum banco conectado";

        private async Task ConnectAsync()
        {
            ProviderChoice? choice = Selected;
            if (choice == null) return;

            await Workspace.ConnectAsync(choice.Provider, Recreate).ConfigureAwait(true);

            // A recreate is a one-shot action, not a sticky mode: leaving it armed
            // would silently wipe the database on the next reconnect.
            Recreate = false;
        }

        protected override void RaiseCommandStates()
        {
            ConnectCommand.RaiseCanExecuteChanged();
            DisconnectCommand.RaiseCanExecuteChanged();
        }

        public override void OnConnectionChanged()
        {
            base.OnConnectionChanged();
            OnPropertyChanged(nameof(ConnectedSummary));
            RaiseCommandStates();
        }
    }
}
