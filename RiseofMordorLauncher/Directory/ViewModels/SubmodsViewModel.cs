using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Steamworks;
using System.Windows;
using System.Windows.Media;
using System.Net.Http;
using System.Windows.Input;
using RiseofMordorLauncher.Directory.Services;
using System.Diagnostics;
using System.Timers;
using System.Threading;
using System.IO;

namespace RiseofMordorLauncher
{
    class SubmodsViewModel : BaseViewModel
    {
        public ObservableCollection<SubmodModel> SubmodsList1 { get; private set; }
        public ObservableCollection<SubmodModel> SubmodsList2 { get; private set; }
        public ObservableCollection<SubmodModel> SubmodsList3 { get; private set; }
        public List<SubmodModel> DownloadingSubmods { get; set; } = new List<SubmodModel>();

        public event EventHandler<ApplicationPage> SwitchPageEvent;

        private System.Timers.Timer SubmodDownloadTimer;

        ISteamSubmodsService steamSubmodsService = new APISteamSubmodService();
        public SharedData sharedData { get; set; }

        public SubmodsViewModel()
        {
        }

        public async void Load()
        {
            await Application.Current.Dispatcher.BeginInvoke(
                new ThreadStart(() =>
                {
                    steamSubmodsService.SubmodDataFinishedEvent += LoadSubmodData;

                })
            );
            steamSubmodsService.GetSubmods(sharedData);

            Thread DownloadUpdateThread = new Thread(SetupTimer);
            DownloadUpdateThread.IsBackground = true;
            DownloadUpdateThread.Start();
        }

