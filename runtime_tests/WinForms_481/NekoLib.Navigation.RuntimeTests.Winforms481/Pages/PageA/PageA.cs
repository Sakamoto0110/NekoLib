using NekoLib.Navigation.WinForms.Hosting;

namespace NekoLib.Navigation.RuntimeTests.Winforms481.Pages.PageA
{
    public partial class PageA : PageView
    {
        private readonly PageAViewModel _viewModel;

        public PageA()
        {
            InitializeComponent();

            _viewModel = new PageAViewModel();

            btnBack.Click += (s, e) => _viewModel.GoBackCommand.Execute(null);
            btnToB.Click  += (s, e) => _viewModel.GoToBCommand.Execute(null);
            btnToC.Click  += (s, e) => _viewModel.GoToCCommand.Execute(null);
            btnToD.Click  += (s, e) => _viewModel.GoToDCommand.Execute(null);
            btnToE.Click  += (s, e) => _viewModel.GoToECommand.Execute(null);
        }
    }
}
