using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using DiscordRPC;

namespace RiseofMordorLauncher
{
    public class SharedData
    {
        public static Brush NiceGreen = (SolidColorBrush)new BrushConverter().ConvertFrom("#0ba302");

        public bool IsOffline { get; set; } = true;
        public string AttilaDir { get; set; }
        public string AttilaWorkshopDir { get; set; }
        public string AppData { get; set; }
        public DiscordRpcClient RPCClient { get; set; }
        public double LauncherVersion { get; private set; } = 0.1;
    }
}