        private async void LoadSubmodData(object sender, List<SubmodModel> submods)
        {
            submods = submods.OrderByDescending(x => x.UpvoteCount - x.DownvoteCount).ToList();

            int current_list = 1;
            SubmodsList1 = new ObservableCollection<SubmodModel>();
            SubmodsList2 = new ObservableCollection<SubmodModel>();
            SubmodsList3 = new ObservableCollection<SubmodModel>();

            foreach (var submod in submods)
            {
                if (current_list == 1)
                {
                    await Application.Current.Dispatcher.BeginInvoke(
                    new ThreadStart(() =>
                    {
                        SubmodsList1.Add(submod);
                     })
                    );
                    current_list = 2;
                }
                else if (current_list == 2)
                {
                    await Application.Current.Dispatcher.BeginInvoke(
                    new ThreadStart(() =>
                    {
                        SubmodsList2.Add(submod);
                    })
                    );
                    current_list = 3;
                }
                else
                {
                    await Application.Current.Dispatcher.BeginInvoke(
                    new ThreadStart(() =>
                    {
                        SubmodsList3.Add(submod);
                    })
                    );
                    current_list = 1;
                }

                submod.VisitSteamPressed += VisitSteamPressed;
                submod.SubscribeButtonPressed += SubscribeButtonPressed;
                submod.EnableButtonPressed += EnableButtonPressed;
                submod.UpvoteButtonPressed += UpvoteButtonPressed;
                submod.DownvoteButtonPressed += DownvoteButtonPressed;
            }
        }
        private async void VisitSteamPressed(object sender, EventArgs e)
        {
            SubmodModel submod = (SubmodModel)sender;

            Process.Start($"https://steamcommunity.com/sharedfiles/filedetails/?id={submod.SubmodSteamId}");
        }
        private async void SubscribeButtonPressed(object sender, EventArgs e)
        {
            SubmodModel submod = (SubmodModel)sender;
            PublishedFileId_t item = new PublishedFileId_t(ulong.Parse(submod.SteamId));

            if (submod.IsInstalled == false)
            {
                SteamUGC.SubscribeItem(item);
                SteamUGC.DownloadItem(item, true);
                submod.ProgressBarVisibility = Visibility.Visible;

                if (submod.ProgressBarValue != 2)
                    submod.ProgressBarValue = 0;

                if (DownloadingSubmods.Count > 0)
                {
                    foreach (var download_submod in DownloadingSubmods)
                    {
                        if (submod.SteamId == download_submod.SteamId)
                        {
                            return;
                        }
                    }
                }
                else
                {
                    DownloadingSubmods.Add(submod);
                    return;
                }
                DownloadingSubmods.Add(submod);

            }
            else
            {
                SteamUGC.UnsubscribeItem(item);
                DisableSubmod(submod);
                submod.EnableButtonVisibility = Visibility.Hidden;
                submod.EnableButtonText = "ENABLE";
                submod.EnableButtonBackground = SharedData.NiceGreen;
                submod.SubscribeButtonBackground = SharedData.NiceGreen;
                submod.SubscribeButtonText = "SUBSCRIBE";
                submod.IsInstalled = false;
                submod.IsEnabled = false;
                submod.ProgressBarValue = 2;
            }
        }
        private async void EnableButtonPressed(object sender, EventArgs e)
        {
            SubmodModel submod = (SubmodModel)sender;
            PublishedFileId_t item = new PublishedFileId_t(ulong.Parse(submod.SteamId));

            if (submod.IsEnabled == false)
            {
                EnableSubmod(submod);
                submod.IsEnabled = true;
                submod.EnableButtonBackground = Brushes.Red;
                submod.EnableButtonText = "DISABLE";
            }
            else
            {               
                DisableSubmod(submod);
                submod.IsEnabled = false;
                submod.EnableButtonBackground = SharedData.NiceGreen;
                submod.EnableButtonText = "ENABLE";
            }
        }
        private async void UpvoteButtonPressed(object sender, EventArgs e)
        {
            SubmodModel submod = (SubmodModel)sender;

            if (submod.has_voted)
            {
                if (!submod.up_voted)
                {
                    PublishedFileId_t item = new PublishedFileId_t(ulong.Parse(submod.SteamId));
                    submod.up_voted = true;
                    submod.down_voted = false;
                    submod.UpvoteCount = (short)(submod.UpvoteCount + 1);
                    submod.DownvoteCount = (short)(submod.DownvoteCount - 1);
                    SteamUGC.SetUserItemVote(item, true);
                }
            }
            else
            {
                PublishedFileId_t item = new PublishedFileId_t(ulong.Parse(submod.SteamId));
                submod.has_voted = true;
                submod.up_voted = true;
                submod.down_voted = false;
                submod.UpvoteCount = (short)(submod.UpvoteCount + 1);
                SteamUGC.SetUserItemVote(item, true);
            }
        }
        private async void DownvoteButtonPressed(object sender, EventArgs e)
        {
            SubmodModel submod = (SubmodModel)sender;

            if (submod.has_voted)
            {
                if (!submod.down_voted)
                {
                    PublishedFileId_t item = new PublishedFileId_t(ulong.Parse(submod.SteamId));
                    submod.up_voted = false;
                    submod.down_voted = true;
                    submod.UpvoteCount = (short)(submod.UpvoteCount - 1);
                    submod.DownvoteCount = (short)(submod.DownvoteCount + 1);
                    SteamUGC.SetUserItemVote(item, false);
                }
            }
            else
            {
                PublishedFileId_t item = new PublishedFileId_t(ulong.Parse(submod.SteamId));
                submod.has_voted = true;
                submod.up_voted = false;
                submod.down_voted = true;
                submod.DownvoteCount = (short)(submod.DownvoteCount + 1);
                SteamUGC.SetUserItemVote(item, false);
            }
        }

