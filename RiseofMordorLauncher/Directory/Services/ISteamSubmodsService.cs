using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiseofMordorLauncher
{
    interface ISteamSubmodsService
    {
        void GetSubmods(SharedData sharedData);

        SubmodInstallation GetSubmodInstallInfo(ulong id);

        event EventHandler<List<SubmodModel>> SubmodDataFinishedEvent;
    }
}
