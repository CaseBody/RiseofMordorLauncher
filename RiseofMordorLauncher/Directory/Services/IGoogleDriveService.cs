using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiseofMordorLauncher
{
    interface IGoogleDriveService
    {
        Task DownloadFile(string file_name, string output_path);
    }
}