        private void SetupTimer()
        {
            SubmodDownloadTimer = new System.Timers.Timer(300);
            SubmodDownloadTimer.Elapsed += CheckDownloads;
            SubmodDownloadTimer.AutoReset = true;
            SubmodDownloadTimer.Start();
        }
        private void CheckDownloads(object source, ElapsedEventArgs e)
        {
            try
            {
                if (DownloadingSubmods.Count > 0)
                {
                    int i = -1;

                    foreach (SubmodModel submod in DownloadingSubmods)
                    {
                        i++;
                        PublishedFileId_t item = new PublishedFileId_t(ulong.Parse(submod.SteamId));

                        ulong downloaded_bytes = 0;
                        ulong total_bytes = 0;
                        bool success = false;

                        try
                        {
                            success = SteamUGC.GetItemDownloadInfo(item, out downloaded_bytes, out total_bytes);
                        }
                        catch { }

                        if (success)
                        {
                            if (!(total_bytes == 0))
                            {
                                if (downloaded_bytes >= total_bytes)
                                {
                                    submod.IsInstalled = true;
                                    submod.ProgressBarVisibility = Visibility.Hidden;
                                    submod.ProgressBarValue = 0;
                                    submod.SubscribeButtonBackground = Brushes.Red;
                                    submod.SubscribeButtonText = "UNSUBSCRIBE";
                                    submod.EnableButtonVisibility = Visibility.Visible;
                                    submod.EnableButtonText = "ENABLE";
                                    submod.EnableButtonBackground = SharedData.NiceGreen;

                                    DownloadingSubmods.RemoveAt(i);
                                }
                                else
                                {
                                    submod.ProgressBarValue = Math.Round((decimal)downloaded_bytes / total_bytes * 100);
                                }
                            }
                            else if (total_bytes == 0 && submod.ProgressBarValue > 1)
                            {
                                submod.IsInstalled = true;
                                submod.ProgressBarVisibility = Visibility.Hidden;
                                submod.ProgressBarValue = 0;
                                submod.SubscribeButtonBackground = Brushes.Red;
                                submod.SubscribeButtonText = "UNSUBSCRIBE";
                                submod.EnableButtonVisibility = Visibility.Visible;
                                submod.EnableButtonText = "ENABLE";
                                submod.EnableButtonBackground = SharedData.NiceGreen;
                                DownloadingSubmods.RemoveAt(i);

                            }

                        }
                        else
                        {
                            submod.IsInstalled = true;
                            submod.ProgressBarVisibility = Visibility.Hidden;
                            submod.ProgressBarValue = 0;
                            submod.SubscribeButtonBackground = Brushes.Red;
                            submod.SubscribeButtonText = "UNSUBSCRIBE";
                            submod.EnableButtonVisibility = Visibility.Visible;
                            submod.EnableButtonText = "ENABLE";
                            submod.EnableButtonBackground = SharedData.NiceGreen;
                            DownloadingSubmods.RemoveAt(i);
                        }
                    }
                }

            }
            catch { }
        }
        private async void EnableSubmod(SubmodModel submod)
        {
            string output = "";

            if (!File.Exists($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"))
            {
                using (StreamWriter writer = new StreamWriter($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"))
                {
                    try { File.CreateText($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"); } catch { }
                }
            }

            string[] lines = File.ReadAllLines($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt");
            if (lines.Count() == 0)
            {
                output = submod.SteamId;
            }
            else
            {
                if (!lines.Contains(submod.SteamId))
                {
                    output = string.Join(Environment.NewLine, lines) + Environment.NewLine + submod.SteamId;
                }
            }

            using (StreamWriter writer = new StreamWriter($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"))
            {
                writer.Write(output);
            }
        }
        private async void DisableSubmod(SubmodModel submod)
        {
            string output = "";

            if (!File.Exists($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"))
            {
                return;
            }

            string[] lines = File.ReadAllLines($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt");
            if (lines.Count() == 0)
            {
                try { File.Delete($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"); } catch { }
                return;
            }
            else
            {
                if (lines.Contains(submod.SteamId))
                {
                    if (lines.ToString() == submod.SteamId)
                    {
                        try { File.Delete($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"); } catch { }
                        return;
                    }
                    else
                    {
                        foreach (var line in lines)
                        {
                            if (output == "")
                            {
                                if (line == submod.SteamId)
                                {

                                }
                                else
                                {
                                    output = line;
                                }
                            }
                            else
                            {
                                if (line == submod.SteamId)
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

            using (StreamWriter writer = new StreamWriter($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"))
            {
                writer.Write(output);
            }

        }
        protected virtual void SwitchPage(ApplicationPage page)
        {
            SwitchPageEvent?.Invoke(this, page);
        }

        private ICommand _BackCommand;
        private ICommand _SubmitCommand;
        public ICommand BackCommand
        {
            get
            {
                return _BackCommand ?? (_BackCommand = new CommandHandler(() => SwitchPage(ApplicationPage.MainLauncher), () => true));
            }
        }

        public ICommand SubmitCommand
        {
            get
            {
                return _SubmitCommand ?? (_SubmitCommand = new CommandHandler(() => { MessageBox.Show("Creator of a Rise of Mordor submod? Contact Case#9810 on Discord to get your submod approved and added to the list.", "Submit a Submod"); }, () => true));
            }
        }

    }
}
