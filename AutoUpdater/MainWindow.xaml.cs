using SevenZipExtractor;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

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
            modAppData = Path.Combine(AppData, "RiseofMordor");

            if (!Directory.Exists(modAppData))
            {
                Directory.CreateDirectory(modAppData);
            }

            launcherAppData = Path.Combine(modAppData, "RiseofMordorLauncher");

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

            var launcherDownloadUrl = "http://3ba9.l.time4vps.cloud/launcher/launcher.7z";
            var launcherDownloadFile = Path.GetFileName(launcherDownloadUrl);
            var launcherDownloadPath = Path.Combine(launcherAppData, launcherDownloadFile);

            var launcherExecPath = currentDirectory;
            var launcherExecFile = Path.Combine(launcherExecPath, "TheDawnlessDaysLauncher.exe"); 

            var isNewVersionAvailable = remoteVersion > localVersion;
            var isLauncherInstalled = File.Exists(launcherExecFile);

            if (isNewVersionAvailable || !isLauncherInstalled)
            {
                StatusText.Dispatcher.Invoke(new Action(() => {
                   StatusText.Text = "Update found, downloading...";
                }));

                await DownloadLauncher(launcherDownloadUrl, launcherDownloadPath, StatusText);

                Dispatcher.Invoke(new Action(() => {
                    StatusText.Text = "Update downloaded, extracting...";
                }));

                if (!File.Exists(launcherDownloadPath))
                {
                    MessageBox.Show($"Send this error to devs: {launcherDownloadPath}", "File not found");
                    return;
                }

                KillRunningInstances();

                try
                {
                    var extractPath = Path.Combine(currentDirectory, "temp");

                    using (var archiveFile = new ArchiveFile(launcherDownloadPath))
                    {
                        archiveFile.Extract(extractPath, true);
                    }

                    foreach (var file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
                    {
                        File.SetAttributes(file, FileAttributes.Normal);

                        var destFile = Path.Combine(currentDirectory, Path.GetFileName(file));
                        var skipFile = destFile.Contains("SevenZipExtractor.dll") && File.Exists(destFile);

                        if (!skipFile)
                        {
                            File.Copy(file, destFile, overwrite: true);
                        }

                        try
                        {
                            File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }

                    foreach (var dir in Directory.GetDirectories(extractPath, "*", SearchOption.AllDirectories))
                    {
                        File.SetAttributes(dir, FileAttributes.Normal);
                    }

                    Directory.Delete(extractPath, true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occured while trying to extract launcher. Please forward this to devs: ${ex.Message}", "An error occured");
                }

                File.Delete(launcherDownloadPath);

                if (File.Exists($"{AppData}/RiseofMordor/RiseofMordorLauncher/installed_launcher_version.txt"))
                {
                    File.Delete($"{AppData}/RiseofMordor/RiseofMordorLauncher/installed_launcher_version.txt");
                }

                using (var x = new StreamWriter($"{AppData}/RiseofMordor/RiseofMordorLauncher/installed_launcher_version.txt"))
                {
                    x.Write(remoteVersion.ToString());
                }
            }
            else
            {
                Dispatcher.Invoke(new Action(() => {
                    StatusText.Text = "No updates found!";
                }));
            }

            Process.Start(launcherExecFile);
            Process.GetCurrentProcess().Kill();
        }

        private int GetCurrentVersion()
        {
            HttpClient client = new HttpClient();
            var t = client.GetStringAsync("http://3ba9.l.time4vps.cloud:7218/api/LauncherVersion/current");
            t.Wait();

            return int.Parse(t.Result);
        }

        private Task DownloadLauncher(string downloadUrl, string downloadDestPath, TextBlock textBlock)
        {
            var tcs = new TaskCompletionSource<bool>();

            long downloadedFileSize = 0;
            long remoteFileSize = GetRemoteFileSize(downloadUrl);

            if (File.Exists(downloadDestPath))
            {
                downloadedFileSize = new FileInfo(downloadDestPath).Length;
                var isLauncherFullyDownloaded = downloadedFileSize == remoteFileSize;

                if (isLauncherFullyDownloaded)
                {
                    tcs.SetResult(true);
                    return tcs.Task;
                }
            }

            if (downloadedFileSize >= remoteFileSize)
            {
                File.Delete(downloadDestPath);
                downloadedFileSize = 0;
            }

            Task.Run(() =>
            {
                try
                {
                    var req = (HttpWebRequest)WebRequest.Create(downloadUrl);

                    if (downloadedFileSize > 0)
                    {
                        req.AddRange(downloadedFileSize);
                    }

                    using (var resp = req.GetResponse())
                    using (var stream = resp.GetResponseStream())
                    using (var fs = new FileStream(downloadDestPath, FileMode.Append, FileAccess.Write))
                    {
                        var buffer = new byte[8192];
                        var bytesRead = 0;
                        var totalRead = downloadedFileSize;

                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            fs.Write(buffer, 0, bytesRead);
                            totalRead += bytesRead;

                            int percent = (int)(totalRead * 100 / remoteFileSize);
                            Dispatcher.Invoke(() =>
                            {
                                textBlock.Text = $"Update download progress: {percent}%";
                            });
                        }
                    }

                    Dispatcher.Invoke(() => textBlock.Text = "Update download completed");
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => textBlock.Text = $"Update download failed");
                    MessageBox.Show(ex.Message);
                    tcs.SetException(ex);
                }
            });

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

        private static long GetRemoteFileSize(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "HEAD";

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                return response.ContentLength;
            }
        }

        private void KillRunningInstances()
        {
            var processName = "TheDawnlessDaysLauncher";

            try
            {
                var processes = Process.GetProcessesByName(processName);

                if (processes.Length == 0)
                {
                    Console.WriteLine($"No process named {processName} found.");
                    return;
                }

                foreach (Process proc in processes)
                {
                    Console.WriteLine($"Killing process {proc.ProcessName} (ID: {proc.Id})...");
                    proc.Kill();
                    proc.WaitForExit();
                }

                Console.WriteLine("Done.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Pass this to developers: {ex.Message}", "An error occured");
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }
    }
}
