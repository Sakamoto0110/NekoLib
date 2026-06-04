using NekoLib.Mvvm;

namespace NekoLib.Navigation.RuntimeTests.Winforms481.Pages.SimpleToast
{
    public sealed class SimpleToastViewModel : ViewModelBase
    {
        private string _message = "Hello from a Toast!";

        public string Message
        {
            get { return _message; }
            set { SetProperty(ref _message, value); }
        }
    }
}
