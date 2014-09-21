﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Linq;
using System.Xml.Linq;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http;
using Windows.Web.Http.Headers;
using Windows.Devices.Geolocation;
using Windows.UI.Xaml.Controls.Maps;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI;

namespace nexMuni
{
    class SearchModel
    {
        public static bool IsDataLoaded { get; set; }
        public static List<string> RoutesCollection { get; set; }
        public static ObservableCollection<string> DirectionCollection { get; set; }
        public static ObservableCollection<Stop> StopCollection { get; set; }
        public static HttpResponseMessage saved { get; set; }
        public static List<string> outboundStops = new List<string>();
        public static List<string> inboundStops = new List<string>();
        public static List<Stop> StopsList = new List<Stop>();
        public static List<Routes> RoutesList;
        public static List<Stop> FoundStops = new List<Stop>();
        public static string title, stopID, lat, lon, tag;
        private static string selectedRoute;
        public static Stop selectedStop { get; set; }

        public static void LoadRoutes()
        {
            RoutesCollection = DatabaseHelper.QueryForRoutes();
            //RoutesCollection = new List<string>();
            DirectionCollection = new ObservableCollection<string>();
            StopCollection = new ObservableCollection<Stop>();

            MainPage.routePicker.ItemsSource = RoutesCollection;
        }

        public static void RouteSelected(ListPickerFlyout sender, ItemsPickedEventArgs args)
        {
            #if WINDOWS_PHONE_APP
                        var systemTray = Windows.UI.ViewManagement.StatusBar.GetForCurrentView();
                        systemTray.ProgressIndicator.ProgressValue = null;
            #endif

            string selectedRoute = sender.SelectedItem.ToString();
            if (DirectionCollection.Count != 0)
            {
                MainPage.dirComboBox.SelectedIndex = -1;
                DirectionCollection.Clear();
            }
            if (StopCollection.Count != 0)
            {
                MainPage.stopPicker.SelectedIndex = -1;
                StopCollection.Clear();
            }
            MainPage.searchText.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            MainPage.favSearchBtn.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

            LoadDirections(selectedRoute);

            MainPage.dirText.Visibility = Windows.UI.Xaml.Visibility.Visible;
            MainPage.dirComboBox.Visibility = Windows.UI.Xaml.Visibility.Visible;

            #if WINDOWS_PHONE_APP
                        systemTray.ProgressIndicator.ProgressValue = 0;
            #endif
        }

        private static async void LoadDirections(string _route)
        {
            string URL = "http://webservices.nextbus.com/service/publicXMLFeed?command=routeConfig&a=sf-muni&r=";

            if (_route.Equals("Powell/Mason Cable Car")) _route = "59";
            else if (_route.Equals("Powell/Hyde Cable Car")) _route = "60";
            else if (_route.Equals("California Cable Car")) _route = "61";
            else
            {
                int i = _route.IndexOf('-');
                _route = _route.Substring(0, i);
            }
            
            selectedRoute = _route;
            URL = URL + _route;

            var response = new HttpResponseMessage();
            var client = new HttpClient();
            XDocument xmlDoc = new XDocument();
            string reader;

            if (saved != null) response = saved;

            //Make sure to pull from network not cache everytime predictions are refreshed 
            response.Headers.CacheControl.Add(new HttpNameValueHeaderValue("max-age", "1"));
            client.DefaultRequestHeaders.CacheControl.Add(new HttpNameValueHeaderValue("max-age", "1"));
            if (response.Content != null) client.DefaultRequestHeaders.IfModifiedSince = response.Content.Headers.Expires;
            try
            {
                response = await client.GetAsync(new Uri(URL));
                response.Content.Headers.Expires = System.DateTime.Now;
                saved = response;

                reader = await response.Content.ReadAsStringAsync();
                xmlDoc = XDocument.Parse(reader);

                GetDirections(xmlDoc);  
            }
            catch(Exception ex)
            {
                ErrorHandler.NetworkError("Error getting route information. Please try again.");
            }            
        }

