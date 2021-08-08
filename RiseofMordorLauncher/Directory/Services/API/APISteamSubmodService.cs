using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace RiseofMordorLauncher
{
    class APISteamSubmodService : ISteamSubmodsService
    {
        public event EventHandler<List<SubmodModel>> SubmodDataFinishedEvent;
        private CallResult<SteamUGCQueryCompleted_t> OnSteamUGCQueryCompletedCallResult;
        private string[] enabled_submods = { };
        private uint amount = 0;
        public List<SubmodModel> submodList;
        private List<PublishedFileId_t> SubscribedSubmods;
        public void GetSubmods(SharedData sharedData)
        {
            OnSteamUGCQueryCompletedCallResult = CallResult<SteamUGCQueryCompleted_t>.Create(OnSteamUGCQueryCompleted);

            // get array of enabled submods
            if (File.Exists($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"))
            {
                enabled_submods = File.ReadAllLines($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt");
            }

            // Get list of rom submods id's
            IGoogleDriveService driveService = new APIGoogleDriveService();
       //     driveService.DownloadFile("approved_submods.txt", $"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/approved_submods.txt", 1);
            string[] ApprovedSubmodsIdString = File.ReadAllLines($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/approved_submods.txt");

            List<PublishedFileId_t> ApprovedSubmodsIdList = new List<PublishedFileId_t>();
            foreach (var item in ApprovedSubmodsIdString)
            {
                PublishedFileId_t t = new PublishedFileId_t();
                t.m_PublishedFileId = ulong.Parse(item);
                ApprovedSubmodsIdList.Add(t);

                amount++;
            }

            // get subscribed submods
            SteamAPI.Init();
            PublishedFileId_t[] SubscribedItems = new PublishedFileId_t[SteamUGC.GetNumSubscribedItems()];
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
            var request = SteamUGC.SendQueryUGCRequest(details);
            OnSteamUGCQueryCompletedCallResult.Set(request);

        }

        void OnSteamUGCQueryCompleted(SteamUGCQueryCompleted_t pCallback, bool bIOFailure)
        {
            Console.WriteLine("hit");
            SteamUGCDetails_t detail;
            // Create SubmodModel list and populate.
            submodList = new List<SubmodModel>();
            for (uint i = 0; i < amount; i++)
            {
                SteamUGC.GetQueryUGCResult(pCallback.m_handle, i, out detail);

                SubmodModel submod = new SubmodModel();

                if (enabled_submods.Count() >= 1)
                {
                    if (enabled_submods.Contains(detail.m_nPublishedFileId.m_PublishedFileId.ToString()))
                    {
                        submod.IsEnabled = true;
                        submod.EnableButtonBackground = Brushes.Red;
                        submod.EnableButtonText = "DISABLE";
                        submod.EnableButtonVisibility = System.Windows.Visibility.Visible;
                    }
                    else
                    {
                        submod.IsEnabled = false;
                        submod.EnableButtonBackground = Brushes.Green;
                        submod.EnableButtonText = "ENABLE";
                        submod.EnableButtonVisibility = System.Windows.Visibility.Visible;
                    }
                }
                if (SubscribedSubmods.Contains(detail.m_nPublishedFileId))
                {
                    submod.IsInstalled = true;
                    submod.SubscribeButtonBackground = Brushes.Red;
                    submod.SubscribeButtonText = "UNSUBSCRIBE";
                    submod.EnableButtonVisibility = System.Windows.Visibility.Visible;

                    string install_dir = "";
                    if (SteamUGC.GetItemInstallInfo(detail.m_nPublishedFileId, out _, out install_dir, (uint)install_dir.ToCharArray().Count(), out _))
                    {
                        submod.InstallDir = install_dir;
                    }
                }
                else
                {
                    submod.IsInstalled = false;
                    submod.SubscribeButtonBackground = Brushes.Green;
                    submod.SubscribeButtonText = "SUBSCRIBE";
                    submod.EnableButtonVisibility = System.Windows.Visibility.Hidden;
                }

                submod.SubmodName = detail.m_rgchTitle;
                submod.SubmodSteamId = detail.m_nPublishedFileId.m_PublishedFileId.ToString();
                submod.ThumbnailPath = detail.m_rgchURL;
                submod.SubmodDesc = detail.m_rgchDescription;
                Console.WriteLine(submod.SubmodDesc.Length);

                submodList.Add(submod);
            }

            SubmodDataFinishedEvent?.Invoke(this, submodList);
        }
    }

}
