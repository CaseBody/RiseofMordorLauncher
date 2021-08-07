using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Steamworks;
using SteamWebAPI2;
using SteamWebAPI2.Utilities;
using SteamWebAPI2.Interfaces;

namespace RiseofMordorLauncher
{
    public class APISteamUserService : ISteamUserService
    {
        public async Task<SteamUser> GetSteamUser()
        {
            SteamAPI.Init();

            // Check if steam is running, Shouldn't even get here if its not. But better safe then sorry.

            try
            {
                if (SteamAPI.IsSteamRunning())
                {
                    // Get username and url to avatar and return, Shorten username if too large for ui.

                    SteamUser user = new SteamUser();
                    user.UserName = SteamFriends.GetPersonaName();
                    if (user.UserName.Length > 17)
                    {
                        user.UserName = user.UserName.Substring(0, 15) + "..";
                    }

                    CSteamID id = Steamworks.SteamUser.GetSteamID();
                    var webInterfaceFactory = new SteamWebInterfaceFactory("E86B44EA77A64FABFB3E99091C9CE8D3");
                    var steamInterface = webInterfaceFactory.CreateSteamWebInterface<SteamWebAPI2.Interfaces.SteamUser>(new HttpClient());
                    var playerSummaryResponse = await steamInterface.GetPlayerSummaryAsync(id.m_SteamID);
                    var playerSummaryData = playerSummaryResponse.Data;
                    user.AvatarUrl = playerSummaryData.AvatarFullUrl;

                    return user;


                }
                else
                {
                    // Return dummy data if steam is not running.

                    SteamUser user = new SteamUser();
                    user.UserName = "SteamUserNotFound";
                    user.AvatarUrl = "../Images/example_avatar.jpg";
                    return user;
                }
            } // incase of a an error (most likely network related) return dummy data
            catch
            {
                SteamUser user = new SteamUser();
                user.UserName = "SteamUserNotFound";
                user.AvatarUrl = "../Images/example_avatar.jpg";
                return user;
            }

        }
    }
}
