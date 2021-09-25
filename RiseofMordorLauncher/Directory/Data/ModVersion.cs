using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiseofMordorLauncher
{
    public class ModVersion
    {
        public long DownloadNumberOfBytes;
        public double LatestVersionNumber { get; set; }  
        public double InstalledVersionNumber { get; set; }
        public string ChangeLog { get; set; }
        public string VersionText { get; set; }
        public List<string> InstalledPackFiles { get; set; }
        public List<string> LatestPackFiles { get; set; }
    }
}
