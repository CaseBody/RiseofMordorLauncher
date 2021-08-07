using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Newtonsoft.Json.Linq;


namespace RiseofMordorLauncher
{
    public class APIGoogleDriveService : IGoogleDriveService
    {
        public Task DownloadFile(string file_name, string output_path)
        {
            string[] Scopes = { DriveService.Scope.DriveReadonly };
            string ApplicationName = "RiseofMordorLauncher";

            UserCredential credential;
            using (var stream1 =
                new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
            {
                // The file token.json stores the user's access and refresh tokens, and is created
                // automatically when the authorization flow completes for the first time.
                string credPath = "token.json";
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream1).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
            }

            // Create Drive API service.
            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            FilesResource.ListRequest listRequest = service.Files.List();
            listRequest.PageSize = 10;
            listRequest.Fields = "nextPageToken, files(id, name)";

            // List files.
            IList<Google.Apis.Drive.v3.Data.File> files = listRequest.Execute().Files;
            string file_id = "";
            if (files != null && files.Count > 0)
            {
                foreach (var file in files)
                {
                    if (file.Name == file_name)
                    {
                        file_id = file.Id;
                    }
                }
            }

            var request = service.Files.Get(file_id);
            var stream = new MemoryStream();

            // Add a handler which will be notified on progress changes.
            // It will notify on each chunk download and when the
            // download is completed or failed.
            request.MediaDownloader.ProgressChanged += (Google.Apis.Download.IDownloadProgress progress) =>
            {
                switch (progress.Status)
                {
                    case Google.Apis.Download.DownloadStatus.Completed:
                        {
                            using (var file = new FileStream(output_path, FileMode.Create, FileAccess.Write))
                            {
                                stream.WriteTo(file);
                            }
                            break;
                        }
                }
            };

            request.Download(stream);

            return Task.CompletedTask;
        }
    }
}