        private static void GetDirections(XDocument doc)
        {
            IEnumerable<XElement> tagElements;
            IEnumerable<XElement> rootElement =
                from e in doc.Descendants("route")
                select e;
            IEnumerable<XElement> elements = 
                from d in rootElement.ElementAt(0).Elements("stop")
                select d;

            //Add all route's stops to a collection
            foreach (XElement el in elements)
            {
                title = el.Attribute("title").Value;
                stopID = el.Attribute("stopId").Value;
                lon = el.Attribute("lon").Value;
                lat = el.Attribute("lat").Value;
                tag = el.Attribute("tag").Value;

                StopsList.Add(new Stop(title, stopID, tag, lon, lat));
            }

            //Move to direction element
            elements =
                from d in rootElement.ElementAt(0).Elements("direction")
                select d;

            foreach (XElement el in elements)
            {   
                //Add direction title
                DirectionCollection.Add(el.Attribute("title").Value);

                if(el.Attribute("name").Value == "Inbound")
                {
                    //Get all stop elements under direction element
                    tagElements =
                        from x in el.Elements("stop")
                        select x;

                    if (inboundStops.Count != 0) inboundStops.Clear();
                    //Add tags for direction to a collection
                    foreach (XElement y in tagElements)
                    {
                        inboundStops.Add(y.Attribute("tag").Value);
                    }
                } else if (el.Attribute("name").Value == "Outbound")
                {
                    //Get all stop elements under direction element
                    tagElements =
                        from x in el.Elements("stop")
                        select x;

                    if (outboundStops.Count != 0) outboundStops.Clear();
                    //Add tags for direction to a collection
                    foreach (XElement y in tagElements)
                    {
                        outboundStops.Add(y.Attribute("tag").Value);
                    }
                }
            }
            MainPage.dirComboBox.SelectedIndex = 0;

            MapRouteView(doc);
        }

        private static void MapRouteView(XDocument doc)
        {
            MainPage.searchMap.TrySetViewAsync(new Geopoint(new BasicGeoposition() { Latitude = 37.7599, Longitude = -122.437 }), 11.5);
            List<BasicGeoposition> positions = new List<BasicGeoposition>();
            IEnumerable<XElement> subElements;
            List<MapPolyline> route = new List<MapPolyline>();

            IEnumerable<XElement> rootElement =
                from e in doc.Descendants("route")
                select e;
            IEnumerable<XElement> elements =
                from d in rootElement.ElementAt(0).Elements("path")
                select d;
            int x = 0;
            if (MainPage.searchMap.MapElements.Count > 0) MainPage.searchMap.MapElements.Clear();
            foreach (XElement el in elements)
            {
                subElements =
                    from p in el.Elements("point")
                    select p;

                if (positions.Count > 0) positions.Clear();
                foreach (XElement e in subElements)
                {
                    positions.Add(new BasicGeoposition() { Latitude = Double.Parse(e.Attribute("lat").Value), Longitude = Double.Parse(e.Attribute("lon").Value) });
                }
                route.Add(new MapPolyline());
                route[x].StrokeColor = Color.FromArgb(255,179,27,27);
                route[x].StrokeThickness = 2.00;
                route[x].ZIndex = 99;
                route[x].Path = new Geopath(positions);
                route[x].Visible = true;
                MainPage.searchMap.MapElements.Add(route[x]);
                x++;
            }

            MapIcon icon = new MapIcon();
            icon.Image = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/Location.png"));
            icon.Location = LocationHelper.PhoneLocation.Coordinate.Point;
            icon.ZIndex = 99;
            MainPage.searchMap.MapElements.Add(icon);
        }

        public static void DirSelected(object sender, SelectionChangedEventArgs e)
        {
            if(((ComboBox)sender).SelectedIndex != -1)
            {
                MainPage.stopPicker.SelectedIndex = -1;
                string selectedDir = ((ComboBox)sender).SelectedItem.ToString();
                LoadStops(selectedDir);
            }

            MainPage.stopText.Visibility = Windows.UI.Xaml.Visibility.Visible;
            MainPage.stopBtn.Visibility = Windows.UI.Xaml.Visibility.Visible;
            MainPage.searchText.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            MainPage.favSearchBtn.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
        }

