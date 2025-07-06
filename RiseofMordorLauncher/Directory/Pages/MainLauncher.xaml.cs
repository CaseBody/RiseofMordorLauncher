using System;
using System.Windows.Controls;
using System.Windows.Input;

namespace RiseofMordorLauncher.Directory.Pages
{
    /// <summary>
    /// Interaction logic for MainLauncher.xaml
    /// </summary>
    public partial class MainLauncher : Page
    {
        public MainLauncher(SharedData sharedData)
        {
            InitializeComponent();
        }

        //private void Expander_MouseEnter(object sender, MouseEventArgs e)
        //{
        //    if (sender is Expander exp)
        //    {
        //        exp.IsExpanded = true;
        //    }
        //}

        //private void Expander_MouseLeave(object sender, MouseEventArgs e)
        //{
        //    if (sender is Expander exp)
        //    {
        //        exp.IsExpanded = false;
        //    }
        //}
    }
}
