using System.Windows.Input;
using NekoLib.Mvvm;
using NekoLib.Navigation;

namespace NavigationDemo.Pages.PageB
{
    public sealed class PageBViewModel : ViewModelBase
    {
        public ICommand GoBackCommand { get; }

        public PageBViewModel()
        {
            GoBackCommand = new RelayCommand(async _ => await NavigationService.GoBackAsync());
        }
    }
}
