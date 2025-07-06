using System;
using System.Collections.Generic;

namespace RiseofMordorLauncher
{
    public class ModVersion
    {
        public string DownloadUrlEU { get; set; }
        public string DownloadUrlNA { get; set; }
        public string DownloadUrlOther { get; set; }
        public double LatestVersionNumber { get; set; }  
        public double InstalledVersionNumber { get; set; }
        public string ChangeLog { get; set; }
        public string VersionText { get; set; }
        public List<string> InstalledPackFiles { get; set; }
        public List<string> LatestPackFiles { get; set; }
    }

    public class NewModVersion
    {
        public string download_url_eu { get; set; }
        public string download_url_na { get; set; }
        public string download_url { get; set; }
        public double latest_version_number { get; set; }
        public string change_log { get; set; }
        public string version_text { get; set; }
        public List<string> pack_files { get; set; }
    }
}
