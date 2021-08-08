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

namespace RiseofMordorLauncher
{
    class SubmodsViewModel : BaseViewModel
    {
        public List<SubmodModel> SubmodsList { get; private set; }
        ISteamSubmodsService steamSubmodsService = new APISteamSubmodService();
        public SharedData sharedData { get; set; }

        public SubmodsViewModel()
        {
        }

        public async void Load()
        {
            steamSubmodsService.SubmodDataFinishedEvent += LoadSubmodData;
            steamSubmodsService.GetSubmods(sharedData);
        }

        private async void LoadSubmodData(object sender, List<SubmodModel> submods)
        {
            SubmodsList = submods;
        }




    }
}
