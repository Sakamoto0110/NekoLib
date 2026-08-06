using System;
using System.ComponentModel;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Metadata.Attributes;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.ViewModels;
using NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Theme;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Pages
{
    /// <summary>
    /// Provider selection. Registered entirely by attribute - the shell never names
    /// this type when bootstrapping.
    /// </summary>
    [PageMetadata(Name = "Conexão", Role = PageRole.Normal, Tags = new[] { "dados" })]
    [PageReuse(PageReusePolicy.StrongSingleton)]
    public partial class ConnectionPage : FarmPageBase
    {
        private ConnectionViewModel _vm;
        private bool _suppressComboEvent;

        public ConnectionPage()
        {
            InitializeComponent();
            ApplyTheme();

            // Guard everything below: at design time there is no workspace, and the
            // page must render as an empty layout instead of throwing in the designer.
            if (IsInert) return;

            _vm = App.Connection;
            WireUp();
            Bind(_vm, _status, ApplyViewModel);
        }

        private void ApplyTheme()
        {
            FarmTheme.StyleCombo(_providerCombo);
            FarmTheme.StyleInput(_connValue);
            _connValue.Font = FarmTheme.FontMono;
            _recreateCheck.BackColor = FarmTheme.Surface;
        }

        private void WireUp()
        {
            _suppressComboEvent = true;
            _providerCombo.Items.Clear();
            foreach (ProviderChoice choice in _vm.Choices)
                _providerCombo.Items.Add(choice);
            _providerCombo.SelectedItem = _vm.Selected;
            _suppressComboEvent = false;

            _providerCombo.SelectedIndexChanged += (s, e) =>
            {
                if (_suppressComboEvent) return;
                _vm.Selected = _providerCombo.SelectedItem as ProviderChoice;
            };

            _recreateCheck.CheckedChanged += (s, e) => _vm.Recreate = _recreateCheck.Checked;

            Bind(_connectButton, _vm.ConnectCommand);
            Bind(_disconnectButton, _vm.DisconnectCommand);
        }

        /// <summary>Pulls current view-model state onto the controls.</summary>
        private void ApplyViewModel()
        {
            _pathValue.Text = _vm.DatabasePath;
            _connValue.Text = _vm.ConnectionString;
            _dialectValue.Text = _vm.DialectNotes;

            string warning = _vm.UnavailableReason;
            _warningValue.Text = warning;
            _warningValue.Visible = !string.IsNullOrEmpty(warning);

            if (_recreateCheck.Checked != _vm.Recreate)
                _recreateCheck.Checked = _vm.Recreate;

            if (!ReferenceEquals(_providerCombo.SelectedItem, _vm.Selected))
            {
                _suppressComboEvent = true;
                _providerCombo.SelectedItem = _vm.Selected;
                _suppressComboEvent = false;
            }

            _header.Subtitle = _vm.ConnectedSummary;
            _header.Invalidate();
        }
    }
}
