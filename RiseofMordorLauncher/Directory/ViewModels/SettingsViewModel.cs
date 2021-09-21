using RiseofMordorLauncher.Directory.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace RiseofMordorLauncher
{
    class SettingsViewModel : BaseViewModel
    {
        public ObservableCollection<String> SubmodLoadOrder { get; set; }
        public bool _AutoInstall = true;
        public bool AutoInstall
        {
            get { return _AutoInstall; }
            set
            {
                _AutoInstall = value;
            }
        }

        private int _SelectedItem = -1;
        public int SelectedItem
        {
            get { return _SelectedItem; }
            set
            {
                _SelectedItem = value;
            }
        }

        UserPreferences prefs;
        private List<SubmodInstallation> EnabledSubmods;
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
            _AutoInstall = prefs.AutoUpdate;

            if (File.Exists($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"))
            {
                EnabledSubmodsRaw = File.ReadAllLines($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt").ToList();
                EnabledSubmods = new List<SubmodInstallation>();

                for (int i = 0; i < EnabledSubmodsRaw.Count; i++)
                {
                    SubmodInstallation installation = new SubmodInstallation();
                    installation = SubmodService.GetSubmodInstallInfo(ulong.Parse(EnabledSubmodsRaw.ElementAt(i)));

                    if (installation.IsInstalled)
                    {
                        EnabledSubmods.Add(installation);
                    }
                    else
                    {
                        DisableSubmod(EnabledSubmodsRaw.ElementAt(i));
                    }
                }

                if (prefs.LoadOrder.Count > 1)
                {
                    for (int i = 0; i < prefs.LoadOrder.Count; i++)
                    {
                        try
                        {
                            if (!EnabledSubmods.Any(s => s.ID == ulong.Parse(prefs.LoadOrder.ElementAt(i))))
                            {
                                prefs.LoadOrder.RemoveAt(i);
                            }
                        }
                        catch
                        {

                        }
                    }
                }

                for (int i = 0; i < EnabledSubmods.Count; i++)
                {
                    if (!(prefs.LoadOrder.Contains(EnabledSubmods.ElementAt(i).ID.ToString())))
                    {
                        prefs.LoadOrder.Add(EnabledSubmods.ElementAt(i).ID.ToString());
                    }
                }

                SubmodLoadOrder = new ObservableCollection<string>();

                for (var i = 0; i < prefs.LoadOrder.Count; i++)
                {
                    ulong id = 0;
                    bool success = ulong.TryParse(prefs.LoadOrder.ElementAt(i), out id);
                    
                    if (success)
                    {
                        var install = SubmodService.GetSubmodInstallInfo(id);
                        prefs.LoadOrder.RemoveAt(i);
                        prefs.LoadOrder.Insert(i, install.FileName);
                    }

                }

                foreach (var item in prefs.LoadOrder)
                {
                    SubmodLoadOrder.Add(item);
                }

                WritePrefs(prefs);
            }

        }

        private void DisableSubmod(string id)
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

        private void WritePrefs(UserPreferences prefs2)
        {
            if (!File.Exists($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/user_preferences.txt"))
                File.CreateText($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/user_preferences.txt");

            if (File.Exists($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt"))
            {

                EnabledSubmodsRaw = File.ReadAllLines($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/enabled_submods.txt").ToList();
                List<SubmodInstallation> EnabledSubmods2 = new List<SubmodInstallation>();

                for (int i = 0; i < EnabledSubmodsRaw.Count; i++)
                {
                    SubmodInstallation installation = new SubmodInstallation();
                    installation = SubmodService.GetSubmodInstallInfo(ulong.Parse(EnabledSubmodsRaw.ElementAt(i)));

                    if (installation.IsInstalled)
                    {
                        EnabledSubmods2.Add(installation);
                    }
                    else
                    {
                        DisableSubmod(EnabledSubmodsRaw.ElementAt(i));
                    }
                }

                for (int i = 0; i < prefs2.LoadOrder.Count; i++)
                {
                    if (!EnabledSubmods2.Any(s => s.FileName == prefs2.LoadOrder.ElementAt(i)) && prefs2.LoadOrder.ElementAt(i) != "rom_base")
                    {
                        prefs2.LoadOrder.RemoveAt(i);
                    }
                    else
                    {
                        foreach (var submod in EnabledSubmods2)
                        {
                            if (submod.FileName == prefs2.LoadOrder.ElementAt(i))
                            {
                                prefs2.LoadOrder.RemoveAt(i);
                                prefs2.LoadOrder.Insert(i, submod.ID.ToString());
                            }
                        }
                    }
                }
            }

            string output = $"auto_update={_AutoInstall}{Environment.NewLine}load_order = {{";
            
            foreach (string pack in prefs2.LoadOrder)
            {
                output = output + Environment.NewLine + pack;
            }

            output = output + Environment.NewLine + "}";

            using (var x = new StreamWriter($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/user_preferences.txt"))
            {
                x.Write(output);
            }
            
        }

        private void MoveUp()
        {
            if (_SelectedItem != -1 && _SelectedItem != 0)
            {
                var index = _SelectedItem;
                string id = prefs.LoadOrder.ElementAt(index);
                string text = SubmodLoadOrder.ElementAt(index);

                SubmodLoadOrder.RemoveAt(index);
                SubmodLoadOrder.Insert(index - 1, text);

                prefs.LoadOrder.RemoveAt(index);
                prefs.LoadOrder.Insert(index - 1, id);

                var prefs2 = prefs;
                for (var i = 0; i < prefs2.LoadOrder.Count; i++)
                {
                    ulong id2 = 0;
                    bool success = ulong.TryParse(prefs.LoadOrder.ElementAt(i), out id2);

                    if (success)
                    {
                        var install = SubmodService.GetSubmodInstallInfo(id2);
                        prefs2.LoadOrder.RemoveAt(i);
                        prefs2.LoadOrder.Insert(i, install.FileName);
                    }

                }

                WritePrefs(prefs2);
            }
        }
        private void MoveDown()
        {
            if (_SelectedItem != -1 && _SelectedItem != SubmodLoadOrder.Count - 1)
            {
                var index = _SelectedItem;
                string text = SubmodLoadOrder.ElementAt(index);
                string id = prefs.LoadOrder.ElementAt(index);

                SubmodLoadOrder.RemoveAt(index);
                SubmodLoadOrder.Insert(index + 1, text);

                prefs.LoadOrder.RemoveAt(index);
                prefs.LoadOrder.Insert(index + 1, id);

                var prefs2 = prefs;
                for (var i = 0; i < prefs2.LoadOrder.Count; i++)
                {
                    ulong id2 = 0;
                    bool success = ulong.TryParse(prefs.LoadOrder.ElementAt(i), out id2);

                    if (success)
                    {
                        var install = SubmodService.GetSubmodInstallInfo(id2);
                        prefs2.LoadOrder.RemoveAt(i);
                        prefs2.LoadOrder.Insert(i, install.FileName);
                    }

                }


                WritePrefs(prefs2);
            }
        }

 
        private ICommand _UpCommand;
        private ICommand _DownCommand;
        public ICommand UpCommand
        {
            get
            {
                return _UpCommand ?? (_UpCommand = new CommandHandler(() => MoveUp(), () => true));
            }
        }

        public ICommand DownCommand
        {
            get
            {
                return _DownCommand ?? (_DownCommand = new CommandHandler(() => MoveDown(), () => true));
            }
        }



    }
}
