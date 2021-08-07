using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiseofMordorLauncher
{
    interface IModVersionService
    {
        Task<ModVersion> GetModVersionInfo(SharedData sharedData);
    }
}
