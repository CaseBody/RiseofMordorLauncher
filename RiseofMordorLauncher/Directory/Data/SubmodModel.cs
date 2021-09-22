using RiseofMordorLauncher.Directory.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace RiseofMordorLauncher
{
    public class SubmodModel : BaseViewModel, ICloneable
    {
        public string       ThumbnailPath               { get; set; }
        public string       FileName                    { get; set; }
        public string       SubmodSteamId               { get; set; }
        public string       SubmodName                  { get; set; }
        public string       InstallDir                  { get; set; }
        public string       SubmodDesc                  { get; set; }
        public bool         IsInstalled                 { get; set; }
        public bool         IsEnabled                   { get; set; }
        public Visibility   EnableButtonVisibility      { get; set; }
        public string       SubscribeButtonText         { get; set; }
        public string       EnableButtonText            { get; set; }
        public Brush        EnableButtonBackground      { get; set; }
        public Brush        EnableButtonForeground      { get; set; }
        public Brush        SubscribeButtonBackground   { get; set; }
        public Brush        SubscribeButtonForeground   { get; set; }
        public short        UpvoteCount                 { get; set; }
        public short        DownvoteCount               { get; set; }
        public string       SteamId                     { get; set; }
        public Visibility   ProgressBarVisibility       { get; set; }
        public decimal      ProgressBarValue            { get; set; }
        public bool         has_voted                   { get; set; } = false;
        public bool         up_voted                    { get; set; } = false;
        public bool         down_voted                  { get; set; } = false;

        #region Commands
        public event EventHandler VisitSteamPressed;
        public event EventHandler SubscribeButtonPressed;
        public event EventHandler EnableButtonPressed;
        public event EventHandler UpvoteButtonPressed;
        public event EventHandler DownvoteButtonPressed;

        private ICommand _VisitSteamPageCommand;
        public ICommand VisitSteamPageCommand
        {
            get
            {
                return _VisitSteamPageCommand ?? (_VisitSteamPageCommand = new CommandHandler(() => VisitSteamPressed?.Invoke(this, null), () => true));
            }
        }

        private ICommand _SubscribeButtonCommand;
        public ICommand SubscribeButtonCommand
        {
            get
            {
                return _SubscribeButtonCommand ?? (_SubscribeButtonCommand = new CommandHandler(() => SubscribeButtonPressed?.Invoke(this, null), () => true));
            }
        }

        private ICommand _EnableButtonCommand;
        public ICommand EnableButtonCommand
        {
            get
            {
                return _EnableButtonCommand ?? (_EnableButtonCommand = new CommandHandler(() => EnableButtonPressed?.Invoke(this, null), () => true));
            }
        }

        private ICommand _UpvoteButtonCommand;
        public ICommand UpvoteButtonCommand
        {
            get
            {
                return _UpvoteButtonCommand ?? (_UpvoteButtonCommand = new CommandHandler(() => UpvoteButtonPressed?.Invoke(this, null), () => true));
            }
        }

        private ICommand _DownvoteButtonCommand;
        public ICommand DownvoteButtonCommand
        {
            get
            {
                return _DownvoteButtonCommand ?? (_DownvoteButtonCommand = new CommandHandler(() => DownvoteButtonPressed?.Invoke(this, null), () => true));
            }
        }
        #endregion

        public object Clone()
        {
            var copy = new SubmodModel();

            copy.ThumbnailPath               = ThumbnailPath;
            copy.FileName                    = FileName;
            copy.SubmodSteamId               = SubmodSteamId;
            copy.SubmodName                  = SubmodName;
            copy.InstallDir                  = InstallDir;
            copy.SubmodDesc                  = SubmodDesc;
            copy.IsInstalled                 = IsInstalled;
            copy.IsEnabled                   = IsEnabled;
            copy.EnableButtonVisibility      = EnableButtonVisibility;
            copy.SubscribeButtonText         = SubscribeButtonText;
            copy.EnableButtonText            = EnableButtonText;
            copy.EnableButtonBackground      = EnableButtonBackground;
            copy.EnableButtonForeground      = EnableButtonForeground;
            copy.SubscribeButtonBackground   = SubscribeButtonBackground;
            copy.SubscribeButtonForeground   = SubscribeButtonForeground;
            copy.SteamId                     = SteamId;
            copy.ProgressBarVisibility       = ProgressBarVisibility;
            copy.ProgressBarValue            = ProgressBarValue;
            copy.has_voted                   = has_voted;
            copy.up_voted                    = up_voted;
            copy.down_voted                  = down_voted;

            return copy;
        }
    }
}
