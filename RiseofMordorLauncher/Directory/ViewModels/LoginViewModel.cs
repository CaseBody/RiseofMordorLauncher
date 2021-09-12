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
        public Visibility OfflineBtnVisibility      { get; private set; }
        public Visibility LoadingScreenVisibility   { get; private set; }

        public event EventHandler<ApplicationPage> SwitchPageEvent;

        public string ErrorClass        { get; private set; }
        public string ErrorDescription  { get; private set; }

        private bool _isSteamAPIInit;
        #endregion

        #region Functionality
        public LoginViewModel()
        {
            _isSteamAPIInit         = false;
            OfflineBtnVisibility    = Visibility.Hidden;
            LoadingScreenVisibility = Visibility.Visible;
        }

        // Start checks
        public void Load()
        {
            //await Task.Delay(1500); // give time to initiliaze, otherwise switching to main launcher page bugs out.

            if (_isSteamAPIInit == false)
            {
                _isSteamAPIInit = InitSteamAPI();
                if (_isSteamAPIInit == false)
                {
                    return;
                }
            }

            var isSteamRunning = CheckSteam();
            if (isSteamRunning == false)
                return;

            var isAttilaInstalled = CheckAttilaInstalled();
            if (isAttilaInstalled == false)
                return;

            var isOnline = CheckInternet();
            if (isOnline == false)
                return;
            else
                Continue(offline: false);
        }

        private void DisplayError(string errClass, string errDescr, bool showOfflineBtn, bool showLoadingScreen)
        {
            LoadingScreenVisibility = showLoadingScreen ? Visibility.Visible : Visibility.Hidden;
            OfflineBtnVisibility    = showOfflineBtn ? Visibility.Visible : Visibility.Hidden;
            ErrorClass              = errClass;
            ErrorDescription        = errDescr;

            Console.WriteLine(errDescr);
        }

        private bool InitSteamAPI()
        {
            bool didInit = SteamAPI.Init();
            if (didInit == false)
            {
                DisplayError("STEAM ERROR", "The Steam client was not detected. Please ensure you have Steam opened.", false, false);
            }

            return didInit;
        }
        
        /// <summary>
        /// First check, ensure steam is running. Mandatory
        /// </summary>
        /// <returns>True if Steam client is running; False if it isn't</returns>
        private bool CheckSteam()
        {
            bool isSteamRunning = SteamAPI.IsSteamRunning();
            if (isSteamRunning == false)
            {
                DisplayError("STEAM ERROR", "The Steam client was not detected. Please ensure you have Steam opened.", false, false);
            }

            return isSteamRunning;
        }

        // second check, ensure Attila is installed. Mandatory
        private bool CheckAttilaInstalled()
        {
            bool isAttilaInstalled = SteamApps.BIsAppInstalled((AppId_t)325610);
            if (isAttilaInstalled == false)
            {
                DisplayError("ATTILA WAS NOT DETECTED", "Total War: Attila Steam installation was not detected. Please ensure you have Total War: Attila installed.", false, false);
            }

            return isAttilaInstalled; 
        }

        // third check, check if there is an internet connection. If not, offer to start in Offline mode.
        private bool CheckInternet()
        {
            try
            {
                var myPing      = new Ping();
                var host        = "google.com";
                var buffer      = new byte[32];
                var timeout     = 1000;
                var pingOptions = new PingOptions();
                var reply       = myPing.Send(host, timeout, buffer, pingOptions);

                bool isOnline = reply.Status == IPStatus.Success;
                if (isOnline == false)
                {
                    DisplayError("NO INTERNET", "An internet connection could not be made. You will not be able to download updates until you reconnect.", true, false);
                }

                return isOnline;
            }
            catch (Exception ex)
            {
                DisplayError("NO INTERNET", "An internet connection could not be made. You will not be able to download updates until you reconnect.", true, false);
                return false;
            }

            return false;
        }

        private void Continue(bool offline)
        {
            var attila_appid = (AppId_t)325610;

            SteamApps.GetAppInstallDir(attila_appid, out string AttilaDir, 10000);

            SharedData.AttilaDir    = AttilaDir;
            SharedData.IsOffline    = offline;
            LoadingScreenVisibility = Visibility.Visible;

            SwitchPage(ApplicationPage.MainLauncher);
        }

        // Switch page and start the launcher after all checks pass
        protected virtual void SwitchPage(ApplicationPage page)
        {
            SwitchPageEvent.Invoke(this, page);
        }
        #endregion

        #region Commands
        // Button clicks

        private ICommand _retryCommand;
        public ICommand RetryCommand
        {
            get
            {
                return _retryCommand ?? (_retryCommand = new CommandHandler(() => Load(), () => true));
            }
        }

        private ICommand _internetOfflineCommand;
        public ICommand InternetOfflineCommand
        {
            get
            {
                return _internetOfflineCommand ?? (_internetOfflineCommand = new CommandHandler(() => Continue(offline: true), () => true));
            }
        }

        #endregion
    }
}
