using System;
using System.Linq;
using System.Threading.Tasks;

namespace RiseofMordorLauncher
{
    public class APILauncherVersionInfo : ILauncherVersionService
    {

        // Return info about installed mod version and latest downloadable version
        public async Task<LauncherVersion> GetLauncherVersionInfo(SharedData sharedData)
        {
            string AppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            IGoogleDriveService drive_service = new APIGoogleDriveService();
            LauncherVersion version = new LauncherVersion();
            // check if online, if not latest downloadable launcher version will be turned as 0 otherwise get latest info.

            if (!sharedData.IsOffline)
            {
                await drive_service.DownloadFile("current_launcher_version.txt", $"{AppDataPath}/RiseofMordor/RiseofMordorLauncher/current_launcher_version.txt", 1);

                string[] current_info = System.IO.File.ReadAllLines($"{AppDataPath}/RiseofMordor/RiseofMordorLauncher/current_launcher_version.txt");
                foreach (string line in current_info)
                {
                    if (line.StartsWith("version="))
                    {
                        version.LatestVersion = double.Parse(line.Split('=').ElementAt(1));
                    }
                    else if (line.StartsWith("high_priority"))
                    {
                        version.IsHighPriority = bool.Parse(line.Split('=').ElementAt(1));
                    }
                    else if (line.StartsWith("download_size"))
                    {
                        version.DownloadNumberOfBytes = long.Parse(line.Split('=').ElementAt(1));
                    }
                }
            }
            else
            {
                version.LatestVersion = 0;
                version.IsHighPriority = false;
            }

            // Check if a local version is currently installed

            if (System.IO.File.Exists($"{AppDataPath}/RiseofMordor/RiseofMordorLauncher/local_version.txt"))
            {
                string[] local_info = System.IO.File.ReadAllLines($"{AppDataPath}/RiseofMordor/RiseofMordorLauncher/local_launcher_version.txt");
                foreach (string line in local_info)
                {
                    if (line.StartsWith("version="))
                    {
                        version.InstalledVersion = double.Parse(line.Split('=').ElementAt(1));
                    }
                }
            }
            else
            {

                version.InstalledVersion = 0;
                
            }

            return version;
        }

    }
}
