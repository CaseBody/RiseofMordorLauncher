using DiscordRPC;
using Newtonsoft.Json.Linq;
using RiseofMordorLauncher.Directory.Pages;
using RiseofMordorLauncher.Directory.Services;
using SevenZipExtractor;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
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
        public string DownloadProgressText { get; set; }
        public bool PlayButtonEnabled { get; set; } = true;
        public bool SubmodButtonEnabled { get; set; } = true;
        public int ProgressBarProgress { get; set; }
        private string _downloadSourceOverride = null;
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

            _downloadSourceOverride = prefs.DownloadSource;
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

                if (data.ThumbnailUrl != null)
                {
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
            }
            else
            {
                ShowVideo = Visibility.Hidden;
            }

            Logger.Log("Getting version data...");

            InitAppDataFolders();

            // get version data
            _modVersionService = new APIModVersionService();
            Version = await _modVersionService.GetModVersionInfo(SharedData);
            VersionText = "Version " + Version.VersionText;
            ChangelogText = Version.ChangeLog;

            //LatestPreviewVM = new LatestPreviewDiscordViewModel(SharedData);
            LatestPreviewVM = new LatestPreviewModDBViewModel(SharedData);

            SwitchPage(ApplicationPage.MainLauncher);

            await PostUiLoadAsync();
        }

        private void InitAppDataFolders()
        {
            var modAppData = Path.Combine(SharedData.AppData, "RiseofMordor");

            if (!System.IO.Directory.Exists(modAppData))
            {
                System.IO.Directory.CreateDirectory(modAppData);
            }

            var launcherAppData = Path.Combine(modAppData, "RiseofMordorLauncher");

            if (!System.IO.Directory.Exists(launcherAppData))
            {
                System.IO.Directory.CreateDirectory(launcherAppData);
            }
        }

        private async Task PostUiLoadAsync()
        {
            if (SharedData.IsOffline)
            {
                Logger.Log("PostUiLoadAsync: No internet connection.");
                MessageBox.Show("Please connect to the internet and restart the Launcher to install Total War: The Dawnless Days");
                PlayButtonText = "OFFLINE";
                PlayButtonEnabled = false;
                return;
            }

            if (Version.InstalledVersionNumber == 0)
            {
                Logger.Log("PostUiLoadAsync: Version.InstalledVersionNumber == 0");
                PlayButtonText = "UPDATING";
                PlayButtonEnabled = false;
            }

            await TryStartDownload();
        }

        private async Task TryStartDownload()
        {
            var modDownloadLocation = Path.Combine(SharedData.AttilaDir, "data");
            var requiredPacks = Version.LatestPackFiles;

            if (isLatestModPacksInstalled(modDownloadLocation, requiredPacks))
            {
                Logger.Log("PostUiLoadAsync: Latest pack files are already installed. Skipping downloading phase...");
                return;
            }

            var regionCode = "Default";

            if (_downloadSourceOverride != null && _downloadSourceOverride != "Default")
            {
                regionCode = _downloadSourceOverride;
            }
            else
            {
                regionCode = await GetRegionByIPAsync();
            }

            var downloadUrl = Version.DownloadUrlOther;

            switch (regionCode)
            {
                case "Europe":
                    downloadUrl = Version.DownloadUrlEU;
                    break;

                case "North America":
                    downloadUrl = Version.DownloadUrlNA;
                    break;

                default: // "Africa", "Antarctica", "Asia", "Oceania", "South America"
                    downloadUrl = Version.DownloadUrlOther;
                    break;
            }

            var isDownloadUrlReachable = await IsEndpointReachableAsync(downloadUrl);

            if (downloadUrl == null || !isDownloadUrlReachable)
            {
                downloadUrl = Version.DownloadUrlOther;
            }

            var archiveFileName = Path.GetFileName(downloadUrl);
            var downloadArchiveFullName = Path.Combine(modDownloadLocation, archiveFileName);

            long remoteFileSize = GetRemoteFileSize(downloadUrl);
            long downloadedFileSize = 0;

            var doesDownloadFileExist = File.Exists(downloadArchiveFullName);
            if (doesDownloadFileExist)
            {
                downloadedFileSize = new FileInfo(downloadArchiveFullName).Length;
            }

            var isModFullyDownloaded = remoteFileSize == downloadedFileSize;
            if (isModFullyDownloaded)
            {
                Logger.Log("PostUiLoadAsync: Mod fully downloaded. Skipping download phase.");
                return;
            }

            if (HasEnoughSpace(modDownloadLocation, remoteFileSize, downloadedFileSize) == false)
            {
                Logger.Log("PostUiLoadAsync: Not enough space on drive detected. Aborting download.");
                MessageBox.Show($"You don't have enough free space on your {Path.GetPathRoot(modDownloadLocation)} disk drive. Clean some space and retry again.", "Insufficient space!");
                return;
            }

            var remoteVersion = Version.LatestVersionNumber;
            var installedVersion = Version.InstalledVersionNumber;
            var shouldDownloadUpdate = (remoteVersion > installedVersion) || !isModFullyDownloaded;

            if (shouldDownloadUpdate)
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
                    await DownloadUpdate(downloadUrl, downloadArchiveFullName);
                    Version = await _modVersionService.GetModVersionInfo(SharedData);

                    DownloadCompleted(downloadArchiveFullName, modDownloadLocation);
                }
            }
        }

        private static long GetRemoteFileSize(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "HEAD";

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                return response.ContentLength;
            }
        }

        public Task DownloadUpdate(string downloadUrl, string downloadDestFilePath)
        {
            var tcs = new TaskCompletionSource<bool>();

            long downloadedFileSize = 0;
            long remoteFileSize = GetRemoteFileSize(downloadUrl);

            if (File.Exists(downloadDestFilePath))
            {
                downloadedFileSize = new FileInfo(downloadDestFilePath).Length;
                var isModFullyDownloaded = downloadedFileSize == remoteFileSize;

                if (isModFullyDownloaded)
                {
                    tcs.SetResult(true);
                    return tcs.Task;
                }
            }

            Logger.Log("Updating mod files...");

            PlayButtonText = "UPDATING";
            PlayButtonEnabled = false;
            PlayButtonMargin = "350 30";
            SubmodButtonEnabled = false;
            ShowProgressBar = Visibility.Visible;

            if (!Debugger.IsAttached)
            {
                var httpClient = new HttpClient();
                Logger.Log("Adding download log to RoM server...");

                _ = httpClient.GetAsync("http://80.208.231.54:7218/api/statistics/addLauncherDownload");
                Logger.Log("Reporting a new download to the statistics server...");
                Logger.Log("Downloading latest version from RoM server...");
            }

            Task.Run(() =>
            {
                try
                {
                    var req = (HttpWebRequest)WebRequest.Create(downloadUrl);

                    if (downloadedFileSize > 0)
                    {
                        req.AddRange(downloadedFileSize);
                    }

                    using (var resp = req.GetResponse())
                    using (var stream = resp.GetResponseStream())
                    using (var fs = new FileStream(downloadDestFilePath, FileMode.Append, FileAccess.Write))
                    {
                        var buffer = new byte[8192];
                        var bytesRead = 0;
                        var totalRead = downloadedFileSize;

                        var stopwatch = Stopwatch.StartNew();
                        long bytesSinceLastUpdate = 0;
                        var lastUpdateTime = stopwatch.Elapsed;

                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            fs.Write(buffer, 0, bytesRead);
                            totalRead += bytesRead;
                            bytesSinceLastUpdate += bytesRead;

                            var currentTime = stopwatch.Elapsed;
                            var timeElapsed = (currentTime - lastUpdateTime).TotalSeconds;

                            if (timeElapsed >= 1.0)
                            {
                                var speedBytesPerSecond = bytesSinceLastUpdate / timeElapsed;
                                var speedString = FormatSpeed(speedBytesPerSecond);

                                int percent = (int)(totalRead * 100 / remoteFileSize);

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    DownloadProgressUpdate(percent, speedBytesPerSecond, totalRead, remoteFileSize);
                                });

                                bytesSinceLastUpdate = 0;
                                lastUpdateTime = currentTime;
                            }
                        }
                    }

                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }

        public async void RequestRedownload()
        {
            if (MessageBox.Show($"You are trying to re-download the mod. Do you wish to continue?", "Re-Download the mod", MessageBoxButton.YesNo) == MessageBoxResult.No)
            {
                return;
            }

            await TryStartDownload();

            // TODO: Reset installed version
        }

        private string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond >= 1024 * 1024)
            {
                return $"{bytesPerSecond / (1024 * 1024):0.00} MB/s";
            }
            else if (bytesPerSecond >= 1024)
            {
                return $"{bytesPerSecond / 1024:0.00} KB/s";
            }
            else
            {
                return $"{bytesPerSecond:0} B/s";
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        private async Task<string> GetRegionByIPAsync()
        {
            using (var client = new HttpClient())
            {
                const string API_KEY = "11e33285579b0472f4e8b8ed03b6a367";
                var url = $"http://api.ipapi.com/api/check?access_key={API_KEY}&fields=continent_name,ip";
                var json = await client.GetStringAsync(url);

                var obj = JObject.Parse(json);

                var continent = obj["continent_name"]?.ToString();

                return continent ?? "Unknown";
            }
        }

        private bool HasEnoughSpace(string modDownloadLocation, long remoteFileSize, long downloadedFileSize)
        {
            var requiredSpace = remoteFileSize - downloadedFileSize;
            var drive = new DriveInfo(Path.GetPathRoot(modDownloadLocation));
            return drive.AvailableFreeSpace >= requiredSpace;
        }

        private Task<bool> IsEndpointReachableAsync(string url)
        {
            return Task.Run(async () =>
            {
                try
                {
                    using (var httpClient = new HttpClient())
                    {
                        httpClient.Timeout = TimeSpan.FromSeconds(5);
                        var response = await httpClient.GetAsync(url);
                        return response.IsSuccessStatusCode;
                    }
                }
                catch
                {
                    return false;
                }
            });
        }

        private bool isLatestModPacksInstalled(string installLocation, List<string> requiredPacksList)
        {
            if (!System.IO.Directory.Exists(installLocation))
            {
                return false;
            }

            var existingFiles = new HashSet<string>(System.IO.Directory.GetFiles(installLocation).Select(Path.GetFileName), StringComparer.Ordinal);

            foreach (var requiredFile in requiredPacksList)
            {
                if (!existingFiles.Contains(requiredFile))
                {
                    return false;
                }
            }

            // TODO: Verify pack files hash & file size

            return true;
        }

        private async void LaunchGame()
        {
            if (SteamApps.GetCurrentGameLanguage() != "english")
            {
                MessageBox.Show("Your game language has been detected as non-english. This may lead to issues. The Dawnless Days currently only supports English, we recommend switching the game language through Steam.");
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

        private void DownloadProgressUpdate(int progress, double downloadSpeed, long bytesDownloaded, long bytesTotal)
        {
            var percent_finished = progress;
            ProgressText = $"DOWNLOADING {progress}%";

            var formatSizeDownloaded = FormatBytes(bytesDownloaded);
            var formatSizeTotal = FormatBytes(bytesTotal);
            var formatDownloadSpeed = FormatSpeed(downloadSpeed);

            DownloadProgressText = $"{formatSizeDownloaded} / {formatSizeTotal} ({formatDownloadSpeed})";

            if (percent_finished > 95)
            {
                percent_finished = 100;
                ProgressText = "EXTRACTING DATA...";
            }

            ProgressBarProgress = percent_finished;
        }

        private async void DownloadCompleted(string downloadArchiveFullName, string extractPath)
        {
            Logger.Log($"Extracting {downloadArchiveFullName}...");

            try
            {
                using (var archiveFile = new ArchiveFile(downloadArchiveFullName))
                {
                    archiveFile.Extract(extractPath);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Extraction failed: {ex.Message}");
                MessageBox.Show($"An exception occured while trying to extract mod files. Please forward the below message to the devs:\n{ex.Message}", "Failed to extract");
            }

            Logger.Log($"Deleting {downloadArchiveFullName}...");
            File.Delete(downloadArchiveFullName);

            try { File.Delete($"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"); } catch { }
            try { File.Delete($"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/local_version.txt"); } catch { }

            var client = new WebClient();
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

        public void SetDownloadSourceOverride(string preferredSource)
        {
            _downloadSourceOverride = preferredSource;
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