        private static void LoadStops(string _dir)
        {
            if(StopCollection.Count != 0) StopCollection.Clear();

            if(_dir.Contains("Inbound"))
            {
                foreach(string s in inboundStops)
                {
                    FoundStops = StopsList.FindAll(z => z.tag == s);
                    StopCollection.Add(new Stop(FoundStops[0].title, FoundStops[0].stopID, FoundStops[0].tag, FoundStops[0].lon.ToString(), FoundStops[0].lat.ToString()));
                }
            }
            else if(_dir.Contains("Outbound"))
            {
                foreach(string s in outboundStops)
                {
                    FoundStops = StopsList.FindAll(z => z.tag == s);
                    StopCollection.Add(new Stop(FoundStops[0].title, FoundStops[0].stopID, FoundStops[0].tag, FoundStops[0].lon.ToString(), FoundStops[0].lat.ToString()));
                }
            }
        }

        public static async void StopSelected(ListPickerFlyout sender, ItemsPickedEventArgs args)
        {
            if (sender.SelectedIndex != -1)
            {
#if WINDOWS_PHONE_APP
                var systemTray = Windows.UI.ViewManagement.StatusBar.GetForCurrentView();
                systemTray.ProgressIndicator.Text = "Getting Arrival Times";
                systemTray.ProgressIndicator.ProgressValue = null;
#endif

                selectedStop = sender.SelectedItem as Stop;
                string title = selectedStop.title;
                if (title.Contains("Inbound"))
                {
                    title = title.Replace(" Inbound", "");
                }
                if (title.Contains("Outbound"))
                {
                    title = title.Replace(" Outbound", "");
                }

                string[] temp = selectedStop.title.Split('&');
                string reversed;
                if (temp.Count() > 1)
                {
                    reversed = temp[1].Substring(1) + " & " + temp[0].Substring(0, (temp[0].Length - 1));
                }
                else reversed = "";

                string url = "http://webservices.nextbus.com/service/publicXMLFeed?command=predictions&a=sf-muni&stopId=" + selectedStop.stopID + "&routeTag=" + selectedRoute;

                await MainPage.searchMap.TrySetViewAsync(new Geopoint(new BasicGeoposition() { Latitude = selectedStop.lat, Longitude = selectedStop.lon }), 16.5);

                if (MainPage.searchText.Visibility == Windows.UI.Xaml.Visibility.Visible) MainPage.searchText.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                if (MainPage.favSearchBtn.Visibility == Windows.UI.Xaml.Visibility.Visible) MainPage.favSearchBtn.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

                //Get bus predictions for stop
                PredictionModel.SearchPredictions(selectedStop, selectedRoute, url);

                //Check to see if the stop is in user's favorites list
                if (MainPageModel.favoritesStops.Any(z => z.Name == title || z.Name == reversed))
                {
                    foreach (StopData s in MainPageModel.favoritesStops)
                    {
                        if (s.Name == title) selectedStop.FavID = s.FavID;
                    }
                    MainPage.searchText.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    MainPage.favSearchBtn.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    //MainPage.removeSearchBtn.Visibility = Windows.UI.Xaml.Visibility.Visible;
                }
                else
                {
                    MainPage.searchText.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    MainPage.favSearchBtn.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    MainPage.removeSearchBtn.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                }

#if WINDOWS_PHONE_APP
                systemTray.ProgressIndicator.ProgressValue = 0;
                systemTray.ProgressIndicator.Text = "nexMuni";
#endif
            }
            else
            {
                MainPage.searchText.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                MainPage.favSearchBtn.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
        }
    }

    public class Stop
    {
        public string title { get; set; }
        public string stopID;
        public double lon;
        public double lat;
        public string tag;
        public string FavID { get; set; }

        public Stop() {}

        public Stop(string _title, string _id, string _tag, string _lon, string _lat)
        {
            title = _title;
            stopID = _id;
            lat = double.Parse(_lat);
            lon = double.Parse(_lon);
            tag = _tag;
        }
    }
}
