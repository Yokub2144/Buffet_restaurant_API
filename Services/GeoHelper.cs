using System;

namespace Buffet_Restaurant_Managment_System_API.Services
{
    public static class GeoHelper
    {

        public static double GetDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371e3; // รัศมีของโลก (เมตร)
            var lat1rad = lat1 * Math.PI / 180;
            var lat2rad = lat2 * Math.PI / 180;
            var deltaLat = (lat2 - lat1) * Math.PI / 180;
            var deltaLon = (lon2 - lon1) * Math.PI / 180;

            var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                    Math.Cos(lat1rad) * Math.Cos(lat2rad) *
                    Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }
    }
}