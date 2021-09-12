using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
            LatestPreviewFrame.DataContext = new LatestPreviewViewModel(sharedData);
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
