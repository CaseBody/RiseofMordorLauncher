using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace RiseofMordorLauncher
{
    public interface ILatestPreview
    {
        BitmapImage PreviewImage { get; set; }
        Visibility ShowPreview { get; set; }
        ICommand PreviewCommand { get; }
    }
}
