using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System.Threading;
using System.Diagnostics;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace AutoUpdater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        public MainWindow()
        {
            InitializeComponent();
            Thread thread = new Thread(BackgroundEntryPoint);
            thread.IsBackground = true;

            thread.Start();

            Thread.CurrentThread.Join();
        }

        private void BackgroundEntryPoint()
        {
            try
            {
                CheckUpdate();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
                StatusText.Text = "Error while updating launcher. Starting local version.";

                Task.Delay(500);

                var launcher = new Process();
                launcher.StartInfo.FileName = $"{Directory.GetCurrentDirectory()}/../RiseofMordorLauncher.exe";
                launcher.Start();

                Process.GetCurrentProcess().Kill();
            }
        }

        private void CheckUpdate()
        {
            var local = GetLocalVersion();
            var current = GetCurrentVersion();

            if (current > local)
            {
                Dispatcher.Invoke(new Action(() => {
                    StatusText.Text = "Update found, downloading now...";
                }));
                Task.Delay(200);

                DownloadFile("launcher.rar", $"{Directory.GetCurrentDirectory()}/../launcher.rar");

                Dispatcher.Invoke(new Action(() => {
                    StatusText.Text = "Update Downloaded, extracting now...";
                }));
                Task.Delay(200);
                using (var archive = RarArchive.Open($"{Directory.GetCurrentDirectory()}/../launcher.rar"))
                {
                    foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                    {
                        entry.WriteToDirectory($"{Directory.GetCurrentDirectory()}/../", new ExtractionOptions()
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        });
                    }
                }

                File.Delete($"{AppData}/RiseofMordor/RiseofMordorLauncher/installed_launcher_version.txt");
                File.Copy($"{AppData}/RiseofMordor/RiseofMordorLauncher/current_launcher_version.txt", $"{AppData}/RiseofMordor/RiseofMordorLauncher/installed_launcher_version.txt");

                Process launcher = new Process();
                launcher.StartInfo.FileName = $"{Directory.GetCurrentDirectory()}/../RiseofMordorLauncher.exe";
                launcher.Start();

                Process.GetCurrentProcess().Kill();
            }
            else
            {
                Dispatcher.Invoke(new Action(() => {
                    StatusText.Text = "No updates found!";
                })); Task.Delay(200);
                Process launcher = new Process();
                launcher.StartInfo.FileName = $"{Directory.GetCurrentDirectory()}/../RiseofMordorLauncher.exe";
                launcher.Start();
                Process.GetCurrentProcess().Kill();
            }
        }

        private int GetCurrentVersion()
        {
            DownloadFile("current_launcher_version.txt", $"{AppData}/RiseofMordor/RiseofMordorLauncher/current_launcher_version.txt");
            return int.Parse(File.ReadAllText($"{AppData}/RiseofMordor/RiseofMordorLauncher/current_launcher_version.txt"));
        }

        private Task DownloadFile(string file_name, string output_path)
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
            listRequest.PageSize = 50;
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
            using (var file_stream = new FileStream(output_path, FileMode.Create, FileAccess.Write))
            {
                request.MediaDownloader.ProgressChanged += (Google.Apis.Download.IDownloadProgress progress) =>
                {
                    switch (progress.Status)
                    {
                        case Google.Apis.Download.DownloadStatus.Completed:
                            {
                                //  using (var file = new FileStream(output_path, FileMode.Create, FileAccess.Write))
                                // {
                                //    stream.WriteTo(file);
                                // }
                                break;
                            }
                        case Google.Apis.Download.DownloadStatus.Downloading:
                            {
                                break;
                            }
                        case Google.Apis.Download.DownloadStatus.Failed:
                            {
                                request.Download(file_stream);
                                Console.WriteLine("Download failed.");
                                break;
                            }
                    }
                };

                request.Download(file_stream);
            }

            // Add a handler which will be notified on progress changes.
            // It will notify on each chunk download and when the
            // download is completed or failed.


            return Task.CompletedTask;
        }

        private int GetLocalVersion()
        {
            if (File.Exists($"{AppData}/RiseofMordor/RiseofMordorLauncher/installed_launcher_version.txt"))
            {
                return int.Parse(File.ReadAllText($"{AppData}/RiseofMordor/RiseofMordorLauncher/installed_launcher_version.txt"));
            }
            else
            {
                return 0;
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}
