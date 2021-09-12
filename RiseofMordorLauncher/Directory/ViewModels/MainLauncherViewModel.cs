﻿using RiseofMordorLauncher.Directory.Services;
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


namespace RiseofMordorLauncher
{
    class MainLauncherViewModel : BaseViewModel
    {
        private ISteamUserService _steamUserService;
        private IYouTubeDataService _youTubeDataService;
        private IModVersionService _modVersionService;
        private IUserPreferencesService UserPreferencesService;
        private ISteamSubmodsService SubmodService;

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
        public Visibility SettingsVisibility { get; set; } = Visibility.Hidden;
        public Settings SettingsPage { get; set; } = new Settings();
        private SettingsViewModel SettingsPageViewModel { get; set; }
        
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


        public async Task Load()
        {
            // get steam user data
            _steamUserService = new APISteamUserService();
            var user = await _steamUserService.GetSteamUser();
            SteamUserName = user.UserName;
            SteamAvatarUrl = user.AvatarUrl;

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

            // get version data
            _modVersionService = new APIModVersionService();
            Version = await _modVersionService.GetModVersionInfo(SharedData);
            VersionText = "Version " + Version.VersionText;
            ChangelogText = Version.ChangeLog;

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

        private async void DownloadUpdate()
        {

            PlayButtonText = "UPDATING";
            PlayButtonEnabled = false;
            PlayButtonMargin = "350 30";
            SubmodButtonEnabled = false;
            ShowProgressBar = Visibility.Visible;

            IGoogleDriveService googleDriveService = new APIGoogleDriveService();
            googleDriveService.DownloadUpdate += DownloadProgressUpdate;
            await googleDriveService.DownloadFile("rom_pack_files.rar", $"{SharedData.AttilaDir}/data/rom_pack_files.rar", Version.DownloadNumberOfBytes);
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

        private void LaunchGame()
        {
            UserPreferences prefs = new UserPreferences();
            SubmodService = new APISteamSubmodService();
            UserPreferencesService = new APIUserPreferencesService();

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
            }
            else
            {
                SettingsVisibility = Visibility.Hidden;
            }
        }

        private void DownloadProgressUpdate(object sender, int percent_finished)
        {
            ProgressBarProgress = percent_finished;
        }

        private async void DisableSubmod(string id)
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
            SwitchPageEvent.Invoke(this, page);
        }

    }
}
