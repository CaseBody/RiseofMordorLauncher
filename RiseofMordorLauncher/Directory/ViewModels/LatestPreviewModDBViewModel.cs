using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using HtmlAgilityPack;
using RiseofMordorLauncher.Directory.Services;

namespace RiseofMordorLauncher
{
    public class LatestPreviewModDBViewModel : BaseViewModel, ILatestPreview
    {
        private static string ROM_MODDB_URL = "https://www.nexusmods.com/totalwarattila/mods/1?tab=files";
        private static int MODDB_LATEST_IMAGE_NODE_NUM = 7;

        public BitmapImage PreviewImage { get; set; } = new BitmapImage();
        public Visibility ShowPreview { get; set; } = Visibility.Visible;
        
        private ICommand _PreviewCommand;
        public ICommand PreviewCommand
        {
            get
            {
                return _PreviewCommand ?? (_PreviewCommand = new CommandHandler(() => Process.Start("https://discord.com/channels/328911806372511744/739128378669662228"), () => true));
            }
        }

        public LatestPreviewModDBViewModel(SharedData sharedData)
        {
            try
            {
                GetLatestPreview();
            }
            catch (Exception ex)
            {
                Logger.Log(ex.ToString());
            }
        }

        private void GetLatestPreview()
        {
            var web                 = new HtmlWeb();
            var htmlDoc             = web.Load(ROM_MODDB_URL);
            var imageBoxNode        = htmlDoc.GetElementbyId("sidebargallery");

            if (imageBoxNode == null || imageBoxNode.ChildNodes.Count < 9)
            {
                Logger.Log("Failed to parse imagebox!");
                return;
            }

            var latestImageLiNode = imageBoxNode.ChildNodes[9];
            if (latestImageLiNode == null)
            {
                Logger.Log("Failed to parse imagebox!");
                return;
            }
            
            var latestImageANode = latestImageLiNode.ChildNodes[1];
            if (latestImageANode == null)
            {
                Logger.Log("Failed to parse imagebox!");
                return;
            }
            
            var latestImageSrc      = latestImageANode.GetAttributeValue("data-src", "");

            if (latestImageSrc != null && latestImageSrc != "")
            {
                PreviewImage.BeginInit();
                PreviewImage.UriSource = new Uri(latestImageSrc, UriKind.Absolute);
                PreviewImage.EndInit();
            }
        }

    }
}
