using System.Windows.Forms;
using NekoLib.Navigation.WinForms.Hosting;

namespace NekoLib.Navigation.RuntimeTests.Winforms481.Pages.TextInputPrompt
{
    public partial class TextInputPromptView : PromptViewBase<string>
    {
        private readonly TextInputPromptViewModel _viewModel;

        public TextInputPromptView()
        {
            InitializeComponent();

            _viewModel = new TextInputPromptViewModel(
                onComplete: result => CompletePrompt(result));

            txtInput.DataBindings.Add(
                nameof(TextBox.Text),
                _viewModel,
                nameof(TextInputPromptViewModel.InputText),
                false,
                DataSourceUpdateMode.OnPropertyChanged);

            btnOk.Click += (s, e) => _viewModel.OkCommand.Execute(null);
        }
    }
}
