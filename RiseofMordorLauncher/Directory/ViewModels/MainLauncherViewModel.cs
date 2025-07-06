using DiscordRPC;
using RiseofMordorLauncher.Directory.Pages;
using RiseofMordorLauncher.Directory.Services;
using SevenZipExtractor;
using Steamworks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RiseofMordorLauncher
{
    class Constants
    {
        public static AppId_t ATTILA_APP_ID = (AppId_t)325610;
    }

    class MainLauncherViewModel : BaseViewModel
    {
        private ISteamUserService _steamUserService;
        private IYouTubeDataService _youTubeDataService;
        private IModVersionService _modVersionService;
        private IUserPreferencesService UserPreferencesService;
        private ISteamSubmodsService SubmodService;
        //private ILauncherVersionService launcherVersionService;

        private Thread downloadThread;

        public event EventHandler<ApplicationPage> SwitchPageEvent;
        public string SteamUserName { get; set; }
        public string SteamAvatarUrl { get; set; }
        public string VersionText { get; set; }
        public string ChangelogText { get; set; }
        public ImageSource YouTubeThumbnailImage { get; set; }
        public SharedData SharedData { get; set; }
        public Visibility ShowVideo { get; set; } = Visibility.Visible;
        public Visibility ShowPreview { get; set; } = Visibility.Visible;
        public Visibility ShowProgressBar { get; set; } = Visibility.Hidden;
        public string PlayButtonText { get; set; } = "PLAY";
        public string PlayButtonMargin { get; set; } = "450 30";
        public string ProgressText { get; set; } = "DOWNLOADING...";
        public bool PlayButtonEnabled { get; set; } = true;
        public bool SubmodButtonEnabled { get; set; } = true;
        public int ProgressBarProgress { get; set; }
        public string BackgroundImage { get; set; }
        private ModVersion Version { get; set; }
        private LauncherVersion LauncherVersion { get; set; }

        public Visibility SettingsVisibility { get; set; } = Visibility.Hidden;
        public Settings SettingsPage { get; set; } = new Settings();
        private Window SettingsWindow { get; set; } = new Window();
        private SettingsViewModel SettingsPageViewModel { get; set; }
        public ILatestPreview LatestPreviewVM { get; private set; }

        private ICommand _PlayCommand;
        private ICommand _SettingsCommand;
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
        public ICommand SettingsCommand
        {
            get
            {
                return _SettingsCommand ?? (_SettingsCommand = new CommandHandler(() => SettingsButtonClick(), () => true));
            }
        }

        private string downloadArchiveFilename = null;

        public async void Load()
        {
            Logger.Log("Started loading Main Launcher");

            // get steam user data
            _steamUserService = new APISteamUserService();
            Logger.Log("Getting steam user data...");
            var user = await _steamUserService.GetSteamUser();
            SteamUserName = user.UserName;
            SteamAvatarUrl = user.AvatarUrl;

            Logger.Log("Applying user prefs on main launcher");
            UserPreferencesService = new APIUserPreferencesService();
            var prefs = UserPreferencesService.GetUserPreferences(SharedData);

            BackgroundImage = $"Directory/Images/{prefs.BackgroundImage}";
            ShowPreview = (bool)prefs.ShowLatestPreview ? Visibility.Visible : Visibility.Hidden;
            ShowVideo = (bool)prefs.ShowLatestVideo ? Visibility.Visible : Visibility.Hidden;

            Logger.Log("Getting YoutTube video data...");
            // get YoutTube video data (if offline hide player)
            if (!SharedData.IsOffline)
            {
                _youTubeDataService = new APIYouTubeDataService();
                var data = await _youTubeDataService.GetYouTubeData();
                _youtubeThumbnailUrl = data.VideoUrl;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(data.ThumbnailUrl);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                if (bitmap.CanFreeze)
                {
                    bitmap.Freeze();
                }

                YouTubeThumbnailImage = bitmap;
                OnPropertyChanged(nameof(YouTubeThumbnailImage));
            }
            else
            {
                ShowVideo = Visibility.Hidden;
            }

            Logger.Log("Getting version data...");
            // get version data
            _modVersionService = new APIModVersionService();
            Version = await _modVersionService.GetModVersionInfo(SharedData);
            VersionText = "Version " + Version.VersionText;
            ChangelogText = Version.ChangeLog;

            downloadThread = new Thread(PostUiLoadAsync);
            downloadThread.IsBackground = true;
            downloadThread.Start();

            //LatestPreviewVM = new LatestPreviewDiscordViewModel(SharedData);
            LatestPreviewVM = new LatestPreviewModDBViewModel(SharedData);

            SwitchPage(ApplicationPage.MainLauncher);
        }

        private async void PostUiLoadAsync()
        {
            if (SharedData.IsOffline && Version.InstalledVersionNumber == 0)
            {
                Logger.Log("PostUiLoadAsync: (SharedData.IsOffline && Version.InstalledVersionNumber == 0)");
                MessageBox.Show("Please connect to the internet and restart the Launcher to install Total War: Rise of Mordor");
                PlayButtonText = "UPDATING";
                PlayButtonEnabled = false;
            }
            else if (Version.LatestVersionNumber > Version.InstalledVersionNumber && !SharedData.IsOffline)
            {
                Logger.Log("Loading user preferences...");
                UserPreferencesService = new APIUserPreferencesService();
                var prefs = UserPreferencesService.GetUserPreferences(SharedData);

                var downloadUpdate = false;

                if (prefs.AutoUpdate)
                {
                    downloadUpdate = true;
                }
                else
                {
                    if (MessageBox.Show($"A new update is available for download, would you like to download it?", "Update Available", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        downloadUpdate = true;
                    }
                }

                if (downloadUpdate)
                {
                    DownloadUpdate();
                    Version = await _modVersionService.GetModVersionInfo(SharedData);
                }
            }
        }

        public async void DownloadUpdate()
        {
            Logger.Log("Updating mod files...");

            PlayButtonText = "UPDATING";
            PlayButtonEnabled = false;
            PlayButtonMargin = "350 30";
            SubmodButtonEnabled = false;
            ShowProgressBar = Visibility.Visible;

            //Logger.Log("Creating moddb service...");
            //IModdbDownloadService moddbService = new APIModdbDownloadService();
            //moddbService.DownloadUpdate += DownloadProgressUpdate;

            //Logger.Log($"Downloading {downloadArchiveFilename} from moddb...");
            //moddbService.DownloadFile(Version.ModdbDownloadPageUrl, $"{SharedData.AttilaDir}/data/{downloadArchiveFilename}");
            HttpClient httpClient = new HttpClient();
            Logger.Log("Adding download log to RoM server...");
            var request = await httpClient.GetAsync("http://80.208.231.54:7218/api/statistics/addLauncherDownload");
            Logger.Log($"Download log response: Code: {request.StatusCode}");

            Logger.Log("downloading latest version from RoM server...");

            downloadArchiveFilename = Path.GetFileName(Version.download_url);

            var client = new WebClient();
            client.DownloadProgressChanged += DownloadProgressUpdate;
            client.DownloadFileCompleted += new AsyncCompletedEventHandler(Buffer);
            client.DownloadFileAsync(new Uri(Version.download_url), $"{SharedData.AttilaDir}/data/{downloadArchiveFilename}"); 
        }

        private async void LaunchGame()
        {
            if (SteamApps.GetCurrentGameLanguage() != "english")
            {
                MessageBox.Show("Your game language has been detected as non-english. This may lead to issues. Rise of Mordor currently only supports English, we recommend switching the game language through Steam.");
            }

            UserPreferences prefs = new UserPreferences();
            SubmodService = new APISteamSubmodService();
            UserPreferencesService = new APIUserPreferencesService();
            Version = await _modVersionService.GetModVersionInfo(SharedData);

            prefs = UserPreferencesService.GetUserPreferences(SharedData);

            string Arguments = "";
            string used_mods = "";

            if (File.Exists($"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"))
            {
                var EnabledSubmodsRaw = File.ReadAllLines($"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt").ToList();
                var EnabledSubmods = new List<SubmodInstallation>();

                for (int i = 0; i < EnabledSubmodsRaw.Count; i++)
                {
                    SubmodInstallation installation = new SubmodInstallation();
                    installation = SubmodService.GetSubmodInstallInfo(ulong.Parse(EnabledSubmodsRaw.ElementAt(i)));

                    if (installation.IsInstalled)
                    {
                        EnabledSubmods.Add(installation);
                    }
                    else
                    {
                        DisableSubmod(EnabledSubmodsRaw.ElementAt(i));
                    }
                }

                if (prefs.LoadOrder.Count > 1)
                {
                    for (int i = 0; i < prefs.LoadOrder.Count; i++)
                    {
                        try
                        {
                            if (!EnabledSubmods.Any(s => s.ID == ulong.Parse(prefs.LoadOrder.ElementAt(i))))
                            {
                                prefs.LoadOrder.RemoveAt(i);
                            }
                        }
                        catch
                        {

                        }
                    }
                }

                for (int i = 0; i < EnabledSubmods.Count; i++)
                {
                    if (!(prefs.LoadOrder.Contains(EnabledSubmods.ElementAt(i).ID.ToString())))
                    {
                        prefs.LoadOrder.Add(EnabledSubmods.ElementAt(i).ID.ToString());
                    }
                }

                var SubmodLoadOrder = new List<string>();

                for (var i = 0; i < prefs.LoadOrder.Count; i++)
                {
                    ulong id = 0;
                    bool success = ulong.TryParse(prefs.LoadOrder.ElementAt(i), out id);

                    if (success)
                    {
                        var install = SubmodService.GetSubmodInstallInfo(id);
                        prefs.LoadOrder.RemoveAt(i);
                        prefs.LoadOrder.Insert(i, install.FileName);
                    }

                }

                foreach (var item in prefs.LoadOrder)
                {
                    SubmodLoadOrder.Add(item);
                }

                WritePrefs(prefs);

                foreach (var mod in prefs.LoadOrder)
                {
                    if (mod == "rom_base")
                    {
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

                            if (used_mods == "")
                            {
                                used_mods = $"mod \"{pack}\";";
                            }
                            else
                            {
                                used_mods = used_mods + Environment.NewLine + $"mod \"{pack}\";";
                            }

                        }
                    }
                    else
                    {
                        SubmodService = new APISteamSubmodService();
                        var submod = SubmodService.GetSubmodInstallInfo(ulong.Parse(mod));

                        if (submod.IsInstalled)
                        {
                            if (Arguments == "")
                            {
                                Arguments = @"add_working_directory """ + submod.InstallFolder + @""";";
                                Arguments = Arguments + @" mod """ + submod.FileName + @""";";
                            }
                            else
                            {
                                Arguments = Arguments + @" add_working_directory """ + submod.InstallFolder + @""";";
                                Arguments = Arguments + @" mod """ + submod.FileName + @""";";
                            }
                        }
                    }
                }
            }
            else
            {
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

                    if (used_mods == "")
                    {
                        used_mods = $"mod \"{pack}\";";
                    }
                    else
                    {
                        used_mods = used_mods + Environment.NewLine + $"mod \"{pack}\";";
                    }
                }
            }

            File.WriteAllText($@"{SharedData.AttilaDir}\used_mods.txt", used_mods);

            SteamAPI.RestartAppIfNecessary((AppId_t)325610);

            Process Attila = new Process();
            Attila.StartInfo.FileName = $@"{SharedData.AttilaDir}\Attila.exe";
            Attila.StartInfo.Arguments = Arguments;
            Attila.StartInfo.WorkingDirectory = SharedData.AttilaDir;
            Attila.Start();
            Attila.WaitForInputIdle();
            
        }

        private void SettingsButtonClick()
        {
            if (!SettingsPage.IsVisible)
            {
                SettingsPage = new Settings();
                SettingsPageViewModel = new SettingsViewModel(SharedData, this);
                SettingsPage.DataContext = SettingsPageViewModel;
                SettingsPage.Show();
                try
                {
                    SharedData.RPCClient.SetPresence(new RichPresence()
                    {
                        Details = "The Dawnless Days Launcher",
                        State = "Tweaking Settings",
                        Buttons = new DiscordRPC.Button[]
                        {
                            new DiscordRPC.Button() { Label = "Join Discord", Url = "https://discord.gg/tdd" },
                            new DiscordRPC.Button() { Label = "Download Mod", Url = "https://www.nexusmods.com/totalwarattila/mods/1" },
                        },
                        Assets = new Assets()
                        {
                            LargeImageKey = "large_image",
                            LargeImageText = "The Dawnless Days",
                        }
                    });
                } catch { }
            }
            else
            {
                SettingsPage.Hide();
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
                } catch { }
            }
        }

        private async void DownloadProgressUpdate(object sender, DownloadProgressChangedEventArgs e)
        {
            var percent_finished = e.ProgressPercentage;

            if (percent_finished > 95)
            {
                percent_finished = 100;
                ProgressText = "EXTRACTING DATA...";
            }

            ProgressBarProgress = percent_finished;
        }
        
        private void Buffer(object sender, AsyncCompletedEventArgs e)
        {
            Thread s = new Thread(DownloadCompleted);
            s.IsBackground = true;
            s.Start();
        }

        private async void DownloadCompleted()
        {
            Logger.Log($"Extracting {downloadArchiveFilename}...");

            using (var archiveFile = new ArchiveFile(downloadArchiveFilename))
            {
                var extractPath = $"{SharedData.AttilaDir}/data/";
                archiveFile.Extract(extractPath);
            }

            Logger.Log($"Deleting {downloadArchiveFilename}...");
            //File.Delete($"{SharedData.AttilaDir}/data/{downloadArchiveFilename}");
            try { File.Delete($"{SharedData.AppData}/RiseofMordor/RiseofMororLauncher/enabled_submods.txt"); } catch { }
            try { File.Delete($"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/local_version.txt"); } catch { }
            WebClient client = new WebClient();
            client.DownloadFile("http://80.208.231.54/launcher/local_version.txt", $"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/local_version.txt");

            using (var x = new StreamWriter($"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/user_preferences.txt"))
            {
                x.Write($"auto_update=true{Environment.NewLine}load_order = {{{Environment.NewLine}rom_base{Environment.NewLine}}}");
            }

            PlayButtonText = "PLAY";
            PlayButtonEnabled = true;
            PlayButtonMargin = "450 30";
            SubmodButtonEnabled = true;
            ShowProgressBar = Visibility.Hidden;

            Version = await _modVersionService.GetModVersionInfo(SharedData);
            VersionText = "Version " + Version.VersionText;
            ChangelogText = Version.ChangeLog;
        }

        private void DisableSubmod(string id)
        {
            string output = "";

            if (!File.Exists($"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"))
            {
                return;
            }

            string[] lines = File.ReadAllLines($"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt");
            if (lines.Count() == 0)
            {
                try { File.Delete($"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"); } catch { }
                return;
            }
            else
            {
                if (lines.Contains(id))
                {
                    if (lines.ToString() == id)
                    {
                        try { File.Delete($"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"); } catch { }
                        return;
                    }
                    else
                    {
                        foreach (var line in lines)
                        {
                            if (output == "")
                            {
                                if (line == id)
                                {

                                }
                                else
                                {
                                    output = line;
                                }
                            }
                            else
                            {
                                if (line == id)
                                {

                                }
                                else
                                {
                                    output = output + Environment.NewLine + line;
                                }
                            }
                        }
                    }
                }
                else
                {
                    output = string.Join(Environment.NewLine, lines);
                }
            }

            using (StreamWriter writer = new StreamWriter($"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"))
            {
                writer.Write(output);
            }
        }

        private void WritePrefs(UserPreferences prefs2)
        {
            if (!File.Exists($"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/user_preferences.txt"))
                File.CreateText($"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/user_preferences.txt");

            if (File.Exists($"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"))
            {

                var EnabledSubmodsRaw = File.ReadAllLines($"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt").ToList();
                List<SubmodInstallation> EnabledSubmods2 = new List<SubmodInstallation>();

                for (int i = 0; i < EnabledSubmodsRaw.Count; i++)
                {
                    SubmodInstallation installation = new SubmodInstallation();
                    installation = SubmodService.GetSubmodInstallInfo(ulong.Parse(EnabledSubmodsRaw.ElementAt(i)));

                    if (installation.IsInstalled)
                    {
                        EnabledSubmods2.Add(installation);
                    }
                    else
                    {
                        DisableSubmod(EnabledSubmodsRaw.ElementAt(i));
                    }
                }

                for (int i = 0; i < prefs2.LoadOrder.Count; i++)
                {
                    if (!EnabledSubmods2.Any(s => s.FileName == prefs2.LoadOrder.ElementAt(i)) && prefs2.LoadOrder.ElementAt(i) != "rom_base")
                    {
                        prefs2.LoadOrder.RemoveAt(i);
                    }
                    else
                    {
                        foreach (var submod in EnabledSubmods2)
                        {
                            if (submod.FileName == prefs2.LoadOrder.ElementAt(i))
                            {
                                prefs2.LoadOrder.RemoveAt(i);
                                prefs2.LoadOrder.Insert(i, submod.ID.ToString());
                            }
                        }
                    }
                }
            }

            string output = $"auto_update={prefs2.AutoUpdate}{Environment.NewLine}load_order = {{";

            foreach (string pack in prefs2.LoadOrder)
            {
                output = output + Environment.NewLine + pack;
            }

            output = output + Environment.NewLine + "}";

            using (var x = new StreamWriter($"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/user_preferences.txt"))
            {
                x.Write(output);
            }
        }
        
        protected virtual void SwitchPage(ApplicationPage page)
        {
            Logger.Log($"Switching page to {page}");
            SwitchPageEvent?.Invoke(this, page);
        }

        #region SocialButtons
        private ICommand _DiscordCommand;
        public ICommand DiscordCommand
        {
            get
            {
                return _DiscordCommand ?? (_DiscordCommand = new CommandHandler(() => Process.Start("https://discord.gg/KMhmdCb7Ut"), () => true)) ; ;
            }
        }

        private const string YOUTUBE_CHANNEL_URL = "https://www.youtube.com/channel/UCangGj6TUjUb9ri8CXcxQuw";

        private ICommand _YoutubeCommand;
        public ICommand YoutubeCommand
        {
            get
            {
                return _YoutubeCommand ?? (_YoutubeCommand = new CommandHandler(() => Process.Start(YOUTUBE_CHANNEL_URL), () => true)); ;
            }
        }

        private string _youtubeThumbnailUrl;

        private ICommand _youtubeThumbnailCommand;
        public ICommand YouTubeThumbnailCommand
        {
            get
            {
                if (_youtubeThumbnailUrl == null)
                {
                    _youtubeThumbnailUrl = YOUTUBE_CHANNEL_URL;
                }

                if (_youtubeThumbnailCommand == null)
                {
                    _youtubeThumbnailCommand = new CommandHandler(() => Process.Start(_youtubeThumbnailUrl), () => true);
                }

                return _youtubeThumbnailCommand;
            }
        }

        private ICommand _NexusCommand;

        public ICommand NexusCommand
        {
            get
            {
                return _NexusCommand ?? (_NexusCommand = new CommandHandler(() => Process.Start("https://www.nexusmods.com/totalwarattila/mods/1"), () => true)); ;
            }
        }

        private ICommand _RedditCommand;

        public ICommand RedditCommand
        {
            get
            {
                return _RedditCommand ?? (_RedditCommand = new CommandHandler(() => Process.Start("https://www.reddit.com/r/RiseofMordor/"), () => true)); ;
            }
        }

        private ICommand _InstagramCommand;

        public ICommand InstagramCommand
        {
            get
            {
                return _InstagramCommand ?? (_InstagramCommand = new CommandHandler(() => Process.Start("https://www.instagram.com/thedawnlessdays"), () => true)); ;
            }
        }
        #endregion

    }
}
