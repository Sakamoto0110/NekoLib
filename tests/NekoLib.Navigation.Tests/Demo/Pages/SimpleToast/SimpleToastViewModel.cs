using NekoLib.Mvvm;

namespace NavigationDemo.Pages.SimpleToast
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
