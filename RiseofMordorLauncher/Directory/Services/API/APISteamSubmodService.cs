﻿using Steamworks;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows;

namespace RiseofMordorLauncher
{
    class APISteamSubmodService : ISteamSubmodsService
    {
        public event EventHandler<List<SubmodModel>> SubmodDataFinishedEvent;
        public SharedData sharedData;
        private CallResult<SteamUGCQueryCompleted_t> OnSteamUGCQueryCompletedCallResult;
        private string[] enabled_submods = { };
        private uint amount = 0;
        public List<SubmodModel> submodList;
        private List<PublishedFileId_t> SubscribedSubmods;  
        
        public APISteamSubmodService()
        {
            OnSteamUGCQueryCompletedCallResult = CallResult<SteamUGCQueryCompleted_t>.Create(OnSteamUGCQueryCompleted);
        }

        public async void GetSubmods(SharedData sharedDataInput)
        {
            sharedData = sharedDataInput;

            // get array of enabled submods
            if (File.Exists($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"))
            {
                enabled_submods = File.ReadAllLines($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt");          
            }

            // Get list of rom submods id's
            var driveService = new APIGoogleDriveService();
            await driveService.DownloadFile("approved_submods.txt", $"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/approved_submods.txt", 1);
            var ApprovedSubmodsIdString = File.ReadAllLines($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/approved_submods.txt");

            var ApprovedSubmodsIdList = new List<PublishedFileId_t>();
            foreach (var item in ApprovedSubmodsIdString)
            {
                var t = new PublishedFileId_t()
                {
                    m_PublishedFileId = ulong.Parse(item),
                };

                ApprovedSubmodsIdList.Add(t);
                amount++;
            }

            // get subscribed submods
            var SubscribedItems = new PublishedFileId_t[SteamUGC.GetNumSubscribedItems()];
            SubscribedSubmods = new List<PublishedFileId_t>();
            SteamUGC.GetSubscribedItems(SubscribedItems, SteamUGC.GetNumSubscribedItems());
            foreach (var item in SubscribedItems)
            {
                if (ApprovedSubmodsIdList.Contains(item))
                {
                    SubscribedSubmods.Add(item);
                }
            }

            // send data request to steam
            var details = SteamUGC.CreateQueryUGCDetailsRequest(ApprovedSubmodsIdList.ToArray(), amount);
            SteamUGC.SetReturnLongDescription(details, true);
            var request = SteamUGC.SendQueryUGCRequest(details);
            OnSteamUGCQueryCompletedCallResult.Set(request);
        }

        void OnSteamUGCQueryCompleted(SteamUGCQueryCompleted_t pCallback, bool bIOFailure)
        {
            submodList = new List<SubmodModel>();
            for (uint i = 0; i < amount; i++)
            {
                SteamUGC.GetQueryUGCResult(pCallback.m_handle, i, out SteamUGCDetails_t detail);

                bool bEnabled = enabled_submods.Count() >= 1 && enabled_submods.Contains(detail.m_nPublishedFileId.m_PublishedFileId.ToString());
                bool bInstalled = SubscribedSubmods.Contains(detail.m_nPublishedFileId);
                
                var submod                          = new SubmodModel();

                submod.IsEnabled                    = bEnabled;
                submod.EnableButtonBackground       = bEnabled ? Brushes.Red : SharedData.NiceGreen;
                submod.EnableButtonText             = bEnabled ? "DISABLE" : "ENABLE";

                submod.IsInstalled                  = bInstalled;
                submod.SubscribeButtonBackground    = bInstalled ? Brushes.Red : SharedData.NiceGreen;
                submod.SubscribeButtonText          = bInstalled ? "UNSUBSCRIBE" : "SUBSCRIBE";
                submod.EnableButtonVisibility       = bInstalled ? Visibility.Visible : Visibility.Hidden;

                submod.SubmodName                   = detail.m_rgchTitle;
                submod.SubmodSteamId                = detail.m_nPublishedFileId.m_PublishedFileId.ToString();
                submod.SubmodDesc                   = detail.m_rgchDescription;
                submod.UpvoteCount                  = (short)detail.m_unVotesUp;
                submod.DownvoteCount                = (short)detail.m_unVotesDown;
                submod.SteamId                      = detail.m_nPublishedFileId.ToString();
                submod.ProgressBarVisibility        = Visibility.Hidden;

                if (bInstalled)
                {
                    string install_dir = "";
                    if (SteamUGC.GetItemInstallInfo(detail.m_nPublishedFileId, out _, out install_dir, (uint)install_dir.ToCharArray().Count(), out _))
                    {
                        submod.InstallDir = install_dir;
                    }
                }

                string thumbUrl = "";
                SteamUGC.GetQueryUGCPreviewURL(pCallback.m_handle, i, out thumbUrl, 1000);
                submod.ThumbnailPath = thumbUrl;

                if (submod.SubmodName.Length > 34)
                {
                    submod.SubmodName = submod.SubmodName.Substring(0, 32);
                    submod.SubmodName = submod.SubmodName + "...";
                }

                submodList.Add(submod);
            }

            SubmodDataFinishedEvent?.Invoke(this, submodList);
        }

        public SubmodInstallation GetSubmodInstallInfo(ulong id)
        {
            SubmodInstallation submod_installation = new SubmodInstallation();
            PublishedFileId_t submod;
            string folder = "";

            try { submod = new PublishedFileId_t(id); } catch { submod_installation.IsInstalled = false; return submod_installation; }

            bool success = SteamUGC.GetItemInstallInfo(submod, out _, out folder, 100, out _);

            if (success)
            {
                submod_installation.IsInstalled = true;
                submod_installation.InstallFolder = folder;
                submod_installation.ID = id;
                FileInfo[] files = new DirectoryInfo(folder).GetFiles();
                bool pack_found = false;

                foreach (FileInfo file in files)
                {
                    if (file.Extension == ".pack")
                    {
                        submod_installation.FileName = file.Name;
                        pack_found = true;
                    }
                }

                if (pack_found)
                {
                    return submod_installation;
                }
                else
                {
                    submod_installation.IsInstalled = false; return submod_installation;
                }
            }
            else
            {
                submod_installation.IsInstalled = false; return submod_installation;
            }
        }
    }

}
