using System.Windows.Input;
using NekoLib.Mvvm;
using NekoLib.Navigation;

namespace NavigationDemo.Pages.PageE
{
    public sealed class PageFViewModel : ViewModelBase
    {
        public ICommand GoBackCommand { get; }

        public PageFViewModel()
        {
            GoBackCommand = new RelayCommand(async _ => await NavigationService.GoBackAsync());
        }
    }
}
