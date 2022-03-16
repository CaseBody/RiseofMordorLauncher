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
using System.Windows.Threading;

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
        }

        private void BackgroundEntryPoint()
        {
            try
            {
                CheckUpdate();
            }
            catch (Exception ex)
            {
                MessageBox.Show("An error occured while updating. You can pass on the following message to the developers: " + Environment.NewLine + ex, "Updater Error");
                Dispatcher.Invoke(new Action(() => {
                    StatusText.Text = "Error while updating launcher. Starting local version.";
                }));

                Task.Delay(500);

                var launcher = new Process();
                launcher.StartInfo.FileName = $"{Directory.GetCurrentDirectory()}/../TheDawnlessDaysLauncher.exe";
                launcher.StartInfo.WorkingDirectory = $"{Directory.GetCurrentDirectory()}/../";
                launcher.Start();

                Process.GetCurrentProcess().Kill();
            }
        }

        private void CheckUpdate()
        {
            var local = GetLocalVersion();
            var current = GetCurrentVersion();

            if (current > local || !File.Exists($"{Directory.GetCurrentDirectory()}/../TheDawnlessDaysLauncher.exe"))
            {
                StatusText.Dispatcher.Invoke(new Action(() => {
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

                if (File.Exists($"{AppData}/RiseofMordor/RiseofMordorLauncher/installed_launcher_version.txt"))
                    File.Delete($"{AppData}/RiseofMordor/RiseofMordorLauncher/installed_launcher_version.txt");

                using (var x = new StreamWriter($"{AppData}/RiseofMordor/RiseofMordorLauncher/installed_launcher_version.txt"))
                {
                    x.Write(current.ToString());
                }


                Process launcher = new Process();
                launcher.StartInfo.FileName = $"{Directory.GetCurrentDirectory()}/../TheDawnlessDaysLauncher.exe";
                launcher.StartInfo.WorkingDirectory = $"{Directory.GetCurrentDirectory()}/../";
                launcher.Start();

                Process.GetCurrentProcess().Kill();
            }
            else
            {
                Dispatcher.Invoke(new Action(() => {
                    StatusText.Text = "No updates found!";
                })); 
                Process launcher = new Process();
                launcher.StartInfo.FileName = $"{Directory.GetCurrentDirectory()}/../TheDawnlessDaysLauncher.exe";
                launcher.StartInfo.WorkingDirectory = $"{Directory.GetCurrentDirectory()}/../";
                launcher.Start();
                Process.GetCurrentProcess().Kill();
            }
        }

        private int GetCurrentVersion()
        {
            HttpClient client = new HttpClient();
            var t = client.GetStringAsync("http://3ba9.l.time4vps.cloud:7218/api/LauncherVersion/current");
            t.Wait();

            return int.Parse(t.Result);
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
                try
                {
                    return int.Parse(File.ReadAllText($"{AppData}/RiseofMordor/RiseofMordorLauncher/installed_launcher_version.txt"));
                }
                catch { return 0; }
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
