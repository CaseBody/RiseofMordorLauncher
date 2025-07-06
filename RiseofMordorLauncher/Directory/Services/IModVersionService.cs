using System;
using System.Threading.Tasks;

namespace RiseofMordorLauncher
{
    interface IModVersionService
    {
        Task<ModVersion> GetModVersionInfo(SharedData sharedData);
    }
}
