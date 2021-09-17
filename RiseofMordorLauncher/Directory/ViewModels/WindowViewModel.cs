using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using Steamworks;
using RiseofMordorLauncher.Directory.Pages;
using System.Threading;
using RiseofMordorLauncher.Directory.Services;
using DiscordRPC;
using System.IO;
using System.Diagnostics;

namespace RiseofMordorLauncher
{
    class WindowViewModel : BaseViewModel
    {
        private DiscordRpcClient RpcClient;

        public event EventHandler MinimizeEvent;
        public event EventHandler CloseEvent;

        private readonly SharedData SharedData = new SharedData();

        private MainLauncherViewModel mainLauncherViewModel;
        private LoginViewModel loginViewModel = new LoginViewModel();
        private SubmodsViewModel submodsViewModel = new SubmodsViewModel();

        private MainLauncher MainLauncherPage;
        private readonly Login LoginPage     = new Login();
        private readonly SubmodsPage SubmodsPage   = new SubmodsPage();
        private Thread UpdateThread;
        public Page CurrentPage { get; set; }

        // Load login page
        public WindowViewModel()
        {
            RpcClient = new DiscordRpcClient("748940888494833754");
            RpcClient.Initialize();

            loginViewModel.SharedData = SharedData;
            loginViewModel.SwitchPageEvent += SwitchPage;
            loginViewModel.Load();
            LoginPage.DataContext = loginViewModel;
            SharedData.AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            SharedData.RPCClient = RpcClient;

            CurrentPage = LoginPage;

            UpdateThread = new Thread(Update);
            UpdateThread.IsBackground = true;
            UpdateThread.Start();
            
            var launcherAppDataPath = $@"{SharedData.AppData}\RiseofMordor\RiseofMordorLauncher\";
            if (System.IO.Directory.Exists(launcherAppDataPath) == false)
            {
                System.IO.Directory.CreateDirectory(launcherAppDataPath);
            }
        }

        // switch page, can be fired by the page ViewModels
        private async void SwitchPage(object sender, ApplicationPage page)
        {
            switch (page)
            {
                case ApplicationPage.MainLauncher:
                    MainLauncherPage = new MainLauncher(SharedData);
                    mainLauncherViewModel = new MainLauncherViewModel();
                    mainLauncherViewModel.SharedData = SharedData;
                    mainLauncherViewModel.SwitchPageEvent += SwitchPage;
                    await mainLauncherViewModel.Load();
                    MainLauncherPage.DataContext = mainLauncherViewModel;
                    CurrentPage = MainLauncherPage;

                    RpcClient.SetPresence(new RichPresence()
                    {
                        Details = "Rise of Mordor Launcher",
                        State = "On the main page",
                        Assets = new Assets()
                        {
                            LargeImageKey = "large_image",
                            LargeImageText = "discord.com/riseofmordor",
                        }
                    });
                    break;

                case ApplicationPage.Login:
                    loginViewModel = new LoginViewModel();
                    loginViewModel.SharedData = SharedData;
                    loginViewModel.SwitchPageEvent += SwitchPage;
                    loginViewModel.Load();
                    CurrentPage = LoginPage;
                    break;

                case ApplicationPage.Submods:
                    submodsViewModel = new SubmodsViewModel();
                    submodsViewModel.SwitchPageEvent += SwitchPage;
                    submodsViewModel.sharedData = SharedData;
                    submodsViewModel.Load();
                    
                    SubmodsPage.DataContext = submodsViewModel;
                    CurrentPage = SubmodsPage;

                    RpcClient.SetPresence(new RichPresence()
                    {
                        Details = "Rise of Mordor Launcher",
                        State = "Browsing Submods",
                        Assets = new Assets()
                        {
                            LargeImageKey = "large_image",
                            LargeImageText = "discord.com/riseofmordor",
                        }
                    });
                    break;
            }

        }

        private async void Update()
        {
            if (SharedData.IsOffline)
                return;

            while (true)
            {
                await Task.Delay(10);
                SteamAPI.RunCallbacks();
            }
        }

        private ICommand _MinimizeCommand;
        private ICommand _CloseCommand;
        public ICommand MinimizeCommand
        {
            get
            {
                return _MinimizeCommand ?? (_MinimizeCommand = new CommandHandler(() => MinimizeEvent?.Invoke(this, new EventArgs()), () => true));
            }
        }

        public ICommand CloseCommand
        {
            get
            {
                return _CloseCommand ?? (_CloseCommand = new CommandHandler(() => CloseEvent?.Invoke(this, new EventArgs()), () => true));
            }
        }

    }
}
