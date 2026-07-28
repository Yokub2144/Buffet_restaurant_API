using Microsoft.AspNetCore.Mvc;
using Buffet_Restaurant_Managment_System_API.Data;
using Buffet_Restaurant_Managment_System_API.Services;
using Buffet_Restaurant_Managment_System_API.Dtos;
using System;
using System.Linq;

namespace Buffet_Restaurant_Managment_System_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AttendanceController : ControllerBase
    {
        private readonly restaurantDbContext _context;

        public AttendanceController(restaurantDbContext context)
        {
            _context = context;
        }

        [HttpPost("clock-in")]
        public IActionResult ClockIn([FromBody] ClockInDto request)
        {
            // อ่านพิกัดร้านจาก JSON
            var shopLocation = ShopLocationService.GetLocation();
            if (shopLocation == null)
            {
                return BadRequest(new { status = "error", message = "เจ้าของร้านยังไม่ได้ตั้งค่าพิกัดร้านในระบบ" });
            }

            // คำนวณระยะทาง
            double distance = GeoHelper.GetDistance(
                shopLocation.Latitude,
                shopLocation.Longitude,
                request.Latitude,
                request.Longitude
            );

            //  ตรวจสอบว่าอยู่ในระยะ 1 km
            if (distance > 1000)
            {
                return BadRequest(new
                {
                    status = "error",
                    message = $"คุณอยู่นอกพื้นที่ทำงาน (ห่างจากร้าน {Math.Round(distance)} เมตร)"
                });
            }


            var employee = _context.Employee.FirstOrDefault(e => e.Emp_id == request.EmployeeId);
            if (employee == null)
            {
                return NotFound(new { status = "error", message = "ไม่พบรหัสพนักงานนี้ในระบบ" });
            }

            // บันทึกประวัติการเข้างานลงไฟล์ JSON (ไม่ยุ่งกับ Database)
            AttendanceLogService.SaveLog(employee.Emp_id, employee.Fullname, DateTime.Now);

            return Ok(new
            {
                status = "success",
                message = "ลงเวลาเข้างานสำเร็จ",
                distance = Math.Round(distance),
                time = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")
            });

        }

        [HttpPost("clock-out")]
        public IActionResult ClockOut([FromBody] ClockInDto request) // ใช้ DTO ตัวเดิมรับพิกัดได้เลย
        {
            var shopLocation = ShopLocationService.GetLocation();
            if (shopLocation == null)
                return BadRequest(new { status = "error", message = "เจ้าของร้านยังไม่ได้ตั้งค่าพิกัดร้านในระบบ" });

            double distance = GeoHelper.GetDistance(
                shopLocation.Latitude, shopLocation.Longitude, request.Latitude, request.Longitude);

            if (distance > 1000)
                return BadRequest(new { status = "error", message = $"คุณอยู่นอกพื้นที่ทำงาน (ห่างจากร้าน {Math.Round(distance)} เมตร)" });

            var employee = _context.Employee.FirstOrDefault(e => e.Emp_id == request.EmployeeId);
            if (employee == null)
                return NotFound(new { status = "error", message = "ไม่พบรหัสพนักงานนี้ในระบบ" });

            // อัปเดตเวลาออกงาน
            bool isSuccess = AttendanceLogService.ClockOutLog(employee.Emp_id, DateTime.Now);

            if (!isSuccess)
            {
                return BadRequest(new { status = "error", message = "ไม่สามารถลงเวลาออกงานได้ (คุณอาจยังไม่ได้ลงเวลาเข้างาน หรือลงเวลาออกไปแล้ว)" });
            }

            return Ok(new
            {
                status = "success",
                message = "ลงเวลาออกงานสำเร็จ",
                distance = Math.Round(distance),
                time = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")
            });
        }


        [HttpGet("logs")]
        public IActionResult GetAttendanceLogs()
        {
            var logs = AttendanceLogService.GetAllLogs();
            return Ok(new
            {
                status = "success",
                totalRecords = logs.Count,
                data = logs
            });
        }
    }
}
