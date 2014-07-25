﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Linq;
using Windows.Devices.Geolocation;
using Windows.Data;
using System.Xml;
using Windows.Data.Xml;
using Windows.Storage;
using System.IO;

namespace nexMuni
{
    class MainPageModel
    {
        public static ObservableCollection<StopData> nearbyStops;
        public static ObservableCollection<StopData> favoritesStops;
        public static bool IsDataLoaded { get; set; }

        public static void LoadData()
        {
            
            nearbyStops = new ObservableCollection<StopData>();
            favoritesStops = new ObservableCollection<StopData>();

            DatabaseHelper.LoadFavorites();
            LocationHelper.UpdateNearbyList();
            IsDataLoaded = true; 
        }
    }
}

    