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

        private MainLauncherViewModel mainLauncherViewModel = new MainLauncherViewModel();
        private LoginViewModel loginViewModel = new LoginViewModel();

        private MainLauncher MainLauncherPage = new MainLauncher();
        private Login LoginPage = new Login();
        public Page CurrentPage { get; set; }

        // Load login page
        public WindowViewModel()
        {
            loginViewModel.SharedData = SharedData;
            loginViewModel.SwitchPageEvent += SwitchPage;
            loginViewModel.Load();
            LoginPage.DataContext = loginViewModel;
            LoginPage.InitializeComponent();
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
                    MainLauncherPage.InitializeComponent();
                    CurrentPage = MainLauncherPage;
                    break;

                case ApplicationPage.Login:
                    loginViewModel = new LoginViewModel();
                    loginViewModel.SharedData = SharedData;
                    loginViewModel.SwitchPageEvent += SwitchPage;
                    loginViewModel.Load();
                    CurrentPage = LoginPage;
                    LoginPage.InitializeComponent();
                    break;
            }


        }

    }
}
