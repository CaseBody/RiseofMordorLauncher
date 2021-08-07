using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using RiseofMordorLauncher.Directory.Pages;


namespace RiseofMordorLauncher
{
    class WindowViewModel : BaseViewModel
    {
        private readonly SharedData SharedData = new SharedData();

        private readonly MainLauncherViewModel mainLauncherViewModel = new MainLauncherViewModel();
        private LoginViewModel loginViewModel = new LoginViewModel();
        private readonly SubmodsViewModel submodsViewModel = new SubmodsViewModel();

        private MainLauncher MainLauncherPage = new MainLauncher();
        private readonly Login LoginPage     = new Login();
        private readonly SubmodsPage SubmodsPage   = new SubmodsPage();
        public Page CurrentPage { get; set; }

        // Load login page
        public WindowViewModel()
        {
            loginViewModel.SharedData = SharedData;
            loginViewModel.SwitchPageEvent += SwitchPage;
            loginViewModel.Load();
            LoginPage.DataContext = loginViewModel;

            CurrentPage = LoginPage;
        }

        // switch page, can be fired by the page ViewModels
        private async void SwitchPage(object sender, ApplicationPage page)
        {
            switch (page)
            {
                case ApplicationPage.MainLauncher:
                    MainLauncherPage = new MainLauncher();
                    mainLauncherViewModel.SharedData = SharedData;
                    mainLauncherViewModel.SwitchPageEvent += SwitchPage;
                    await mainLauncherViewModel.Load();
                    MainLauncherPage.DataContext = mainLauncherViewModel;
                    CurrentPage = MainLauncherPage;
                    break;

                case ApplicationPage.Login:
                    loginViewModel = new LoginViewModel();
                    loginViewModel.SharedData = SharedData;
                    loginViewModel.SwitchPageEvent += SwitchPage;
                    loginViewModel.Load();
                    CurrentPage = LoginPage;
                    break;

                case ApplicationPage.Submods:
                    //submodsViewModel = new SubmodsViewModel();
                    //submodsViewModel.SharedData = SharedData;
                    //submodsViewModel.SwitchPageEvent += SwitchPage;
                    //submodsViewModel.Load();
                    
                    SubmodsPage.DataContext = submodsViewModel;
                    CurrentPage = SubmodsPage;
                    break;
            }

        }

    }
}
