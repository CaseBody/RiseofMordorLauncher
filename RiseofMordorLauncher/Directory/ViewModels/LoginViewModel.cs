using System;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Steamworks;
using System.Windows.Input;
using RiseofMordorLauncher.Directory.Services;
using System.Net.NetworkInformation;

namespace RiseofMordorLauncher
{
    class LoginViewModel : BaseViewModel
    {
        #region Variables
        public SharedData SharedData { get; set; }

        public Visibility internet_error_shown { get; set; } = Visibility.Hidden;
        public Visibility attila_error_shown { get; set; } = Visibility.Hidden;
        public Visibility steam_error_shown { get; set; } = Visibility.Hidden;

        public event EventHandler<ApplicationPage> SwitchPageEvent;

        #endregion

        #region Functionality
        // Start checks
        public async void Load()
        {
            await Task.Delay(1500); // give time to initiliaze, otherwise switching to main launcher page bugs out.
            CheckSteam();
        }

        // first check, ensure steam is running. Mandatory
        public void CheckSteam()
        {
            SteamAPI.Init();
            
            if (SteamAPI.IsSteamRunning())
            {
                steam_error_shown = Visibility.Hidden;
                CheckAttilaInstalled();
            }
            else
            {
                steam_error_shown = Visibility.Visible;
            }
        }

        // second check, ensure Attila is installed. Mandatory
        public void CheckAttilaInstalled()
        {
            SteamAPI.Init();
            AppId_t attila_appid = (AppId_t)325610;

            if (SteamApps.BIsAppInstalled(attila_appid))
            {
                attila_error_shown = Visibility.Hidden;
                CheckInternet();
            }
            else
            {
                attila_error_shown = Visibility.Visible;
            }

        }

        // third check, check if there is an internet connection. If not, offer to start in Offline mode.
        public void CheckInternet()
        {
            try
            {
                Ping myPing = new Ping();
                String host = "google.com";
                byte[] buffer = new byte[32];
                int timeout = 1000;
                PingOptions pingOptions = new PingOptions();
                PingReply reply = myPing.Send(host, timeout, buffer, pingOptions);
                
                if (reply.Status == IPStatus.Success)
                {
                    SharedData.isOffline = false;
                    internet_error_shown = Visibility.Hidden;
                    SwitchPage(ApplicationPage.MainLauncher);
                }
                else
                {
                    internet_error_shown = Visibility.Visible;
                }
            }
            catch (Exception)
            {
                internet_error_shown = Visibility.Visible;
            }
        }

        // Switch page and start the launcher after all checks pass
        protected virtual void SwitchPage(ApplicationPage page)
        {
            SwitchPageEvent.Invoke(this, page);
        }
        #endregion

        #region Commands
        // Button clicks

        private ICommand _SteamRetryCommand;
        public ICommand SteamRetryCommand
        {
            get
            {
                return _SteamRetryCommand ?? (_SteamRetryCommand = new CommandHandler(() => CheckSteam(), () => true));
            }
        }

        private ICommand _AttilaRetryCommand;
        public ICommand AttilaRetryCommand
        {
            get
            {
                return _AttilaRetryCommand ?? (_AttilaRetryCommand = new CommandHandler(() => CheckAttilaInstalled(), () => true));
            }
        }

        private ICommand _InternetRetryCommand;
        public ICommand InternetRetryCommand
        {
            get
            {
                return _InternetRetryCommand ?? (_InternetRetryCommand = new CommandHandler(() => CheckInternet(), () => true));
            }
        }

        private ICommand _InternetOfflineCommand;
        public ICommand InternetOfflineCommand
        {
            get
            {
                return _InternetOfflineCommand ?? (_InternetOfflineCommand = new CommandHandler(() => { SharedData.isOffline = true; SwitchPage(ApplicationPage.MainLauncher); }, () => true));
            }
        }

        #endregion

    }
}
