using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace RiseofMordorLauncher
{
    class MainLauncherViewModel : BaseViewModel
    {
        public ISteamUserService _steamUserService;
        public IYouTubeDataService _youTubeDataService;
        public IModVersionService _modVersionService;

        public event EventHandler<ApplicationPage> SwitchPageEvent;
        public string SteamUserName { get; set; } 
        public string SteamAvatarUrl { get; set; }      
        public string YouTubeVideoURL { get; set; }
        public SharedData SharedData { get; set; }
        public Visibility ShowVideo { get; set; } = Visibility.Visible;

        public async Task Load()
        {

            // get steam user data
            _steamUserService = new APISteamUserService();
            SteamUser user = await _steamUserService.GetSteamUser();
            SteamUserName = user.UserName;
            SteamAvatarUrl = user.AvatarUrl;

            // get YoutTube video data (if offline hide player)
            if (!SharedData.isOffline)
            {
                _youTubeDataService = new APIYouTubeDataService();
                YouTubeData data = new YouTubeData();
                data = await _youTubeDataService.GetYouTubeData();
                YouTubeVideoURL = data.VideoUrl;
            }
            else
            {
                ShowVideo = Visibility.Hidden;
            }

            // get version data
            _modVersionService = new APIModVersionService();
            await _modVersionService.GetModVersionInfo(SharedData);
        }
        protected virtual void SwitchPage(ApplicationPage page)
        {
            SwitchPageEvent.Invoke(this, page);
        }

    }
}
