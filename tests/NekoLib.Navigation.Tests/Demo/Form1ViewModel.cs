using System.Windows.Input;
using NavigationDemo.Core;
using NekoLib.Navigation;
using PageAView = NavigationDemo.Pages.PageA.PageA;
using PageBView = NavigationDemo.Pages.PageB.PageB;
using HomeView = NavigationDemo.Pages.Home.HomePage;
using HeavyView = NavigationDemo.Pages.HeavyPage.HeavyPage;

namespace NavigationDemo
{
    public sealed class Form1ViewModel : ViewModelBase
    {
        public ICommand GoHomeOnLoadCommand { get; }
        public ICommand GoPageACommand { get; }
        public ICommand GoPageBCommand { get; }
        public ICommand GoHeavyPageCommand { get; }
        public ICommand GoHomeCommand { get; }

        public Form1ViewModel()
        {
            GoHomeOnLoadCommand = new RelayCommand(async _ => await NavigationService.GoHomeAsync());
            GoPageACommand      = new RelayCommand(async _ => await NavigationService.SwitchPage<PageAView>());
            GoPageBCommand      = new RelayCommand(async _ => await NavigationService.SwitchPage<PageBView>());
            GoHeavyPageCommand  = new RelayCommand(async _ => await NavigationService.SwitchPage<HeavyView>());
            GoHomeCommand       = new RelayCommand(async _ => await NavigationService.SwitchPage<HomeView>());
        }
    }
}
