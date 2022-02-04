using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;


namespace RiseofMordorLauncher
{
    public class APIModdbDownloadService : IModdbDownloadService
    {

        public event EventHandler<int> DownloadUpdate;
        public void DownloadFile(string download_page_url, string output_path)
        {
            var web = new HtmlWeb();
            var htmlDoc = web.Load(download_page_url);
            var mirror_link_element = htmlDoc.GetElementbyId("downloadmirrorstoggle");

            htmlDoc = web.Load("https://moddb.com/" + mirror_link_element.Attributes["href"].Value);
            var download_url = "https://moddb.com/" + htmlDoc.DocumentNode.SelectSingleNode("html//body/p/a").Attributes["href"].Value;

            using (var client = new WebClient())
            {
                client.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) =>
                {
                    DownloadUpdate?.Invoke(this, e.ProgressPercentage);
                };

                client.DownloadFileCompleted += (object sender, AsyncCompletedEventArgs e) =>
                {
                    DownloadUpdate?.Invoke(this, 105);
                };

                client.DownloadFileAsync(new System.Uri(download_url), output_path);
            }         
        }
    }
}
