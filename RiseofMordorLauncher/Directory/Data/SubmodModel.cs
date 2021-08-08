using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace RiseofMordorLauncher
{
    public class SubmodModel
    {
        public string ThumbnailPath              { get; set; }
        public string SubmodSteamId              { get; set; }
        public string SubmodName                 { get; set; }
        public string InstallDir                 { get; set; }
        public string SubmodDesc                 { get; set; }
        public bool IsInstalled                  { get; set; }
        public bool IsEnabled                    { get; set; }
        public Visibility EnableButtonVisibility { get; set; }
        public string SubscribeButtonText        { get; set; }
        public string EnableButtonText           { get; set; }
        public Brush EnableButtonBackground      { get; set; }
        public Brush SubscribeButtonBackground   { get; set; }

    }
}
