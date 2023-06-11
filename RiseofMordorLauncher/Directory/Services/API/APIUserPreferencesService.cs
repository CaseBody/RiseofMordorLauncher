using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiseofMordorLauncher
{
    public class APIUserPreferencesService : IUserPreferencesService
    {
        public UserPreferences GetUserPreferences(SharedData sharedData)
        {
            UserPreferences pref = new UserPreferences();

            if (File.Exists($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/user_preferences.txt"))
            {
                string[] lines = File.ReadAllLines($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/user_preferences.txt");
                List<string> Packs = new List<string>();
                pref.LoadOrder = new List<string>();

                bool is_reading_packs = false;
                foreach (string line in lines)
                {
                    if (is_reading_packs)
                    {
                        if (line == "}")
                        {
                            is_reading_packs = false;
                        }
                        else
                        {
                            Packs.Add(line);
                        }
                    }
                    else
                    {
                        if (line.StartsWith("auto_update"))
                        {
                            pref.AutoUpdate = bool.Parse(line.Split('=').ElementAt(1));
                        }
                        else if (line.StartsWith("background"))
                        {
                            pref.BackgroundImage = line.Split('=').ElementAt(1);
                        }
                        else if (line.StartsWith("show_latest_video"))
                        {
                            pref.ShowLatestVideo = bool.Parse(line.Split('=').ElementAt(1));
                        }
                        else if (line.StartsWith("show_latest_preview"))
                        {
                            pref.ShowLatestPreview = bool.Parse(line.Split('=').ElementAt(1));
                        }
                        else if (line == "load_order = {")
                        {
                            is_reading_packs = true;
                        }
                    }
                }

                if (Packs.Count == 0)
                {
                    pref.AutoUpdate = true;
                    pref.LoadOrder.Add("rom_base");
                }
                else
                {
                    pref.LoadOrder = Packs;
                }

                if (pref.BackgroundImage == null)
                {
                    pref.BackgroundImage = "unknown.png";
                }

                if (pref.ShowLatestVideo == null)
                {
                    pref.ShowLatestVideo = true;
                }

                if (pref.ShowLatestPreview == null)
                {
                    pref.ShowLatestPreview = true;
                }

                return pref;
            }
            else
            {
                using (var x = new StreamWriter($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/user_preferences.txt"))
                {
                    x.Write($"auto_update=true{Environment.NewLine}show_latest_preview=true{Environment.NewLine}show_latest_video=true{Environment.NewLine}background=unknown.png{Environment.NewLine}load_order = {{{Environment.NewLine}rom_base{Environment.NewLine}}}");
                }
                pref.AutoUpdate = true;
                pref.BackgroundImage = "unknown.png";
                pref.ShowLatestPreview = true;
                pref.ShowLatestVideo = true;
                pref.LoadOrder = new List<string>();
                pref.LoadOrder.Add("rom_base");
                return pref;
            }
        }
    }
}
