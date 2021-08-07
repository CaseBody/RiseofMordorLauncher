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
using System.Diagnostics;
using Steamworks;
using RiseofMordorLauncher.Directory.Pages;

namespace RiseofMordorLauncher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            try
            {
            //    SteamAPI.RestartAppIfNecessary((AppId_t)325610);
               //   SteamAPI.Init();
                //    Process Attila = new Process();
                //    Attila.StartInfo.FileName = @"D:\SteamLibrary\steamapps\common\Total War Attila\Attila.exe";
                //    Attila.StartInfo.Arguments = "mod rise_of_mordor_alpha_pack1_main_0.5.0.pack; mod rise_of_mordor_alpha_pack2_models.pack; mod rise_of_mordor_alpha_pack3_buildings.pack; mod rise_of_mordor_alpha_pack4_weather.pack;";
                //     Attila.StartInfo.WorkingDirectory = @"D:\SteamLibrary\steamapps\common\Total War Attila";
                //    Attila.Start();
                //    Attila.WaitForInputIdle();

                this.DataContext = new WindowViewModel();

            }
            catch (System.Exception e)
            {
                // Something went wrong! Steam is closed?
            }
        }
    }
}
