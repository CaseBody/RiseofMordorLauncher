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
using System.Net.Http;
using System.Net;

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

            thread.Join();
        }

        private void BackgroundEntryPoint()
        {
            try
            {
                CheckUpdate();
            }
            catch (Exception ex)
            {
                MessageBox.Show("An error occured while updating. You can pass on the following message to the developers: " + Environment.NewLine + ex.Message, "Updater Error");
                StatusText.Text = "Error while updating launcher. Starting local version.";

                Task.Delay(500);

                var launcher = new Process();
                launcher.StartInfo.FileName = $"{Directory.GetCurrentDirectory()}/../RiseofMordorLauncher.exe";
                launcher.Start();

                Process.GetCurrentProcess().Kill();
            }
        }

        private async void CheckUpdate()
        {
            var local = GetLocalVersion();
            var current = await GetCurrentVersion();

            if (current > local || !File.Exists($"{Directory.GetCurrentDirectory()}/../RiseofMordorLauncher.exe"))
            {
                Dispatcher.Invoke(new Action(() => {
                    StatusText.Text = "Update found, downloading now...";
                }));

                DownloadLauncher();

                Dispatcher.Invoke(new Action(() => {
                    StatusText.Text = "Update Downloaded, extracting now...";
                }));
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
                File.CreateText($"{AppData}/RiseofMordor/RiseofMordorLauncher/installed_launcher_version.txt");
                File.WriteAllText($"{AppData}/RiseofMordor/RiseofMordorLauncher/installed_launcher_version.txt", current.ToString());

                Process launcher = new Process();
                launcher.StartInfo.FileName = $"{Directory.GetCurrentDirectory()}/../RiseofMordorLauncher.exe";
                launcher.Start();

                Process.GetCurrentProcess().Kill();
            }
            else
            {
                Dispatcher.Invoke(new Action(() => {
                    StatusText.Text = "No updates found!";
                })); 
                Process launcher = new Process();
                launcher.StartInfo.FileName = $"{Directory.GetCurrentDirectory()}/../RiseofMordorLauncher.exe";
                launcher.Start();
                Process.GetCurrentProcess().Kill();
            }
        }

        private async Task<int> GetCurrentVersion()
        {
            HttpClient client = new HttpClient();
            return int.Parse(await client.GetStringAsync("http://3ba9.l.time4vps.cloud:7218/api/LauncherVersion/current"));
        }

        private void DownloadLauncher()
        {
            var client = new WebClient();
            client.DownloadFile("http://3ba9.l.time4vps.cloud/launcher/launcher.rar", $"{Directory.GetCurrentDirectory()}/../launcher.rar");
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
