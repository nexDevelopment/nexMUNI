﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Windows.Devices.Geolocation;
using Windows.Storage;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.UI.Xaml.Controls.Maps;
using Windows.Storage.Streams;
using System.Threading.Tasks;

namespace nexMuni
{
    class LocationHelper
    {
        private static double PhoneLat { get; set; }
        private static double PhoneLong { get; set; }
        public static Geoposition PhoneLocation { get; set; }

        public static async Task UpdateNearbyList()
        {
#if WINDOWS_PHONE_APP
            var systemTray = Windows.UI.ViewManagement.StatusBar.GetForCurrentView();
            systemTray.ProgressIndicator.Text = "Getting Location";
            systemTray.ProgressIndicator.ProgressValue = null;
#endif

            Geolocator geolocator = new Geolocator();
            geolocator.DesiredAccuracyInMeters = 50;

            MapIcon icon = new MapIcon();
            icon.Image = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/Location.png"));
            var position = await geolocator.GetGeopositionAsync(maximumAge: TimeSpan.FromSeconds(5), timeout: TimeSpan.FromSeconds(30));
            PhoneLocation = position;
            icon.Location = position.Coordinate.Point;
            icon.ZIndex = 99;
            MainPage.searchMap.MapElements.Clear();
            MainPage.searchMap.MapElements.Add(icon);
            //await MainPage.searchMap.TrySetViewAsync(position.Coordinate.Point, 15);

#if WINDOWS_PHONE_APP
            systemTray.ProgressIndicator.Text = "Locating Stops";
#endif
            //Find nearby stops in the database
             await DatabaseHelper.QueryForNearby(position.Coordinate.Point, 0.5);

#if WINDOWS_PHONE_APP           
            systemTray.ProgressIndicator.ProgressValue = 0;
            systemTray.ProgressIndicator.Text = "nexMuni";
#endif
        }

        public static double[][] MakeBounds(Geopoint location, double dist)
        {
            PhoneLat = location.Position.Latitude;
            PhoneLong = location.Position.Longitude;

            //Create search radius bounds
           return new double[][] { Destination(PhoneLat, PhoneLong, 0.0, dist),
                                                Destination(PhoneLat, PhoneLong, 90.0, dist),
                                                Destination(PhoneLat, PhoneLong, 180.0, dist),
                                                Destination(PhoneLat, PhoneLong, 270.0, dist)};
        }

        private static double[] Destination(double lat, double lon, double bearing, double d)
        {
            double rLat = Deg2Rad(lat);
            double rLon = Deg2Rad(lon);
            double rBearing = Deg2Rad(bearing);
            double rDist = d / 3963.19;

            double rLatBound = Math.Asin(Math.Sin(rLat) * Math.Cos(rDist) + Math.Cos(rLat) * Math.Sin(rDist) * Math.Cos(rBearing));
            double rLonBound = rLon + Math.Atan2(Math.Sin(rBearing) * Math.Sin(rDist) * Math.Cos(rLat),
                                                 Math.Cos(rDist) - (Math.Sin(rLat) * Math.Sin(rLatBound)));

            double[] LatLon = new double[] { Rad2Deg(rLatBound), Rad2Deg(rLonBound) };
            return LatLon;
        }

        private static double Deg2Rad(double degrees)
        {
            return (Math.PI / 180) * degrees;
        }

        private static double Rad2Deg(double radians)
        {
            return (180 / Math.PI) * radians;
        }

        public static double Distance(double latB, double lonB)
        {
            double rLatA = Deg2Rad(PhoneLat);
            double rLatB = Deg2Rad(latB);
            double rHalfDeltaLat = Deg2Rad((latB - PhoneLat) / 2.0);
            double rHalfDeltaLon = Deg2Rad((lonB - PhoneLong) / 2.0);

            return (2 * 3963.19) * Math.Asin(Math.Sqrt(Math.Pow(Math.Sin(rHalfDeltaLat), 2) + Math.Cos(rLatA) * Math.Cos(rLatB) * Math.Pow(Math.Sin(rHalfDeltaLon), 2)));

        }

        internal static void SortFavorites()
        {
            FavoritesDistance();

            ObservableCollection<StopData> tempCollection = new ObservableCollection<StopData>(MainPageModel.favoritesStops.OrderBy(z => z.DoubleDist));

            MainPageModel.favoritesStops.Clear();
            foreach (StopData s in tempCollection)
            {
                MainPageModel.favoritesStops.Add(new StopData(s.Name, s.Routes, s.Tags, s.DoubleDist, s.Lat, s.Lon, s.FavID));
            }
        }

        public static void FavoritesDistance()
        {
            foreach (StopData stop in MainPageModel.favoritesStops)
            {
                stop.DoubleDist = Distance(stop.Lat, stop.Lon);
            }
        }
    }
}
