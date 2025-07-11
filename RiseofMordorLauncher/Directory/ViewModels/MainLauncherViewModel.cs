﻿using DiscordRPC;
using RiseofMordorLauncher.Directory.Pages;
using RiseofMordorLauncher.Directory.Services;
using SevenZip;
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

        private string modAppData = null;
        public string launcherAppData = null;

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
            modAppData = Path.Combine(SharedData.AppData, "RiseofMordor");

            if (!System.IO.Directory.Exists(modAppData))
            {
                System.IO.Directory.CreateDirectory(modAppData);
            }

            launcherAppData = Path.Combine(modAppData, "RiseofMordorLauncher");

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

            var isReadyToPlay = await TryStartDownload();
            if (isReadyToPlay)
            {
                await MarkReadyToPlay();
            }
            else
            {
                MarkCannotPlay();
            }
        }

        private async Task<bool> TryStartDownload()
        {
            Logger.Log("PostUiLoadAsync: TryStartDownload");

            var modDownloadLocation = Path.Combine(SharedData.AttilaDir, "data");
            var requiredPacks = Version.LatestPackFiles;
            var isLatestPacksInstalled = isLatestModPacksInstalled(modDownloadLocation, requiredPacks);

            if (Version.InstalledVersionNumber == Version.LatestVersionNumber && isLatestPacksInstalled)
            {
                Logger.Log("PostUiLoadAsync: Latest pack files are already installed. Skipping downloading phase...");
                return true;
            }

            var regionCode = "Default";

            if (_downloadSourceOverride != null && _downloadSourceOverride != "Default")
            {
                regionCode = _downloadSourceOverride;
            }
            else
            {
                Logger.Log("PostUiLoadAsync: Getting region from IP...");
                regionCode = await GetRegionByIPAsync();
            }

            Logger.Log($"PostUiLoadAsync: Region: {regionCode}");
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

            Logger.Log("PostUiLoadAsync: Checking if the endpoint is reachable...");
            var isDownloadUrlReachable = await IsEndpointReachableAsync(downloadUrl);
            Logger.Log($"PostUiLoadAsync: isDownloadUrlReachable: {isDownloadUrlReachable}");

            if (downloadUrl == null || !isDownloadUrlReachable)
            {
                downloadUrl = Version.DownloadUrlOther;
            }

            Logger.Log("PostUiLoadAsync: Re-checking if the endpoint is reachable...");
            isDownloadUrlReachable = await IsEndpointReachableAsync(downloadUrl);
            Logger.Log($"PostUiLoadAsync: isDownloadUrlReachable: {isDownloadUrlReachable}");

            if (isDownloadUrlReachable == false)
            {
                Logger.Log("PostUiLoadAsync: Download link is invalid.");
                MessageBox.Show($"The launcher is trying to download the mod from an invalid link. Please forward this error to the developers.", "Download link is invalid");
                return false;
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

            Logger.Log($"PostUiLoadAsync: archiveFileName - {archiveFileName}");
            Logger.Log($"PostUiLoadAsync: downloadArchiveFullName - {downloadArchiveFullName}");
            Logger.Log($"PostUiLoadAsync: remoteFileSize - {remoteFileSize}");
            Logger.Log($"PostUiLoadAsync: downloadedFileSize - {downloadedFileSize}");
            Logger.Log($"PostUiLoadAsync: doesDownloadFileExist - {doesDownloadFileExist}");

            var isModFullyDownloaded = remoteFileSize == downloadedFileSize;
            if (isModFullyDownloaded)
            {
                Logger.Log("PostUiLoadAsync: Mod fully downloaded. Skipping download phase.");
                await ExtractArchive(downloadArchiveFullName, modDownloadLocation);

                Logger.Log($"PostUiLoadAsync: Update local version");
                UpdateLocalVersion();

                return true;
            }

            if (isLatestPacksInstalled)
            {
                Logger.Log($"PostUiLoadAsync: Get mod version info (isLatestPacksInstalled)...");
                Version = await _modVersionService.GetModVersionInfo(SharedData);

                UpdateLocalVersion();

                Logger.Log("PostUiLoadAsync: Mod fully installed. Skipping download & install phases.");
                return true;
            }

            Logger.Log($"PostUiLoadAsync: isModFullyDownloaded - {isModFullyDownloaded}");

            if (HasEnoughSpace(modDownloadLocation, remoteFileSize, downloadedFileSize) == false)
            {
                Logger.Log("PostUiLoadAsync: Not enough space on drive detected. Aborting download.");
                MessageBox.Show($"You don't have enough free space on your {Path.GetPathRoot(modDownloadLocation)} disk drive. Clean some space and retry again.", "Insufficient space!");
                return false;
            }

            Logger.Log($"PostUiLoadAsync: HasEnoughSpace");

            var remoteVersion = Version.LatestVersionNumber;
            var installedVersion = Version.InstalledVersionNumber;
            var shouldDownloadUpdate = (remoteVersion > installedVersion) || !isModFullyDownloaded;

            Logger.Log($"PostUiLoadAsync: remoteVersion - {remoteVersion}");
            Logger.Log($"PostUiLoadAsync: installedVersion - {installedVersion}");
            Logger.Log($"PostUiLoadAsync: shouldDownloadUpdate - {shouldDownloadUpdate}");

            if (shouldDownloadUpdate)
            {
                Logger.Log("PostUiLoadAsync: Loading user preferences...");
                UserPreferencesService = new APIUserPreferencesService();
                var prefs = UserPreferencesService.GetUserPreferences(SharedData);

                var downloadUpdate = false;

                if (prefs.AutoUpdate)
                {
                    Logger.Log($"PostUiLoadAsync: AutoUpdate on");
                    downloadUpdate = true;
                }
                else
                {
                    Logger.Log($"PostUiLoadAsync: AutoUpdate off");

                    if (MessageBox.Show($"A new update is available for download, would you like to download it?", "Update Available", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        downloadUpdate = true;
                    }
                }

                Logger.Log($"PostUiLoadAsync: downloadUpdate - {downloadUpdate}");

                if (downloadUpdate)
                {
                    Logger.Log($"PostUiLoadAsync: Downloading...");
                    await DownloadUpdate(downloadUrl, downloadArchiveFullName);

                    Logger.Log($"PostUiLoadAsync: Get mod version info...");
                    Version = await _modVersionService.GetModVersionInfo(SharedData);

                    Logger.Log($"PostUiLoadAsync: Download completed");
                    await ExtractArchive(downloadArchiveFullName, modDownloadLocation);

                    Logger.Log($"PostUiLoadAsync: Update local version");
                    UpdateLocalVersion();
                }
            }

            return true;
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
            Logger.Log($"DownloadUpdate: Downloading start");

            var tcs = new TaskCompletionSource<bool>();

            long downloadedFileSize = 0;
            long remoteFileSize = GetRemoteFileSize(downloadUrl);

            if (File.Exists(downloadDestFilePath))
            {
                Logger.Log($"DownloadUpdate: File {downloadDestFilePath} exists");

                downloadedFileSize = new FileInfo(downloadDestFilePath).Length;
                var isModFullyDownloaded = downloadedFileSize == remoteFileSize;

                Logger.Log($"DownloadUpdate: isModFullyDownloaded - {isModFullyDownloaded}");

                if (isModFullyDownloaded)
                {
                    tcs.SetResult(true);
                    return tcs.Task;
                }
                else if (downloadedFileSize > remoteFileSize)
                {
                    Logger.Log($"DownloadUpdate: Deleting {downloadDestFilePath}");
                    File.Delete(downloadDestFilePath);
                    Logger.Log($"DownloadUpdate: Deleted {downloadDestFilePath}");
                    downloadedFileSize = 0;
                }
            }
            else
            {
                Logger.Log($"DownloadUpdate: File {downloadDestFilePath} does not exist");
            }

            Logger.Log("DownloadUpdate: Updating mod files...");

            PlayButtonText = "UPDATING";
            PlayButtonEnabled = false;
            PlayButtonMargin = "350 30";
            SubmodButtonEnabled = false;
            ShowProgressBar = Visibility.Visible;

            if (!Debugger.IsAttached)
            {
                var httpClient = new HttpClient();
                Logger.Log("DownloadUpdate. Adding download log to RoM server...");

                _ = httpClient.GetAsync("http://80.208.231.54:7218/api/statistics/addLauncherDownload");
                Logger.Log("DownloadUpdate. Reporting a new download to the statistics server...");
                Logger.Log("DownloadUpdate. Downloading latest version from RoM server...");
            }

            Task.Run(() =>
            {
                try
                {
                    var req = (HttpWebRequest)WebRequest.Create(downloadUrl);

                    Logger.Log("DownloadUpdateTask. Create WebRequest");

                    if (downloadedFileSize > 0)
                    {
                        req.AddRange(downloadedFileSize);
                        Logger.Log($"DownloadUpdateTask. Add range: {downloadedFileSize}");
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
                    Logger.Log($"DownloadUpdate. Exception: {ex.Message}");
                    MessageBox.Show(ex.Message);
                    tcs.SetException(ex);
                }
            });

            Logger.Log($"DownloadUpdate. tcs task return");

            return tcs.Task;
        }

        public async void RequestRedownload()
        {
            if (MessageBox.Show($"You are trying to re-download the mod. Do you wish to continue?", "Re-Download the mod", MessageBoxButton.YesNo) == MessageBoxResult.No)
            {
                return;
            }

            await TryStartDownload();
            await MarkReadyToPlay();
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
            var client = new HttpClient();

            var publicIP = await client.GetStringAsync("https://api.ipify.org");
            var regionCode = await client.GetStringAsync($"http://3ba9.l.time4vps.cloud:7218/api/LauncherVersion/region?ip_address={publicIP}");

            return regionCode;
        }

        private bool HasEnoughSpace(string modDownloadLocation, long remoteFileSize, long downloadedFileSize)
        {
            var requiredSpace = remoteFileSize - downloadedFileSize;
            var drive = new DriveInfo(Path.GetPathRoot(modDownloadLocation));

            Logger.Log($"Drive Info. Drive: {drive.Name}");
            Logger.Log($"Drive Info. Available free space: {drive.AvailableFreeSpace}");

            return drive.AvailableFreeSpace >= requiredSpace;
        }

        private async Task<bool> IsEndpointReachableAsync(string url)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(5);

                    var request = new HttpRequestMessage(HttpMethod.Head, url);
                    var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
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

            var Arguments = "";
            var used_mods = "";

            if (File.Exists($"{launcherAppData}/enabled_submods.txt"))
            {
                var EnabledSubmodsRaw = File.ReadAllLines($"{launcherAppData}/enabled_submods.txt").ToList();
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
                            new DiscordRPC.Button() { Label = "Join Discord", Url = "https://discord.gg/KMhmdCb7Ut" },
                            new DiscordRPC.Button() { Label = "Download Mod", Url = "https://www.nexusmods.com/totalwarattila/mods/1?tab=files" },
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
                            new DiscordRPC.Button() { Label = "Join Discord", Url = "https://discord.gg/KMhmdCb7Ut" },
                            new DiscordRPC.Button() { Label = "Download Mod", Url = "https://www.nexusmods.com/totalwarattila/mods/1?tab=files" },
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
            ProgressBarProgress = percent_finished;
        }

        private async Task ExtractArchive(string downloadArchiveFullName, string extractPath)
        {
            ProgressText = "EXTRACTING DATA...";
            ShowProgressBar = Visibility.Visible;

            Logger.Log($"DownloadCompleted. Extracting {downloadArchiveFullName}...");
            
            try
            {
                var workingDir = AppDomain.CurrentDomain.BaseDirectory;
                var architecture = Environment.Is64BitProcess ? "x64" : "x86";
                var sevenZipPath = Path.Combine(workingDir, architecture, "7z.dll");
                SevenZipExtractor.SetLibraryPath(sevenZipPath);

                using (var archiveFile = new SevenZipExtractor(downloadArchiveFullName))
                {
                    archiveFile.Extracting += (s, e) =>
                    {
                        ExtractProgressUpdate(e.PercentDone);
                    };

                    await archiveFile.ExtractArchiveAsync(extractPath);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"DownloadCompleted. Extraction failed: {ex.Message}");
                MessageBox.Show($"An exception occured while trying to extract mod files. Please forward the below message to the devs:\n{ex.Message}", "Failed to extract");
                return;
            }

            Logger.Log($"DownloadCompleted. Deleting {downloadArchiveFullName}...");
            File.Delete(downloadArchiveFullName);
            Logger.Log($"DownloadCompleted. Deleted {downloadArchiveFullName}");

            try
            {
                var enabledSubmodsFile = $"{launcherAppData}/enabled_submods.txt";
                if (File.Exists(enabledSubmodsFile))
                {
                    Logger.Log($"DownloadCompleted. Deleting {enabledSubmodsFile}...");
                    File.Delete(enabledSubmodsFile);
                    Logger.Log($"DownloadCompleted. Deleted{enabledSubmodsFile}");
                }

                var localVersionFile = $"{launcherAppData}/local_version.txt";
                if (File.Exists(localVersionFile))
                {
                    Logger.Log($"DownloadCompleted. Deleting {localVersionFile}...");
                    File.Delete(localVersionFile);
                    Logger.Log($"DownloadCompleted. Deleted{localVersionFile}");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"DownloadCompleted. Exception: {ex.Message}");
                MessageBox.Show($"An error happened. Please forward this to devs: {ex.Message}", "Exception");
            }
        }

        private void UpdateLocalVersion()
        {
            var client = new WebClient();

            Logger.Log($"DownloadCompleted. Downloading launcher local version");
            client.DownloadFile(new Uri("http://80.208.231.54/launcher/local_version.txt"), $"{launcherAppData}/local_version.txt");

            using (var x = new StreamWriter($"{launcherAppData}/user_preferences.txt"))
            {
                x.Write($"auto_update=true{Environment.NewLine}load_order = {{{Environment.NewLine}rom_base{Environment.NewLine}}}");
            }
        }

        private void ExtractProgressUpdate(int progress)
        {
            var percent_finished = progress;
            ProgressText = $"EXTRACTING {progress}%";
            ProgressBarProgress = percent_finished;
        }

        private async Task MarkReadyToPlay()
        {
            Version = await _modVersionService.GetModVersionInfo(SharedData);
            VersionText = "Version " + Version.VersionText;
            ChangelogText = Version.ChangeLog;

            PlayButtonText = "PLAY";
            PlayButtonEnabled = true;
            PlayButtonMargin = "450 30";
            SubmodButtonEnabled = true;
            ShowProgressBar = Visibility.Hidden;
        }

        private void MarkCannotPlay()
        {
            PlayButtonText = "ERROR";
            PlayButtonEnabled = false;
            PlayButtonMargin = "450 30";
            SubmodButtonEnabled = false;
            ShowProgressBar = Visibility.Hidden;
        }

        private void DisableSubmod(string id)
        {
            string output = "";

            if (!File.Exists($"{launcherAppData}/enabled_submods.txt"))
            {
                return;
            }

            string[] lines = File.ReadAllLines($"{launcherAppData}/enabled_submods.txt");
            if (lines.Count() == 0)
            {
                try { File.Delete($"{launcherAppData}/enabled_submods.txt"); } catch { }
                return;
            }
            else
            {
                if (lines.Contains(id))
                {
                    if (lines.ToString() == id)
                    {
                        try { File.Delete($"{launcherAppData}/enabled_submods.txt"); } catch { }
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

            using (StreamWriter writer = new StreamWriter($"{launcherAppData}/enabled_submods.txt"))
            {
                writer.Write(output);
            }
        }

        private void WritePrefs(UserPreferences prefs2)
        {
            if (!File.Exists($"{launcherAppData}/user_preferences.txt"))
                File.CreateText($"{launcherAppData}/user_preferences.txt");

            if (File.Exists($"{launcherAppData}/enabled_submods.txt"))
            {

                var EnabledSubmodsRaw = File.ReadAllLines($"{launcherAppData}/enabled_submods.txt").ToList();
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

            using (var x = new StreamWriter($"{launcherAppData}/user_preferences.txt"))
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
