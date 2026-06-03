using System.Windows.Input;
using NekoLib.Mvvm;
using NekoLib.Navigation;

namespace NavigationDemo.Pages.PageC
{
    public sealed class PageCViewModel : ViewModelBase
    {
        public ICommand GoBackCommand { get; }

        public PageCViewModel()
        {
            GoBackCommand = new RelayCommand(async _ => await NavigationService.GoBackAsync());
        }
    }
}
