﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Devices.Geolocation;
using Windows.UI.ViewManagement;
using Windows.Web.Http;
using nexMuni.DataModels;
using nexMuni.Helpers;

namespace nexMuni.ViewModels
{
    public delegate void ChangedEventHandler();

    public class SearchViewModel : INotifyPropertyChanged
    {
        private string _searchTimes;
        private string _selectedRoute;
        private List<string> _routesList;
        private List<string> _directionsList;
        private List<Stop> _stopsList;
        private Stop _selectedStop;
        private Geopoint _mapCenter;

        public string SearchTimes
        {
            get
            {
                return _searchTimes;
            }
            set
            {
                _searchTimes = value;
                NotifyPropertyChanged("SearchTimes");
            }
        }
        public string SelectedRoute
        {
            get
            {
                return _selectedRoute;
            }
            set
            {
                _selectedRoute = value;
                NotifyPropertyChanged("SelectedRoute");
            }
        }
        public Stop SelectedStop
        {
            get
            {
                return _selectedStop;
            }
            set
            {
                _selectedStop = value;
                NotifyPropertyChanged("SelectedStop");
            }
        }

        public List<string> RoutesList
        {
            get
            {
                return _routesList;
            }
            set
            {
                _routesList = value;
                NotifyPropertyChanged("RoutesList");
            }
        }
        public List<string> DirectionsList
        {
            get
            {
                return _directionsList;
            }
            set
            {
                _directionsList = value;
                NotifyPropertyChanged("DirectionsList");
            }
        }
        public List<Stop> StopsList
        {
            get
            {
                return _stopsList;
            }
            set
            {
                _stopsList = value;
                NotifyPropertyChanged("StopsList");
            }
        }
        public Geopoint MapCenter
        {
            get
            {
                return _mapCenter;
            }
            set
            {
                _mapCenter = value;
                NotifyPropertyChanged("MapCenter");
            }
        }

        private Task initialize;
        private Stop foundStop;
        private List<Stop> allStopsList;
        private List<string> outboundStops = new List<string>();
        private List<string> inboundStops = new List<string>();

        public SearchViewModel()
        {
            DatabaseHelper.FavoritesChanged += SyncFavoriteIds;
            MapCenter = new Geopoint(new BasicGeoposition() { Latitude = 37.7480, Longitude = -122.437 });

            allStopsList = new List<Stop>();
            outboundStops = new List<string>();
            inboundStops = new List<string>();
            //initialize = LoadDataAsync();
        }

        public async Task LoadRoutesAsync()
        {
            RoutesList = await DatabaseHelper.QueryForRoutes();
        }

        public async Task LoadDirectionsAsync(string route)
        {
            //if (DirectionsList.Count != 0)
            //{
            //    DirectionsList.Clear();
            //}
            //if (StopsList.Count != 0)
            //{
            //    StopsList.Clear();
            //}

            SelectedRoute = route;
            SelectedStop = null;

            var dirUrl = "http://webservices.nextbus.com/service/publicXMLFeed?command=routeConfig&a=sf-muni&r=";

            if (route.Equals("Powell/Mason Cable Car")) route = "59";
            else if (route.Equals("Powell/Hyde Cable Car")) route = "60";
            else if (route.Equals("California Cable Car")) route = "61";
            else
            {
                route = route.Substring(0, route.IndexOf('-'));
            }
            

            //selectedRoute = _route;
            dirUrl = dirUrl + route;

            var response = new HttpResponseMessage();
            var client = new HttpClient();

            //Make sure to pull from network not cache everytime predictions are refreshed 
            client.DefaultRequestHeaders.IfModifiedSince = DateTime.Now;
            try
            {
                response = await client.GetAsync(new Uri(dirUrl));
                var reader = await response.Content.ReadAsStringAsync();
                //GetDirections(XDocument.Parse(reader));
                DirectionsList = await Task.Run(() => GetDirections(reader));
            }
            catch (Exception)
            {
                ErrorHandler.NetworkError("Error getting route information. Please try again.");
            }
        }

