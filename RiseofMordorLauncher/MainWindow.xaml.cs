﻿using System;
using System.Windows;
using System.Windows.Input;
using System.Diagnostics;

namespace RiseofMordorLauncher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly WindowViewModel viewModel = new WindowViewModel();

        public MainWindow()
        {
            try
            {
                //SteamAPI.RestartAppIfNecessary((AppId_t)325610);
                //  SteamAPI.Init();
                //    Process Attila = new Process();
                //    Attila.StartInfo.FileName = @"D:\SteamLibrary\steamapps\common\Total War Attila\Attila.exe";
                //    Attila.StartInfo.Arguments = "mod rise_of_mordor_alpha_pack1_main_0.5.0.pack; mod rise_of_mordor_alpha_pack2_models.pack; mod rise_of_mordor_alpha_pack3_buildings.pack; mod rise_of_mordor_alpha_pack4_weather.pack;";
                //     Attila.StartInfo.WorkingDirectory = @"D:\SteamLibrary\steamapps\common\Total War Attila";
                //    Attila.Start();
                //    Attila.WaitForInputIdle();

                DataContext = viewModel;
                viewModel.MinimizeEvent += Minimize;
                viewModel.CloseEvent += Close;

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Minimize(object sender, EventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Close(object sender, EventArgs e)
        {
            Process.GetCurrentProcess().Kill();
        }
    }
}
