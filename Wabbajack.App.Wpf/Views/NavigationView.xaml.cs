﻿using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using Wabbajack.Common;
using Wabbajack.Messages;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for NavigationView.xaml
    /// </summary>
    public partial class NavigationView : ReactiveUserControl<NavigationVM>
    {
        public Dictionary<ScreenType, Button> ScreenButtonDictionary { get; set; }
        public NavigationView()
        {
            InitializeComponent();
            ScreenButtonDictionary = new() {
                { ScreenType.Home, HomeButton },
                { ScreenType.ModListGallery, BrowseButton },
                { ScreenType.CreateModList, CompileButton },
                { ScreenType.Settings, SettingsButton },
            };
            this.WhenActivated(dispose =>
            {
                this.BindCommand(ViewModel, vm => vm.BrowseCommand, v => v.BrowseButton)
                    .DisposeWith(dispose);
                this.BindCommand(ViewModel, vm => vm.HomeCommand, v => v.HomeButton)
                    .DisposeWith(dispose);
                this.BindCommand(ViewModel, vm => vm.CreateModListCommand, v => v.CompileButton)
                    .DisposeWith(dispose);
                this.BindCommand(ViewModel, vm => vm.SettingsCommand, v => v.SettingsButton)
                    .DisposeWith(dispose);

                this.WhenAny(x => x.ViewModel.Version)
                    .Select(version => $"v{version}")
                    .BindToStrict(this, v => v.VersionTextBlock.Text)
                    .DisposeWith(dispose);


                this.WhenAny(x => x.ViewModel.ActiveScreen)
                    .Subscribe(x => SetButtonActive(x))
                    .DisposeWith(dispose);

                /*
                this.WhenAny(x => x.ViewModel.InstallCommand)
                    .BindToStrict(this, x => x.InstallButton.Command)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.CompileCommand)
                    .BindToStrict(this, x => x.CompileButton.Command)
                    .DisposeWith(dispose);
                */
            });
        }

        private void SetButtonActive(ScreenType activeScreen)
        {
            var activeButtonStyle = (Style)Application.Current.Resources["ActiveNavButtonStyle"];
            var mainButtonStyle = (Style)Application.Current.Resources["MainNavButtonStyle"];
            foreach(var (screen, button) in ScreenButtonDictionary)
            {
                if (screen == activeScreen)
                    button.Style = activeButtonStyle;
                else
                    button.Style = mainButtonStyle;
            }
        }
    }
}