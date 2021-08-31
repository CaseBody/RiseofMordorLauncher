﻿using System;
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
                        else if (line == "{")
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

                return pref;

            }
            else
            {
                File.Create($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/user_preferences.txt");
                File.WriteAllText($"{sharedData.AppData}/RiseofMordor/RiseofMordorLauncher/user_preferences.txt", $"auto_update=true{Environment.NewLine}load_order = {{{Environment.NewLine}rom_base{Environment.NewLine}}}");
                pref.AutoUpdate = true;
                pref.LoadOrder.Add("rom_base");
                return pref;
            }
        }
    }
}