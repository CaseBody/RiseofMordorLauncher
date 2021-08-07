using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiseofMordorLauncher
{
    class SubmodsViewModel : BaseViewModel
    {
        public IList<SubmodModel> SubmodsList { get; private set; }

        public SubmodsViewModel()
        {
            SubmodsList = new ObservableCollection<SubmodModel>();

            var submod1 = new SubmodModel()
            {
                Name            = "Unit expansion",
                SubmodSteamId   = "0",
                ImagePath       = "Img1",
            };

            var submod2 = new SubmodModel()
            {
                Name            = "Europe Campaign",
                SubmodSteamId   = "1",
                ImagePath       = "Img2",
            };

            var submod3 = new SubmodModel()
            {
                Name            = "Kill the testers",
                SubmodSteamId   = "2",
                ImagePath       = "Img3",
            };

            SubmodsList.Add(submod1);
            SubmodsList.Add(submod2);
            SubmodsList.Add(submod3);
            SubmodsList.Add(submod3);
            SubmodsList.Add(submod3);
            SubmodsList.Add(submod3);
            SubmodsList.Add(submod3);
            SubmodsList.Add(submod3);
            SubmodsList.Add(submod3);
            SubmodsList.Add(submod3);
        }

    }
}
