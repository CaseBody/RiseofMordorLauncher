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
        private static string ROM_MODDB_URL = "https://www.moddb.com/mods/total-war-rise-of-mordor";
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
            var imageBoxNode        = htmlDoc.GetElementbyId("imagebox");

            if (imageBoxNode == null || imageBoxNode.ChildNodes.Count <= MODDB_LATEST_IMAGE_NODE_NUM)
            {
                Logger.Log("Failed to parse imagebox!");
                return;
            }

            var latestImageLiNode = imageBoxNode.ChildNodes[MODDB_LATEST_IMAGE_NODE_NUM];
            if (latestImageLiNode == null || latestImageLiNode.ChildNodes.Count < 2)
            {
                Logger.Log("Failed to parse imagebox!");
                return;
            }
            
            var latestImageANode = latestImageLiNode.ChildNodes[1];
            if (latestImageANode == null || latestImageLiNode.ChildNodes.Count <= 0)
            {
                Logger.Log("Failed to parse imagebox!");
                return;
            }
            
            var latestImageImgNode  = latestImageANode.ChildNodes[0];
            var latestImageSrc      = latestImageImgNode.GetAttributeValue("src", "");

            // Get full size image URL from cropped image URL
            latestImageSrc          = latestImageSrc.Replace("cache/", "");         // remove "cache/"
            latestImageSrc          = latestImageSrc.Replace("crop_120x90/", "");   // remove "rop_120x90/"

            if (latestImageSrc != null && latestImageSrc != "")
            {
                PreviewImage.BeginInit();
                PreviewImage.UriSource = new Uri(latestImageSrc, UriKind.Absolute);
                PreviewImage.EndInit();
            }
        }

    }
}
