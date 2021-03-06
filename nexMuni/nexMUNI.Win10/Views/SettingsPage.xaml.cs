﻿using nexMuni.Common;
using nexMuni.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace nexMuni.Views
{
    public sealed partial class SettingsPage : Page
    {
        private NavigationHelper navigationHelper;
        private ObservableDictionary defaultViewModel = new ObservableDictionary();

        public SettingsPage()
        {
            this.InitializeComponent();

            this.navigationHelper = new NavigationHelper(this);
            this.navigationHelper.LoadState += this.NavigationHelper_LoadState;
            this.navigationHelper.SaveState += this.NavigationHelper_SaveState;

            CountBox.SelectionChanged += ChangeNearbyCount;
            PivotBox.SelectionChanged += ChangePivotSetting;
            TileSwitch.Toggled += TileSwitchToggled;

            if (SettingsHelper.NearbyCount == 15)
            {
                CountBox.SelectedIndex = 0;
            }
            else
            {
                CountBox.SelectedIndex = 1;
            }


            PivotBox.SelectedIndex = SettingsHelper.LaunchPivotIndex;

            if (SettingsHelper.TransparentTile == 1)
            {
                TileSwitch.IsOn = true;
            }
            else
            {
                TileSwitch.IsOn = false;
            }

            RefreshLabel.Text = SettingsHelper.RefreshedDate;
        }

        private void TileSwitchToggled(object sender, RoutedEventArgs e)
        {
            SettingsHelper.TileSwitchToggled(((ToggleSwitch)sender).IsOn);
        }

        private void ChangeNearbyCount(object sender, SelectionChangedEventArgs e)
        {
            SettingsHelper.SetNearbySetting(((ComboBox) sender).SelectedIndex);
        }

        private void ChangePivotSetting(object sender, SelectionChangedEventArgs e)
        {
            SettingsHelper.SetLaunchPivotSetting(((ComboBox) sender).SelectedIndex);
        }

        private async void RefreshData(object sender, RoutedEventArgs e)
        {
            RefreshButton.IsEnabled = false;
            RefreshRing.IsActive = true;
            //var refreshClient = new DataRefreshHelper();
            //await refreshClient.RefreshDataAsync();

            //TODO: Use better error handeling for database refresh
            try
            {
                await DatabaseHelper.MakeMuniDatabaseAsync();
                SettingsHelper.DatabaseRefreshed(true);
            }
            catch
            {
                SettingsHelper.DatabaseRefreshed(false);
            }
            RefreshRing.IsActive = false;
            RefreshButton.IsEnabled = true;

            RefreshLabel.Text = SettingsHelper.RefreshedDate;
        }

        private void NavigationHelper_LoadState(object sender, LoadStateEventArgs e)
        {
        }

        private void NavigationHelper_SaveState(object sender, SaveStateEventArgs e)
        {
        }

        #region NavigationHelper registration

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            this.navigationHelper.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            this.navigationHelper.OnNavigatedFrom(e);
        }

        public NavigationHelper NavigationHelper
        {
            get { return this.navigationHelper; }
        }

        public ObservableDictionary DefaultViewModel
        {
            get { return this.defaultViewModel; }
        }


        #endregion
    }
}
