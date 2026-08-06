using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Forms;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Metadata.Attributes;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.ViewModels;
using NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Theme;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Pages
{
    /// <summary>
    /// Hand-written SQL. The sample list changes with the connected engine, which is
    /// the shortest way to show that a statement written for one provider does not
    /// necessarily run on the other.
    /// </summary>
    [PageMetadata(Name = "Consulta livre", Role = PageRole.Normal, Tags = new[] { "dados" })]
    [PageReuse(PageReusePolicy.StrongSingleton)]
    public partial class RawQueryPage : FarmPageBase
    {
        private RawQueryViewModel _vm;
        private bool _suppressTextEvent;

        public RawQueryPage()
        {
            InitializeComponent();
            ApplyTheme();

            if (IsInert) return;

            _vm = App.RawQuery;
            WireUp();
            Bind(_vm, _status, ApplyViewModel);
        }

        private void ApplyTheme()
        {
            FarmTheme.StyleGrid(_grid);
            FarmTheme.StyleInput(_sqlBox);
            FarmTheme.StyleCombo(_samplesCombo);

            _sqlBox.Font = FarmTheme.FontMono;
            _editorBar.BackColor = FarmTheme.Surface;
            _editorButtons.BackColor = FarmTheme.Surface;
            _layout.BackColor = FarmTheme.Canvas;
        }

        private void WireUp()
        {
            _sqlBox.TextChanged += (s, e) =>
            {
                if (_suppressTextEvent) return;
                _vm.Sql = _sqlBox.Text;
            };

            // Ctrl+Enter runs, which is what anyone who has used a SQL client expects.
            _sqlBox.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    if (_vm.ExecuteCommand.CanExecute(null))
                        _vm.ExecuteCommand.Execute(null);
                }
            };

            _samplesCombo.SelectedIndexChanged += (s, e) =>
            {
                if (_samplesCombo.SelectedItem is QuerySample sample)
                    _vm.Sql = sample.Sql;
            };

            Bind(_runButton, _vm.ExecuteCommand);
            Bind(_clearButton, _vm.ClearCommand);
        }

        public override Task OnNavigatedToAsync(NavigationArgs args)
        {
            if (!IsInert)
                ReloadSamples();

            return Task.CompletedTask;
        }

        /// <summary>
        /// The sample set is provider-dependent, so it is rebuilt on entry rather
        /// than once in the constructor.
        /// </summary>
        private void ReloadSamples()
        {
            _samplesCombo.BeginUpdate();
            _samplesCombo.Items.Clear();
            foreach (QuerySample sample in _vm.Samples)
                _samplesCombo.Items.Add(sample);
            _samplesCombo.EndUpdate();
        }

        private void ApplyViewModel()
        {
            if (_sqlBox.Text != _vm.Sql)
            {
                _suppressTextEvent = true;
                _sqlBox.Text = _vm.Sql;
                _suppressTextEvent = false;
            }

            if (!ReferenceEquals(_grid.DataSource, _vm.Result))
                _grid.DataSource = _vm.Result;

            _resultCard.Subtitle = _vm.ResultText;
            _resultCard.Invalidate();
        }
    }
}
