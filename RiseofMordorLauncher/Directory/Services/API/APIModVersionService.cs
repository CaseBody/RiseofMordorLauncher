using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using Steamworks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System.Windows;

namespace RiseofMordorLauncher
{
    public class APIModVersionService : IModVersionService
    {

        // Return info about installed mod version and latest downloadable version
        public async Task<ModVersion> GetModVersionInfo(SharedData sharedData)
        {
            SteamAPI.Init();
            AppId_t attila_appid = (AppId_t)325610;
            ModVersion version = new ModVersion();

            string AttilaDir = "";
            SteamApps.GetAppInstallDir(attila_appid, out AttilaDir, 10000);
            string AppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            IGoogleDriveService drive_service = new APIGoogleDriveService();

            // check if online, if not latest downloadable mod version will be turned as 0 otherwise get latest info.

            if (!sharedData.isOffline)
            {
                await drive_service.DownloadFile("current_mod_version.txt", $"{AppDataPath}/RiseofMordor/RiseofMordorLauncher/current_mod_version.txt");

                string[] current_info = System.IO.File.ReadAllLines($"{AppDataPath}/RiseofMordor/RiseofMordorLauncher/current_mod_version.txt");
                bool is_reading_pack_files = false;
                version.LatestPackFiles = new List<string>();
                foreach (string line in current_info)
                    {
                        if (is_reading_pack_files)
                        {
                            if (line == "}")
                            {
                                is_reading_pack_files = false;
                            }
                            else
                            {
                                version.LatestPackFiles.Add(line);
                            }
                        }
                        else
                        {
                            if (line.StartsWith("version"))
                            {
                                string raw_version = line.Split('=').ElementAt(1);
                                char[] char_array = raw_version.ToCharArray();
                                string final = "";
                                bool has_hit_first_dot = false;

                                foreach (char charachter in char_array)
                                {
                                    if (charachter == '.')
                                    {
                                        if (has_hit_first_dot)
                                        {

                                        }
                                        else
                                        {
                                            has_hit_first_dot = true;
                                            final = final + charachter;
                                        }
                                    }
                                    else
                                    {
                                        final = final + charachter;
                                    }
                                }
                                version.LatestVersionNumber = double.Parse(final);
                            }
                            else if (line == ("pack_files = {")) { is_reading_pack_files = true; }
                        }
                    }
            }
            else
            {
                version.LatestVersionNumber = 0;
            }

            // Check if a local version is currently installed

            if (System.IO.File.Exists($"{AppDataPath}/RiseofMordor/RiseofMordorLauncher/local_version.txt"))
            {
                string[] local_info = System.IO.File.ReadAllLines($"{AppDataPath}/RiseofMordor/RiseofMordorLauncher/local_version.txt");
                bool is_reading_pack_files = false;
                bool is_reading_changelog = false;
                version.InstalledPackFiles = new List<string>();
                foreach (string line in local_info)
                    {
                        if (is_reading_pack_files)
                        {
                            if (line == "}")
                            {
                                is_reading_pack_files = false;
                            }
                            else
                            {
                                version.InstalledPackFiles.Add(line);
                            }
                        }
                        else if (is_reading_changelog)
                        {
                            if (line == "}")
                            {
                                is_reading_changelog = false;
                            }
                            else
                            {
                                if (version.ChangeLog == "")
                                {
                                    version.ChangeLog = line;
                                }
                                else
                                {
                                    if (line == "")
                                    {
                                        version.ChangeLog = version.ChangeLog + Environment.NewLine;
                                    }
                                    else
                                    {
                                        version.ChangeLog = version.ChangeLog + Environment.NewLine + line;
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (line.StartsWith("version"))
                            {
                                string raw_version = line.Split('=').ElementAt(1);
                                char[] char_array = raw_version.ToCharArray();
                                string final = "";
                                bool has_hit_first_dot = false;

                                foreach (char charachter in char_array)
                                {
                                    if (charachter == '.')
                                    {
                                        if (has_hit_first_dot)
                                        {

                                        }
                                        else
                                        {
                                            has_hit_first_dot = true;
                                            final = final + charachter;
                                        }
                                    }
                                    else
                                    {
                                        final = final + charachter;
                                    }
                                }
                                version.InstalledVersionNumber = double.Parse(final);
                            }
                            else if (line == ("pack_files = {")) { is_reading_pack_files = true; }
                            else if (line == ("change_log = {")) { is_reading_changelog = true; }
                        }
                    }
            }
            else
            {
                version.InstalledVersionNumber = 0;
            }

            // If a local version is installed, ensure all pack files are in present in Data. if not return version as 0 so they will be downloaded.

            if (version.InstalledPackFiles != null)
            {
                if (version.InstalledPackFiles.Count != 0)
                {
                    bool packs_installed = true;

                    foreach (string pack in version.InstalledPackFiles)
                    {
                        if (!(System.IO.File.Exists($"{AttilaDir}/data/{pack}")))
                        {
                            packs_installed = false;
                        }
                    }

                    if (!packs_installed)
                    {
                        version.InstalledVersionNumber = 0;
                    }
                }
                else
                {
                    version.InstalledVersionNumber = 0;
                }
            }

            return version;
        }

    }
}
