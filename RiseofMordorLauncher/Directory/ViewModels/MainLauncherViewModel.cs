using RiseofMordorLauncher.Directory.Services;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;


namespace RiseofMordorLauncher
{
    class MainLauncherViewModel : BaseViewModel
    {
        public ISteamUserService _steamUserService;
        public IYouTubeDataService _youTubeDataService;
        public IModVersionService _modVersionService;

        private Thread downloadThread;

        public event EventHandler<ApplicationPage> SwitchPageEvent;
        public string SteamUserName { get; set; } 
        public string SteamAvatarUrl { get; set; }      
        public string YouTubeVideoURL { get; set; }
        public SharedData SharedData { get; set; }
        public Visibility ShowVideo { get; set; } = Visibility.Visible;
        public Visibility ShowProgressBar { get; set; } = Visibility.Hidden;
        public string PlayButtonText { get; set; } = "PLAY";
        public string PlayButtonMargin{ get; set; } = "450 30";
        public string ProgressText { get; set; } = "DOWNLOADING...";
        public bool PlayButtonEnabled { get; set; } = true;
        public bool SubmodButtonEnabled { get; set; } = true;
        public int ProgressBarProgress { get; set; }
        private ModVersion Version { get; set; }
        
        private ICommand _PlayCommand;

        private ICommand _submodsPageCmd;
        public ICommand SubmodsPageCmd
        {
            get
            {
                return _submodsPageCmd ?? (_submodsPageCmd = new CommandHandler(() => SwitchPage(ApplicationPage.Submods), () => true));
            }
        }

        public ICommand PlayCommand
        {
            get
            {
                return _PlayCommand ?? (_PlayCommand = new CommandHandler(() => LaunchGame(), () => true));
            }
        }

        public async Task Load()
        {

            // get steam user data
            _steamUserService = new APISteamUserService();
            SteamUser user = await _steamUserService.GetSteamUser();
            SteamUserName = user.UserName;
            SteamAvatarUrl = user.AvatarUrl;

            // get YoutTube video data (if offline hide player)
            if (!SharedData.IsOffline)
            {
                _youTubeDataService = new APIYouTubeDataService();
                YouTubeData data = new YouTubeData();
                data = await _youTubeDataService.GetYouTubeData();
                YouTubeVideoURL = data.VideoUrl;
            }
            else
            {
                ShowVideo = Visibility.Hidden;
            }

            // get version data
            _modVersionService = new APIModVersionService();
            Version = await _modVersionService.GetModVersionInfo(SharedData);

            downloadThread = new Thread(PostUiLoadAsync);
            downloadThread.IsBackground = true;
            downloadThread.Start();
        }

        private async void PostUiLoadAsync()
        {
            if (SharedData.IsOffline && Version.InstalledVersionNumber == 0)
            {
                MessageBox.Show("Please connect to the internet and restart the Launcher to install Total War: Rise of Mordor");
            }
            else if (Version.LatestVersionNumber > Version.InstalledVersionNumber && !SharedData.IsOffline)
            {
                DownloadUpdate();
                Version = await _modVersionService.GetModVersionInfo(SharedData);
            }
        }

        private void DownloadUpdate()
        {

            PlayButtonText = "UPDATING";
            PlayButtonEnabled = false;
            PlayButtonMargin = "350 30";
            SubmodButtonEnabled = false;
            ShowProgressBar = Visibility.Visible;

            IGoogleDriveService googleDriveService = new APIGoogleDriveService();
            googleDriveService.DownloadUpdate += DownloadProgressUpdate;
            googleDriveService.DownloadFile("rom_pack_files.rar", $"{SharedData.AttilaDir}/data/rom_pack_files.rar", Version.DownloadNumberOfBytes);
            ProgressBarProgress = 100;

            ProgressText = "EXTRACTING...";
            using (var archive = RarArchive.Open($"{SharedData.AttilaDir}/data/rom_pack_files.rar"))
            {
                foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                {
                    entry.WriteToDirectory($"{SharedData.AttilaDir}/data/", new ExtractionOptions()
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }
            }

            File.Delete($"{SharedData.AttilaDir}/data/rom_pack_files.rar");
            File.Copy($"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/current_mod_version.txt", $"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/local_version.txt");

            PlayButtonText = "PLAY";
            PlayButtonEnabled = true;
            PlayButtonMargin = "450 30";
            SubmodButtonEnabled = true;
            ShowProgressBar = Visibility.Hidden;
        }

        private void LaunchGame()
        {
            string Arguments = "";

            foreach (string pack in Version.InstalledPackFiles)
            {
                if (Arguments == "")
                {
                    Arguments = $"mod {pack};";
                }
                else
                {
                    Arguments = Arguments + $" mod {pack};";
                }
            }

            SteamAPI.RestartAppIfNecessary((AppId_t)325610);

            Process Attila = new Process();
            Attila.StartInfo.FileName = $@"{SharedData.AttilaDir}\Attila.exe";
            Attila.StartInfo.Arguments = Arguments;
            Attila.StartInfo.WorkingDirectory = SharedData.AttilaDir;
            Attila.Start();
            Attila.WaitForInputIdle();
            
        }

        private void DownloadProgressUpdate(object sender, int percent_finished)
        {
            ProgressBarProgress = percent_finished;
        }
        protected virtual void SwitchPage(ApplicationPage page)
        {
            SwitchPageEvent.Invoke(this, page);
        }

    }
}
