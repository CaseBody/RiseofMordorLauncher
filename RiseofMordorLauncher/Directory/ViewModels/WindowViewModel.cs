using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using Steamworks;
using RiseofMordorLauncher.Directory.Pages;
using System.Threading;
using RiseofMordorLauncher.Directory.Services;
using DiscordRPC;
using System.Windows;

namespace RiseofMordorLauncher
{
    class WindowViewModel : BaseViewModel
    {
        private DiscordRpcClient RpcClient;

        public event EventHandler MinimizeEvent;
        public event EventHandler CloseEvent;

        private readonly SharedData SharedData;

        private MainLauncherViewModel mainLauncherViewModel;
        private LoginViewModel loginViewModel = new LoginViewModel();
        private SubmodsViewModel submodsViewModel = new SubmodsViewModel();

        private MainLauncher MainLauncherPage;
        private readonly Login LoginPage;
        private SubmodsPage SubmodsPage;
        private Thread UpdateThread;
        public Page CurrentPage { get; set; }

        // Load login page
        public WindowViewModel()
        {
            try
            {
                RpcClient = new DiscordRpcClient("748940888494833754");
                RpcClient.Initialize();
            }
            catch
            {}

            #region Create Shared Data
            SharedData = new SharedData
            {
                AppData     = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                RPCClient   = RpcClient
            };
            #endregion

            #region Setup AppData
            var launcherAppDataPath = $@"{SharedData.AppData}\RiseofMordor\RiseofMordorLauncher\";
            if (System.IO.Directory.Exists(launcherAppDataPath) == false)
            {
                System.IO.Directory.CreateDirectory(launcherAppDataPath);
            }
            #endregion

            #region Login Page Setup
            try
            {
                loginViewModel = new LoginViewModel();
                loginViewModel.SharedData = SharedData;
                loginViewModel.SwitchPageEvent += SwitchPage;
                loginViewModel.Load();

                LoginPage = new Login
                {
                    DataContext = loginViewModel
                };
            }
            catch (Exception e)
            {
                MessageBox.Show("INIT LOGIN VIEW ERROR" + e.Message);
            }
            #endregion

            #region Setup Update Thread
            UpdateThread = new Thread(Update);
            UpdateThread.IsBackground = true;
            UpdateThread.Start();
            #endregion

            // Initial page is Login Page
            CurrentPage = LoginPage;
        }

        // switch page, can be fired by the page ViewModels
        private void SwitchPage(object sender, ApplicationPage page)
        {
            if (sender is LoginViewModel)
            {
                #region Main Page Setup
                try
                {

                    mainLauncherViewModel = new MainLauncherViewModel();
                    mainLauncherViewModel.SharedData = SharedData;
                    mainLauncherViewModel.SwitchPageEvent += SwitchPage;
                    mainLauncherViewModel.Load();

                    MainLauncherPage = new MainLauncher(SharedData)
                    {
                        DataContext = mainLauncherViewModel
                    };
                }
                catch (Exception e)
                {
                    MessageBox.Show("INIT MAIN VIEW ERROR" + e.Message);
                }
                #endregion

                #region Submods Page Setup
                submodsViewModel = new SubmodsViewModel();
                submodsViewModel.SharedData = SharedData;
                submodsViewModel.SwitchPageEvent += SwitchPage;
                submodsViewModel.Load();

                SubmodsPage = new SubmodsPage
                {
                    DataContext = submodsViewModel
                };
                #endregion
            }

            string discordStateText;
            switch (page)
            {
                case ApplicationPage.MainLauncher:
                    discordStateText = "On Main Page";
                    CurrentPage = MainLauncherPage;
                    break;

                case ApplicationPage.Login:
                    discordStateText = "Initializing launcher...";
                    CurrentPage = LoginPage;
                    break;

                case ApplicationPage.Submods:
                    discordStateText = "Browsing Submods";
                    CurrentPage = SubmodsPage;
                    break;

                default:
                    discordStateText = "";
                    break;
            }
            
            try
            {
                SharedData.RPCClient.SetPresence(new RichPresence()
                {
                    Details = "The Dawnless Days Launcher",
                    Buttons = new DiscordRPC.Button[]
                        {
                            new DiscordRPC.Button() { Label = "Join Discord", Url = "https://discord.gg/RzYRVdQezF" },
                            new DiscordRPC.Button() { Label = "Download Mod", Url = "https://www.nexusmods.com/totalwarattila/mods/1" },
                        },
                    Assets = new Assets()
                    {
                        LargeImageKey = "large_image",
                        LargeImageText = "The Dawnless Days",
                    }
                });
            }
            catch
            {}
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
