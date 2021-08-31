using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiseofMordorLauncher
{
    class SettingsViewModel : BaseViewModel
    {
        public ObservableCollection<String> SubmodLoadOrder { get; set; }
        public bool AutoInstall { get; set; }
        UserPreferences prefs;
        private List<String> EnabledSubmods;
        private List<String> EnabledSubmodsRaw;
        ISteamSubmodsService SubmodService;
        IUserPreferencesService UserPreferencesService;
        SharedData sharedData;
        public SettingsViewModel(SharedData sharedDataInput)
        {
            sharedData = sharedDataInput;

            UserPreferencesService = new APIUserPreferencesService();
            SubmodService = new APISteamSubmodService();

            prefs = UserPreferencesService.GetUserPreferences(sharedData);
            AutoInstall = prefs.AutoUpdate;

            if (File.Exists($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"))
            {
                EnabledSubmodsRaw = File.ReadAllLines($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt").ToList();
                EnabledSubmods = new List<string>();

                for (int i = 0; i < EnabledSubmodsRaw.Count; i++)
                {
                    SubmodInstallation installation = new SubmodInstallation();
                    installation = SubmodService.GetSubmodInstallInfo(ulong.Parse(EnabledSubmodsRaw.ElementAt(i)));

                    if (installation.IsInstalled)
                    {
                        EnabledSubmods.Add(installation.FileName);
                    }
                    else
                    {
                        DisableSubmod(EnabledSubmodsRaw.ElementAt(i));
                    }
                }

                if (prefs.LoadOrder.Count > 1)
                {
                    for (int i = 0; i <= prefs.LoadOrder.Count; i++)
                    {
                        if (!(EnabledSubmods.Contains(prefs.LoadOrder.ElementAt(i))))
                        {
                            prefs.LoadOrder.RemoveAt(i);
                        }
                    }
                }

                for (int i = 0; i < EnabledSubmods.Count; i++)
                {
                    if (!(prefs.LoadOrder.Contains(EnabledSubmods.ElementAt(i))))
                    {
                        prefs.LoadOrder.Add(EnabledSubmods.ElementAt(i));
                    }
                }

                SubmodLoadOrder = new ObservableCollection<string>();
                foreach (var item in prefs.LoadOrder)
                {
                    SubmodLoadOrder.Add(item);
                }
            }
        }

        private async void DisableSubmod(string id)
        {
            string output = "";

            if (!File.Exists($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"))
            {
                return;
            }

            string[] lines = File.ReadAllLines($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt");
            if (lines.Count() == 0)
            {
                try { File.Delete($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"); } catch { }
                return;
            }
            else
            {
                if (lines.Contains(id))
                {
                    if (lines.ToString() == id)
                    {
                        try { File.Delete($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"); } catch { }
                        return;
                    }
                    else
                    {
                        foreach (var line in lines)
                        {
                            if (output == "")
                            {
                                if (line == id)
                                {

                                }
                                else
                                {
                                    output = line;
                                }
                            }
                            else
                            {
                                if (line == id)
                                {

                                }
                                else
                                {
                                    output = output + Environment.NewLine + line;
                                }
                            }
                        }
                    }
                }
                else
                {
                    output = string.Join(Environment.NewLine, lines);
                }
            }

            using (StreamWriter writer = new StreamWriter($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"))
            {
                writer.Write(output);
            }
        }

    }
}
