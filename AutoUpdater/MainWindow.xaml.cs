using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Net.Http;
using System.Net;
using System.Windows.Threading;
using SevenZipExtractor;

namespace AutoUpdater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        private string modAppData = null;
        private string launcherAppData = null;

        public MainWindow()
        {
            InitializeComponent();
            InitAppData();
            CheckInstallLauncher();
        }

        private void InitAppData()
        {
            modAppData = System.IO.Path.Combine(AppData, "RiseofMordor");

            if (!Directory.Exists(modAppData))
            {
                Directory.CreateDirectory(modAppData);
            }

            launcherAppData = System.IO.Path.Combine(modAppData, "RiseofMordorLauncher");

            if (!Directory.Exists(launcherAppData))
            {
                Directory.CreateDirectory(launcherAppData);
            }
        }

        private void CheckInstallLauncher()
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

        private async void CheckUpdate()
        {
            var localVersion = GetLocalVersion();
            var remoteVersion = GetCurrentVersion();

            var currentDirectory = Directory.GetCurrentDirectory();
            var launcherDownloadPath = Path.Combine(launcherAppData, "launcher.7z");
            var launcherExecPath = Path.Combine(currentDirectory, "..TheDawnlessDaysLauncher.exe");

            if (remoteVersion > localVersion || !File.Exists(launcherExecPath))
            {
                StatusText.Dispatcher.Invoke(new Action(() => {
                   StatusText.Text = "Update found, downloading now...";
                }));

                if (!File.Exists(launcherDownloadPath))
                {
                    await DownloadLauncher(launcherDownloadPath, StatusText);
                }

                Dispatcher.Invoke(new Action(() => {
                    StatusText.Text = "Update Downloaded, extracting now...";
                }));

                using (var archiveFile = new ArchiveFile(launcherDownloadPath))
                {
                    var extractPath = $"{currentDirectory}/../";
                    foreach (var entry in archiveFile.Entries)
                    {
                        entry.Extract(Path.Combine(extractPath, entry.FileName));
                    }
                }

                if (File.Exists($"{AppData}/RiseofMordor/RiseofMordorLauncher/installed_launcher_version.txt"))
                {
                    File.Delete($"{AppData}/RiseofMordor/RiseofMordorLauncher/installed_launcher_version.txt");
                }

                using (var x = new StreamWriter($"{AppData}/RiseofMordor/RiseofMordorLauncher/installed_launcher_version.txt"))
                {
                    x.Write(remoteVersion.ToString());
                }

                var launcher = new Process();
                launcher.StartInfo.FileName = launcherExecPath; 
                launcher.StartInfo.WorkingDirectory = $"{currentDirectory}/../";
                launcher.Start();

                Process.GetCurrentProcess().Kill();
            }
            else
            {
                Dispatcher.Invoke(new Action(() => {
                    StatusText.Text = "No updates found!";
                })); 

                var launcher = new Process();
                launcher.StartInfo.FileName = launcherExecPath;
                launcher.StartInfo.WorkingDirectory = $"{currentDirectory}/../";
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

        private Task DownloadLauncher(string downloadDestPath, TextBlock textBlock)
        {
            var tcs = new TaskCompletionSource<bool>();
            var client = new WebClient();

            client.DownloadProgressChanged += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    textBlock.Text = $"Update found, download progress: {e.ProgressPercentage}%";
                });
            };

            client.DownloadFileCompleted += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    textBlock.Text = "Update found, download completed";
                    tcs.SetResult(true);
                });
            };

            var url = "http://3ba9.l.time4vps.cloud/launcher/launcher.7z";
            client.DownloadFileAsync(new Uri(url), downloadDestPath);

            return tcs.Task;
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
