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
using System.Globalization;

namespace RiseofMordorLauncher
{
    public class APIModVersionService : IModVersionService
    {
        // Return info about installed mod version and latest downloadable version
        public async Task<ModVersion> GetModVersionInfo(SharedData sharedData)
        {
            Logger.Log("Getting mod version info...");

            var culture = new CultureInfo("en");
            var version = new ModVersion();
            var AppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var drive_service = new APIGoogleDriveService();

            // check if online, if not latest downloadable mod version will be turned as 0 otherwise get latest info.
            if (!sharedData.IsOffline)
            {
                Logger.Log("Downloading current_mod_version.txt...");

                await drive_service.DownloadFile("current_mod_version.txt", $"{AppDataPath}/RiseofMordor/RiseofMordorLauncher/current_mod_version.txt", 1);

                try
                {
                    var current_info = System.IO.File.ReadAllLines($"{AppDataPath}/RiseofMordor/RiseofMordorLauncher/current_mod_version.txt");
                    var is_reading_pack_files = false;
                    version.LatestPackFiles = new List<string>();

                    Logger.Log("Parsing current_mod_version.txt...");
                    foreach (var line in current_info)
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
                                var raw_version = line.Split('=').ElementAt(1);
                                version.VersionText = raw_version;
                                var char_array = raw_version.ToCharArray();
                                var final = "";
                                var has_hit_first_dot = false;

                                foreach (char charachter in char_array)
                                {
                                    if (charachter == '.')
                                    {
                                        if (has_hit_first_dot == false)
                                        {
                                            has_hit_first_dot = true;
                                            final += charachter;
                                        }
                                    }
                                    else
                                    {
                                        final += charachter;
                                    }
                                }

                                version.LatestVersionNumber = double.Parse(final, culture);
                            }
                            else if (line.StartsWith("url"))
                            {
                                version.ModdbDownloadPageUrl = line.Split('=').ElementAt(1);
                            }
                            else if (line == "pack_files = {")
                            {
                                is_reading_pack_files = true;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex.Message);
                }
            }
            else
            {
                version.LatestVersionNumber = 0;
            }

            Logger.Log("Checking if local_version.txt is installed...");
            if (System.IO.File.Exists($"{AppDataPath}/RiseofMordor/RiseofMordorLauncher/local_version.txt"))
            {
                var local_info = System.IO.File.ReadAllLines($"{AppDataPath}/RiseofMordor/RiseofMordorLauncher/local_version.txt");
                var is_reading_pack_files = false;
                var is_reading_changelog = false;
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

                            version.InstalledVersionNumber = double.Parse(final, culture);
                        }
                        else if (line == ("pack_files = {")) { is_reading_pack_files = true; }
                        else if (line == ("change_log = {")) { is_reading_changelog = true; }
                    }
                }
            }
            else
            {
                if (version.LatestVersionNumber != 0 && !sharedData.IsOffline)
                {
                    if (version.LatestPackFiles.Count != 0)
                    {
                        bool packs_installed = true;

                        foreach (string pack in version.LatestPackFiles)
                        {
                            if (!System.IO.File.Exists($"{sharedData.AttilaDir}/data/{pack}"))
                            {
                                packs_installed = false;
                            }
                        }

                        if (!packs_installed)
                        {
                            version.InstalledVersionNumber = 0;
                        }
                        else
                        {
                            version.InstalledVersionNumber = version.LatestVersionNumber;
                            try { System.IO.File.Delete($"{sharedData.AppData}/RiseofMordor/RiseofMororLauncher/enabled_submods.txt"); } catch { }
                            try { System.IO.File.Delete($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/local_version.txt"); } catch { }
                            System.IO.File.Copy($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/current_mod_version.txt", $"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/local_version.txt");
                        }
                    }
                    else
                    {
                        version.InstalledVersionNumber = 0;
                    }
                }
                else
                {
                    version.InstalledVersionNumber = 0;
                }
            }

            Logger.Log("Ensuring all pack files are in present in Data...");
            if (version.InstalledPackFiles != null)
            {
                if (version.InstalledPackFiles.Count != 0)
                {
                    bool packs_installed = true;

                    foreach (string pack in version.InstalledPackFiles)
                    {
                        if (!System.IO.File.Exists($"{sharedData.AttilaDir}/data/{pack}"))
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
