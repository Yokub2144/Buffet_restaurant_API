using Microsoft.AspNetCore.Mvc;
using Buffet_Restaurant_Managment_System_API.Data;
using Buffet_Restaurant_Managment_System_API.Services;
using Buffet_Restaurant_Managment_System_API.Dtos;
using Buffet_Restaurant_Managment_System_API.Models;
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
            //อ่านพิกัดร้านจาก JSON
            var shopLocation = ShopLocationService.GetLocation();
            if (shopLocation == null)
            {
                return BadRequest(new { status = "error", message = "เจ้าของร้านยังไม่ได้ตั้งค่าพิกัดร้านในระบบ" });
            }

            //  คำนวณระยะทาง
            double distance = GeoHelper.GetDistance(
                shopLocation.Latitude,
                shopLocation.Longitude,
                request.Latitude,
                request.Longitude
            );

            //ตรวจสอบระยะทาง 1 km (1000 เมตร)
            if (distance > 1000)
            {
                return BadRequest(new
                {
                    status = "error",
                    message = $"คุณอยู่นอกพื้นที่ทำงาน (ห่างจากร้าน {Math.Round(distance)} เมตร)"
                });
            }

            //  ตรวจสอบพนักงานใน DB
            var employee = _context.Employee.FirstOrDefault(e => e.Emp_id == request.EmployeeId);
            if (employee == null)
            {
                return NotFound(new { status = "error", message = "ไม่พบรหัสพนักงานนี้ในระบบ" });
            }

            //  เช็คว่าพนักงานลงเวลาเข้างานค้างไว้แต่ยังไม่ได้ออกงานหรือไม่
            var activeLog = _context.TimeLog
                .FirstOrDefault(t => t.Emp_id == request.EmployeeId && t.Time_out == null);

            if (activeLog != null)
            {
                return BadRequest(new { status = "error", message = "คุณได้ลงเวลาเข้างานไปแล้ว และยังไม่ได้ลงเวลาออกงาน" });
            }

            //บันทึกข้อมูลเข้า Database
            var now = DateTime.Now;
            var newLog = new TimeLog
            {
                Emp_id = employee.Emp_id,
                Date = now.Date,
                Time_in = now
            };

            _context.TimeLog.Add(newLog);
            _context.SaveChanges();

            return Ok(new
            {
                status = "success",
                message = "ลงเวลาเข้างานสำเร็จ",
                distance = Math.Round(distance),
                time = now.ToString("dd/MM/yyyy HH:mm:ss")
            });
        }

        [HttpPost("clock-out")]
        public IActionResult ClockOut([FromBody] ClockInDto request)
        {
            // อ่านพิกัดร้านจาก JSON
            var shopLocation = ShopLocationService.GetLocation();
            if (shopLocation == null)
                return BadRequest(new { status = "error", message = "เจ้าของร้านยังไม่ได้ตั้งค่าพิกัดร้านในระบบ" });

            // คำนวณระยะทาง
            double distance = GeoHelper.GetDistance(
                shopLocation.Latitude, shopLocation.Longitude, request.Latitude, request.Longitude);

            if (distance > 1000)
                return BadRequest(new { status = "error", message = $"คุณอยู่นอกพื้นที่ทำงาน (ห่างจากร้าน {Math.Round(distance)} เมตร)" });

            //  ตรวจสอบพนักงาน
            var employee = _context.Employee.FirstOrDefault(e => e.Emp_id == request.EmployeeId);
            if (employee == null)
                return NotFound(new { status = "error", message = "ไม่พบรหัสพนักงานนี้ในระบบ" });

            //  ค้นหา Log ล่าสุดที่ยังไม่ได้ลงเวลาออกงาน
            var lastLog = _context.TimeLog
                .Where(t => t.Emp_id == request.EmployeeId && t.Time_out == null)
                .OrderByDescending(t => t.Time_in)
                .FirstOrDefault();

            if (lastLog == null)
            {
                return BadRequest(new { status = "error", message = "ไม่สามารถลงเวลาออกงานได้ (คุณอาจยังไม่ได้ลงเวลาเข้างาน หรือลงเวลาออกไปแล้ว)" });
            }

            //  บันทึกเวลาออกงาน
            var now = DateTime.Now;
            lastLog.Time_out = now;
            _context.SaveChanges();

            // คำนวณเวลาที่ทำงานจริง และสร้างข้อความแจ้งเตือนส่งกลับไปหน้าบ้าน
            TimeSpan workDuration = now - lastLog.Time_in;
            string alertMessage = "ลงเวลาออกงานสำเร็จ";

            // สมมติว่ากะปกติคือ 13 ชั่วโมง (ถ้าทำน้อยกว่า 13 ชม. ให้ขึ้นเตือน)
            if (workDuration.TotalHours < 13)
            {
                int totalMinutes = (int)Math.Round(workDuration.TotalMinutes);
                alertMessage = $"ลงเวลาออกงานสำเร็จ (เตือน: คุณทำงานไปเพียง {totalMinutes} นาที รายได้จะถูกคิดตามชั่วโมงจริง)";
            }

            return Ok(new
            {
                status = "success",
                message = alertMessage,
                distance = Math.Round(distance),
                time = now.ToString("dd/MM/yyyy HH:mm:ss")
            });
        }

        [HttpGet("logs")]
        public IActionResult GetAttendanceLogs()
        {
            var logs = _context.TimeLog
                .Select(t => new
                {
                    t.Timelog_id,
                    t.Emp_id,
                    ClockInTime = t.Time_in,
                    ClockOutTime = t.Time_out,
                    t.Date
                })
                .OrderByDescending(t => t.ClockInTime)
                .ToList();

            return Ok(new
            {
                status = "success",
                totalRecords = logs.Count,
                data = logs
            });
        }


        [HttpGet("income/{empId}")]
        public IActionResult GetEmployeeIncome(int empId)
        {
            var employee = _context.Employee.FirstOrDefault(e => e.Emp_id == empId);
            if (employee == null)
            {
                return NotFound(new { status = "error", message = "ไม่พบรหัสพนักงานนี้ในระบบ" });
            }

            decimal dailyWage = employee.Wage ?? 0m;

            // ดึงข้อมูลการเข้า-ออกงานที่มีการกด Clock Out แล้ว
            var logs = _context.TimeLog
                .Where(t => t.Emp_id == empId && t.Time_out != null)
                .OrderByDescending(t => t.Date)
                .ToList();

            var today = DateTime.Today;

            // สมมติว่ากะปกติของร้านคือ 13 ชั่วโมง (เช่น 10:00 - 23:00) 
            decimal standardWorkHoursPerDay = 13.0m;

            var dailyIncomeLogs = logs.Select(l =>
            {
                //  คำนวณจำนวนชั่วโมงที่ทำงานจริง
                TimeSpan duration = l.Time_out.Value - l.Time_in;
                decimal hoursWorked = (decimal)duration.TotalHours;

                // คำนวณรายได้ตามสัดส่วนจริง (Pro-rate)
                decimal calculatedIncome = 0m;
                if (hoursWorked > 0 && standardWorkHoursPerDay > 0)
                {
                    // คิดค่าจ้างต่อชั่วโมง = ค่าจ้างรายวัน / ชั่วโมงกะปกติ
                    decimal hourlyRate = dailyWage / standardWorkHoursPerDay;

                    // รายได้ = ชั่วโมงที่ทำจริง * ค่าจ้างต่อชั่วโมง
                    calculatedIncome = hoursWorked * hourlyRate;

                    // ป้องกันไม่ให้รายได้เกินค่าจ้างรายวัน
                    if (calculatedIncome > dailyWage)
                    {
                        calculatedIncome = dailyWage;
                    }
                }

                return new
                {
                    DateObj = l.Date,
                    Date = l.Date.ToString("dd/MM/yyyy", new System.Globalization.CultureInfo("th-TH")),
                    TimeRange = $"{l.Time_in:HH:mm}-{l.Time_out?.ToString("HH:mm")}",
                    HoursWorked = Math.Round(hoursWorked, 2),
                    DailyIncome = Math.Round(calculatedIncome, 2)
                };
            }).ToList();

            decimal todayIncome = dailyIncomeLogs
                .Where(x => x.DateObj.Date == today.Date)
                .Sum(x => x.DailyIncome);

            decimal monthIncome = dailyIncomeLogs
                .Where(x => x.DateObj.Month == today.Month && x.DateObj.Year == today.Year)
                .Sum(x => x.DailyIncome);

            decimal totalIncome = dailyIncomeLogs.Sum(x => x.DailyIncome);

            return Ok(new
            {
                status = "success",
                summary = new
                {
                    DailyIncome = todayIncome,
                    MonthlyIncome = monthIncome,
                    TotalIncome = totalIncome
                },
                logs = dailyIncomeLogs.Select(x => new
                {
                    x.Date,
                    x.TimeRange,
                    x.DailyIncome
                })
            });
        }
    }
}