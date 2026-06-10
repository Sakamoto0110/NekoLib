using NekoLib.Mvvm;

namespace NekoLib.Navigation.RuntimeTests.Winforms481.Pages.HeavyPage
{
    public sealed class HeavyPageViewModel : ViewModelBase
    {
        private string _statusText = "Loading...";

        public string StatusText
        {
            get { return _statusText; }
            set { SetProperty(ref _statusText, value); }
        }
    }
}