        public async Task LoadStops(string direction)
        {
            var foundStops = new List<Stop>();
            //if (StopsList.Count != 0) StopsList.Clear();
            SelectedStop = null;
            SearchTimes = "";

            await Task.Run(() =>
            {
                if (direction.Contains("Inbound"))
                {
                    foreach (string s in inboundStops)
                    {
                        foundStops.Add(allStopsList.Find(z => z.StopTags == s));
                        //foundStop = allStopsList.Find(z => z.StopTags == s);
                        //StopsList.Add(foundStop);
                    }
                }
                else if (direction.Contains("Outbound"))
                {
                    foreach (string s in outboundStops)
                    {
                        foundStops.Add(allStopsList.Find(z => z.StopTags == s));
                        //foundStop = allStopsList.Find(z => z.StopTags == s);
                        //StopsList.Add(foundStop);
                    }
                }
            });

            StopsList = foundStops;
        }

        public async Task StopSelectedAsync(Stop stop)
        {
            SelectedStop = stop;
            string title = SelectedStop.StopName;

            if (title.Contains("Inbound"))
            {
                SelectedStop.StopName = title.Replace(" Inbound", "");
            }
            if (title.Contains("Outbound"))
            {
                SelectedStop.StopName = title.Replace(" Outbound", "");
            }

            string[] temp = SelectedStop.StopName.Split('&');
            string reversed;
            if (temp.Count() > 1)
            {
                reversed = temp[1].Substring(1) + " & " + temp[0].Substring(0, (temp[0].Length - 1));
            }
            else reversed = "";

            var xmlDoc = await WebHelper.GetSearchPredictionsAsync(SelectedStop, SelectedRoute);
            if (xmlDoc != null)
            {
                //Get bus predictions for stop
                SearchTimes = await Task.Run(() => ParseHelper.ParseSearchTimesAsync(xmlDoc));

                Stop tempStop = await GetStopAsync();
                if (tempStop != null) SelectedStop = tempStop;
            }
        }

        public async Task FavoriteSearchAsync()
        {
            await DatabaseHelper.FavoriteSearchAsync(SelectedStop);
        }

        public async Task UnfavoriteSearchAsync()
        {
            await DatabaseHelper.RemoveFavoriteAsync(SelectedStop);
        }

        public bool IsFavorite()
        {
            return DatabaseHelper.FavoritesList.Any(f => f.Name == SelectedStop.StopName);
        }

        private async Task<Stop> GetStopAsync()
        {
            List<Stop> stops = await DatabaseHelper.QueryForStop(SelectedStop.StopName);

            if (stops.Any())
                return stops.ElementAt(0);
            //return new Stop(stops[0].StopName, stops[0].Routes, stops[0].StopTags, stops[0].Latitude, stops[0].Longitude, stops[0].Distance);
            else return null;
        }

        private List<string> GetDirections(string text)
        {
            var doc = XDocument.Parse(text);

            IEnumerable<XElement> rootElement =
                from e in doc.Descendants("route")
                select e;
            IEnumerable<XElement> elements =
                from d in rootElement.ElementAt(0).Elements("stop")
                select d;

            //Add all route's stops to a collection
            foreach (XElement el in elements)
            {
                allStopsList.Add(new Stop(el.Attribute("title").Value,
                                              el.Attribute("stopId").Value,
                                              "",
                                              el.Attribute("tag").Value,
                                              double.Parse(el.Attribute("lon").Value),
                                              double.Parse(el.Attribute("lat").Value)));
            }

            //Move to direction element
            elements =
                from d in rootElement.ElementAt(0).Elements("direction")
                select d;

            var directions = new List<string>();

            foreach (XElement el in elements)
            {
                //Add direction title
                directions.Add(el.Attribute("title").Value);

                IEnumerable<XElement> tagElements;
                if (el.Attribute("name").Value == "Inbound")
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
                }
                else if (el.Attribute("name").Value == "Outbound")
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
            return directions;
        }

        private void SyncFavoriteIds()
        {
            if (SelectedStop == null) return;
            FavoriteData tempStop = DatabaseHelper.FavoritesList.ToList().Find(s => s.Name == SelectedStop.StopName);
            if (tempStop == null) return;
            SelectedStop.favId = tempStop.Id;
        }

        #region INotify Methods
        private void NotifyPropertyChanged(string property)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(property));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        #endregion
    }
}
