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
using RiseofMordorLauncher.Directory.Pages;
using DiscordRPC;

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
        public string YouTubeVideoURL { get; set; }
        public SharedData SharedData { get; set; }
        public Visibility ShowVideo { get; set; } = Visibility.Visible;
        public Visibility ShowProgressBar { get; set; } = Visibility.Hidden;
        public string PlayButtonText { get; set; } = "PLAY";
        public string PlayButtonMargin { get; set; } = "450 30";
        public string ProgressText { get; set; } = "DOWNLOADING...";
        public bool PlayButtonEnabled { get; set; } = true;
        public bool SubmodButtonEnabled { get; set; } = true;
        public int ProgressBarProgress { get; set; }
        private ModVersion Version { get; set; }
        private LauncherVersion LauncherVersion { get; set; }

        public Visibility SettingsVisibility { get; set; } = Visibility.Hidden;
        public Settings SettingsPage { get; set; } = new Settings();
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

        public async void Load()
        {
            Logger.Log("Started loading Main Launcher");

            // get steam user data
            _steamUserService = new APISteamUserService();
            Logger.Log("Getting steam user data...");
            var user = await _steamUserService.GetSteamUser();
            SteamUserName = user.UserName;
            SteamAvatarUrl = user.AvatarUrl;

            Logger.Log("Getting YoutTube video data...");
            // get YoutTube video data (if offline hide player)
            if (!SharedData.IsOffline)
            {
                _youTubeDataService = new APIYouTubeDataService();
                var data = await _youTubeDataService.GetYouTubeData();
                YouTubeVideoURL = data.VideoUrl;
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

                if (prefs.AutoUpdate)
                {
                    DownloadUpdate();
                    Version = await _modVersionService.GetModVersionInfo(SharedData);
                }
                else
                {
                    if (MessageBox.Show($"A new update is available for download, would you like to download it?", "Update Available", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        DownloadUpdate();
                        Version = await _modVersionService.GetModVersionInfo(SharedData);
                    }
                }
            }
        }

        private async void DownloadUpdate()
        {
            Logger.Log("Updating mod files...");

            PlayButtonText = "UPDATING";
            PlayButtonEnabled = false;
            PlayButtonMargin = "350 30";
            SubmodButtonEnabled = false;
            ShowProgressBar = Visibility.Visible;

            Logger.Log("Creating moddb service...");
            IModdbDownloadService moddbService = new APIModdbDownloadService();
            moddbService.DownloadUpdate += DownloadProgressUpdate;

            Logger.Log("Downloading rom_pack_files.rar from moddb...");
            moddbService.DownloadFile(Version.ModdbDownloadPageUrl, $"{SharedData.AttilaDir}/data/rom_pack_files.rar");
            
        }

        private async void DownloadLauncherUpdate()
        {
            PlayButtonText = "UPDATING LAUNCHER";
            PlayButtonEnabled = false;
            PlayButtonMargin = "350 30";
            SubmodButtonEnabled = false;
            ShowProgressBar = Visibility.Visible;

            IGoogleDriveService googleDriveService = new APIGoogleDriveService();
            googleDriveService.DownloadUpdate += DownloadProgressUpdate;
            await googleDriveService.DownloadFile("launcher.rar", $"{System.IO.Directory.GetCurrentDirectory()}/launcher.rar", LauncherVersion.DownloadNumberOfBytes);
            ProgressBarProgress = 100;

            System.IO.Directory.CreateDirectory($"{System.IO.Directory.GetCurrentDirectory()}/temp/");
            ProgressText = "APPLYING LAUNCHER UPDATE";
            using (var archive = RarArchive.Open($"{System.IO.Directory.GetCurrentDirectory()}/launcher.rar"))
            {
                foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                {
                    entry.WriteToDirectory(System.IO.Directory.GetCurrentDirectory() + "/temp/", new ExtractionOptions()
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }
            }

            File.Delete($"{System.IO.Directory.GetCurrentDirectory()}/launcher.rar");
            try { File.Delete($"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/local_launcher_version.txt"); } catch { }
            File.Copy($"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/current_launcher_version.txt", $"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/local_launcher_version.txt");

            Process Launcher = new Process();
            Launcher.StartInfo.FileName = $"{System.IO.Directory.GetCurrentDirectory()}/temp/RiseofMordorLauncher.exe";
            Launcher.StartInfo.Arguments = "update_1";
            Launcher.Start();

            Process.GetCurrentProcess().Kill();
        }

        private async void LaunchGame()
        {
            UserPreferences prefs = new UserPreferences();
            SubmodService = new APISteamSubmodService();
            UserPreferencesService = new APIUserPreferencesService();
            Version = await _modVersionService.GetModVersionInfo(SharedData);

            prefs = UserPreferencesService.GetUserPreferences(SharedData);

            string Arguments = "";

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

        private void SettingsButtonClick()
        {
            if (SettingsVisibility == Visibility.Hidden)
            {
                SettingsPageViewModel = new SettingsViewModel(SharedData);
                SettingsPage.DataContext = SettingsPageViewModel;
                SettingsVisibility = Visibility.Visible;

                try
                {
                    SharedData.RPCClient.SetPresence(new RichPresence()
                    {
                        Details = "Rise of Mordor Launcher",
                        State = "Tweaking Settings",
                        Buttons = new DiscordRPC.Button[]
                        {
                            new DiscordRPC.Button() { Label = "Join Discord", Url = "https://www.discord.gg/riseofmordor" },
                            new DiscordRPC.Button() { Label = "Download Mod", Url = "https://www.moddb.com/mods/total-war-rise-of-mordor" },
                        },
                        Assets = new Assets()
                        {
                            LargeImageKey = "large_image",
                            LargeImageText = "discord.gg/riseofmordor",
                        }
                    });
                } catch { }
            }
            else
            {
                SettingsVisibility = Visibility.Hidden;

                try
                {
                    SharedData.RPCClient.SetPresence(new RichPresence()
                    {
                        Details = "Rise of Mordor Launcher",
                        State = "On the main page",
                        Buttons = new DiscordRPC.Button[]
                        {
                            new DiscordRPC.Button() { Label = "Join Discord", Url = "https://www.discord.gg/riseofmordor" },
                            new DiscordRPC.Button() { Label = "Download Mod", Url = "https://www.moddb.com/mods/total-war-rise-of-mordor" },
                        },
                        Assets = new Assets()
                        {
                            LargeImageKey = "large_image",
                            LargeImageText = "discord.gg/riseofmordor",
                        }
                    });
                } catch { }
            }
        }

        private async void DownloadProgressUpdate(object sender, int percent_finished)
        {
            ProgressBarProgress = percent_finished;

            if (percent_finished == 105)
            {
                Logger.Log("Extracting rom_pack_files.rar...");
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

                Logger.Log("Deleting rom_pack_files.rar...");
                File.Delete($"{SharedData.AttilaDir}/data/rom_pack_files.rar");
                try { File.Delete($"{SharedData.AppData}/RiseofMordor/RiseofMororLauncher/enabled_submods.txt"); } catch { }
                try { File.Delete($"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/local_version.txt"); } catch { }
                File.Copy($"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/current_mod_version.txt", $"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/local_version.txt");

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

        private ICommand _YoutubeCommand;
        public ICommand YoutubeCommand
        {
            get
            {
                return _YoutubeCommand ?? (_YoutubeCommand = new CommandHandler(() => Process.Start("https://www.youtube.com/channel/UCangGj6TUjUb9ri8CXcxQuw"), () => true)); ;
            }
        }

        private ICommand _ModdbCommand;

        public ICommand ModdbCommand
        {
            get
            {
                return _ModdbCommand ?? (_ModdbCommand = new CommandHandler(() => Process.Start("https://www.moddb.com/mods/total-war-rise-of-mordor"), () => true)); ;
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
                return _InstagramCommand ?? (_InstagramCommand = new CommandHandler(() => Process.Start("https://www.instagram.com/riseofmordor_tw/"), () => true)); ;
            }
        }
        #endregion

    }
}
