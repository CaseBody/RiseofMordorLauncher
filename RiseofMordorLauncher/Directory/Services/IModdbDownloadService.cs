using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiseofMordorLauncher
{
    interface IModdbDownloadService
    {
        void DownloadFile(string download_page_url, string output_path);
        event EventHandler<int> DownloadUpdate;
    }
}
