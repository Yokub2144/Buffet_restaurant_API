using System.IO;
using System.Text.Json;

namespace Buffet_Restaurant_Managment_System_API.Services
{
    public static class ShopLocationService
    {
        // บันทึกไฟล์ไว้ที่ root ของโปรเจกต์
        private static readonly string _filePath = "shop_location.json";

        // ตัวล็อคเพื่อป้องกันการอ่าน/เขียนไฟล์พร้อมกัน
        private static readonly object _fileLock = new object();

        public class LocationData
        {
            public double Latitude { get; set; }
            public double Longitude { get; set; }
        }

        public static void SaveLocation(double latitude, double longitude)
        {
            var data = new LocationData { Latitude = latitude, Longitude = longitude };
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(data, options);

            // ใช้ lock เพื่อป้องกันไฟล์พังเวลาเรียกใช้งานพร้อมกันหลายคน
            lock (_fileLock)
            {
                File.WriteAllText(_filePath, jsonString);
            }
        }

        public static LocationData GetLocation()
        {
            lock (_fileLock)
            {
                if (!File.Exists(_filePath))
                {
                    return null;
                }

                string jsonString = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<LocationData>(jsonString);
            }
        }
    }
}