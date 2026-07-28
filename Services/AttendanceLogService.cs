using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Buffet_Restaurant_Managment_System_API.Services
{
    public static class AttendanceLogService
    {
        private static readonly string _filePath = "attendance_logs.json";
        private static readonly object _fileLock = new object();

        // 1. อัปเดตโครงสร้างข้อมูลให้รองรับเวลาออกงาน
        public class AttendanceRecord
        {
            public int EmployeeId { get; set; }
            public string EmployeeName { get; set; }
            public DateTime ClockInTime { get; set; }
            public DateTime? ClockOutTime { get; set; } // เพิ่ม DateTime? (ใส่ ? เพื่อให้เป็น null ได้ตอนที่เพิ่งเข้างาน)
        }

        // 2. ฟังก์ชันลงเวลาเข้างาน (ของเดิม)
        public static void SaveLog(int empId, string empName, DateTime time)
        {
            lock (_fileLock)
            {
                var logs = GetAllLogsInternal();

                logs.Add(new AttendanceRecord
                {
                    EmployeeId = empId,
                    EmployeeName = empName,
                    ClockInTime = time,
                    ClockOutTime = null // เข้างานใหม่ยังไม่มีเวลาออก
                });

                SaveToFile(logs);
            }
        }

        // 3. (เพิ่มใหม่) ฟังก์ชันลงเวลาออกงาน
        public static bool ClockOutLog(int empId, DateTime time)
        {
            lock (_fileLock)
            {
                var logs = GetAllLogsInternal();

                // หาประวัติการเข้างานล่าสุดของพนักงานคนนี้ ที่ยังไม่ได้ลงเวลาออกงาน (ClockOutTime == null)
                var lastRecord = logs.LastOrDefault(l => l.EmployeeId == empId && l.ClockOutTime == null);

                if (lastRecord != null)
                {
                    lastRecord.ClockOutTime = time; // อัปเดตเวลาออกงาน
                    SaveToFile(logs);
                    return true;
                }
                return false; // กรณีหาไม่เจอ หรือลงเวลาออกไปแล้ว
            }
        }

        // 4. (เพิ่มใหม่) ฟังก์ชันสำหรับดึงข้อมูลประวัติทั้งหมดไปแสดงผล
        public static List<AttendanceRecord> GetAllLogs()
        {
            lock (_fileLock)
            {
                return GetAllLogsInternal();
            }
        }

        // Helper Methods ไว้จัดการไฟล์
        private static List<AttendanceRecord> GetAllLogsInternal()
        {
            if (File.Exists(_filePath))
            {
                string existingJson = File.ReadAllText(_filePath);
                if (!string.IsNullOrWhiteSpace(existingJson))
                {
                    return JsonSerializer.Deserialize<List<AttendanceRecord>>(existingJson) ?? new List<AttendanceRecord>();
                }
            }
            return new List<AttendanceRecord>();
        }

        private static void SaveToFile(List<AttendanceRecord> logs)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_filePath, JsonSerializer.Serialize(logs, options));
        }
    }
}