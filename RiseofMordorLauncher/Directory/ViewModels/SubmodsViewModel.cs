﻿using System;
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
        public IList<SubmodModel> SubmodsList1 { get; set; }
        public IList<SubmodModel> SubmodsList2 { get; set; }
        public IList<SubmodModel> SubmodsList3 { get; set; }
        public IList<SubmodModel> DownloadingSubmods { get; set; } = new List<SubmodModel>();
        public string BackgroundImage { get; set; }

        public event EventHandler<ApplicationPage> SwitchPageEvent;

        private System.Timers.Timer SubmodDownloadTimer;

        ISteamSubmodsService steamSubmodsService = new APISteamSubmodService();
        public SharedData SharedData { get; set; }

        public SubmodsViewModel()
        {
            SubmodsList1 = new ObservableCollection<SubmodModel>();
            SubmodsList2 = new ObservableCollection<SubmodModel>();
            SubmodsList3 = new ObservableCollection<SubmodModel>();
        }

        public void Load()
        {
            var userPreferencesService = new APIUserPreferencesService();
            var prefs = userPreferencesService.GetUserPreferences(SharedData);

            BackgroundImage = $"Directory/Images/{prefs.BackgroundImage}";

            steamSubmodsService.SubmodDataFinishedEvent += LoadSubmodData;
            steamSubmodsService.GetSubmods(SharedData);

            SubmodDownloadTimer = new System.Timers.Timer(300);
            SubmodDownloadTimer.Elapsed += (o, s) => Task.Factory.StartNew(() => CheckDownloads(o, s));
            SubmodDownloadTimer.AutoReset = true;
            SubmodDownloadTimer.Start();
        }

        public void LoadSubmodData(object sender, List<SubmodModel> submods)
        {
            Application.Current.Dispatcher.Invoke(new ThreadStart(() =>
            {
                submods = submods.OrderByDescending(x => x.UpvoteCount - x.DownvoteCount).ToList();
                int current_list = 1;

                foreach (var submod in submods)
                {
                    if (current_list == 1)
                    {
                        SubmodsList1.Add(submod);
                        current_list = 2;
                    }
                    else if (current_list == 2)
                    {
                        SubmodsList2.Add(submod);
                        current_list = 3;
                    }
                    else
                    {
                        SubmodsList3.Add(submod);
                        current_list = 1;
                    }

                    submod.VisitSteamPressed        += VisitSteamPressed;
                    submod.SubscribeButtonPressed   += SubscribeButtonPressed;
                    submod.EnableButtonPressed      += EnableButtonPressed;
                    submod.UpvoteButtonPressed      += UpvoteButtonPressed;
                    submod.DownvoteButtonPressed    += DownvoteButtonPressed;
                }
            }));
        }

        private void VisitSteamPressed(object sender, EventArgs e)
        {
            var submod = sender as SubmodModel;
            Process.Start($"https://steamcommunity.com/sharedfiles/filedetails/?id={submod.SubmodSteamId}");
        }

        private void SubscribeButtonPressed(object sender, EventArgs e)
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
                submod.EnableButtonVisibility       = Visibility.Hidden;
                submod.EnableButtonText             = "ENABLE";
                submod.EnableButtonBackground       = Brushes.LightGreen;
                submod.EnableButtonForeground       = Brushes.Black;
                submod.SubscribeButtonBackground    = Brushes.LightGreen;
                submod.SubscribeButtonForeground    = Brushes.Black;
                submod.SubscribeButtonText          = "SUBSCRIBE";
                submod.IsInstalled                  = false;
                submod.IsEnabled                    = false;
                submod.ProgressBarValue             = 2;
            }
        }

        private void EnableButtonPressed(object sender, EventArgs e)
        {
            SubmodModel submod = (SubmodModel)sender;
            PublishedFileId_t item = new PublishedFileId_t(ulong.Parse(submod.SteamId));

            if (submod.IsEnabled == false)
            {
                EnableSubmod(submod);
                submod.IsEnabled = true;
                submod.EnableButtonBackground   = Brushes.OrangeRed;
                submod.EnableButtonForeground   = Brushes.White;
                submod.EnableButtonText         = "DISABLE";
            }
            else
            {               
                DisableSubmod(submod);
                submod.IsEnabled = false;
                submod.EnableButtonBackground   = Brushes.LightGreen;
                submod.EnableButtonForeground   = Brushes.Black;
                submod.EnableButtonText         = "ENABLE";
            }
        }

        private void UpvoteButtonPressed(object sender, EventArgs e)
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
        private void DownvoteButtonPressed(object sender, EventArgs e)
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
                                    submod.IsInstalled                  = true;
                                    submod.ProgressBarVisibility        = Visibility.Hidden;
                                    submod.ProgressBarValue             = 0;
                                    submod.SubscribeButtonBackground    = Brushes.OrangeRed;
                                    submod.SubscribeButtonForeground    = Brushes.White;
                                    submod.SubscribeButtonText          = "UNSUBSCRIBE";
                                    submod.EnableButtonVisibility       = Visibility.Visible;
                                    submod.EnableButtonText             = "ENABLE";
                                    submod.EnableButtonBackground       = Brushes.LightGreen;
                                    submod.EnableButtonForeground       = Brushes.Black;

                                    DownloadingSubmods.RemoveAt(i);
                                }
                                else
                                {
                                    submod.ProgressBarValue = Math.Round((decimal)downloaded_bytes / total_bytes * 100);
                                }
                            }
                            else if (total_bytes == 0 && submod.ProgressBarValue > 1)
                            {
                                submod.IsInstalled                  = true;
                                submod.ProgressBarVisibility        = Visibility.Hidden;
                                submod.ProgressBarValue             = 0;
                                submod.SubscribeButtonBackground    = Brushes.OrangeRed;
                                submod.SubscribeButtonForeground    = Brushes.White;
                                submod.SubscribeButtonText          = "UNSUBSCRIBE";
                                submod.EnableButtonVisibility       = Visibility.Visible;
                                submod.EnableButtonText             = "ENABLE";
                                submod.EnableButtonBackground       = Brushes.LightGreen;
                                submod.EnableButtonForeground       = Brushes.Black;
                                DownloadingSubmods.RemoveAt(i);

                            }

                        }
                        else
                        {
                            submod.IsInstalled                      = true;
                            submod.ProgressBarVisibility            = Visibility.Hidden;
                            submod.ProgressBarValue                 = 0;
                            submod.SubscribeButtonBackground        = Brushes.OrangeRed;
                            submod.SubscribeButtonBackground        = Brushes.White;
                            submod.SubscribeButtonText              = "UNSUBSCRIBE";
                            submod.EnableButtonVisibility           = Visibility.Visible;
                            submod.EnableButtonText                 = "ENABLE";
                            submod.EnableButtonBackground           = Brushes.LightGreen;
                            submod.EnableButtonBackground           = Brushes.Black;
                            DownloadingSubmods.RemoveAt(i);
                        }
                    }
                }

            }
            catch { }
        }

        private void EnableSubmod(SubmodModel submod)
        {
            string output = "";

            if (!File.Exists($"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"))
            {
                using (StreamWriter writer = new StreamWriter($"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"))
                {
                    try { File.CreateText($"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"); } catch { }
                }
            }

            string[] lines = File.ReadAllLines($"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt");
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

            using (StreamWriter writer = new StreamWriter($"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"))
            {
                writer.Write(output);
            }
        }

        private void DisableSubmod(SubmodModel submod)
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
                if (lines.Contains(submod.SteamId))
                {
                    if (lines.ToString() == submod.SteamId)
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

            using (StreamWriter writer = new StreamWriter($"{SharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"))
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
                return _SubmitCommand ?? (_SubmitCommand = new CommandHandler(() => { MessageBox.Show("Creator of a The Dawnless Days submod? Contact Case#9810 on Discord to get your submod approved and added to the list.", "Submit a Submod"); }, () => true));
            }
        }
    }
}
