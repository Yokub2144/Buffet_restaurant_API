using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Buffet_Restaurant_Managment_System_API.Data;
using Buffet_Restaurant_Managment_System_API.Models;
using Buffet_Restaurant_Managment_System_API.Dtos;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Authorization;
using QRCoder;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.SignalR;
using Buffet_Restaurant_Managment_System_API.Hubs;
using System.Reflection.Metadata.Ecma335;
using MimeKit.Encodings;
using Buffet_Restaurant_API.Models;
namespace Buffet_Restaurant_Managment_System_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ManagerController : ControllerBase
    {
        private readonly restaurantDbContext _context;
        private readonly IHubContext<tableStatusHub> _hubContext;
        public ManagerController(restaurantDbContext context, IHubContext<tableStatusHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        [Authorize(Roles = "เจ้าของร้าน")]
        [HttpGet("getEmployeesNotApproved")]
        public async Task<IActionResult> GetEmployeesNotApproved()
        {
            try
            {
                var employees = await _context.Employee
                .Where(e => e.Employee_Status == "" || e.Employee_Status == null)
                .ToListAsync();
                return Ok(employees);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "เกิดข้อผิดพลาดในการดึงข้อมูลพนักงานที่รอการอนุมัติ", Error = ex.Message });
            }
        }
        [Authorize(Roles = "เจ้าของร้าน")]
        [HttpPost("approveEmployee")]
        public async Task<IActionResult> ApproveEmployee(int empId)
        {
            try
            {
                var employee = await _context.Employee
                    .FirstOrDefaultAsync(e => e.Emp_id == empId && (e.Employee_Status == "" || e.Employee_Status == null));
                if (employee == null)
                {
                    return NotFound(new { Message = "ไม่พบพนักงานที่รอการอนุมัติ" });
                }
                employee.Employee_Status = "ทำงานปัจจุบัน";
                employee.Hire_Date = DateTime.Now;
                await _context.SaveChangesAsync();
                return Ok(new { Message = "อนุมัติพนักงานสำเร็จ" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "เกิดข้อผิดพลาดในการอนุมัติพนักงาน", Error = ex.Message });
            }

        }

        [Authorize(Roles = "เจ้าของร้าน")]
        [HttpPost("rejectEmployee")]
        public async Task<IActionResult> RejectEmployee(int empId)
        {
            try
            {
                var employee = await _context.Employee
                    .FirstOrDefaultAsync(e => e.Emp_id == empId && (e.Employee_Status == "" || e.Employee_Status == null));
                if (employee == null)
                {
                    return NotFound(new { Message = "ไม่พบพนักงานที่รอการอนุมัติ" });
                }
                _context.Employee.Remove(employee);
                await _context.SaveChangesAsync();
                return Ok(new { Message = "ปฏิเสธพนักงานสำเร็จ" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "เกิดข้อผิดพลาดในการปฏิเสธพนักงาน", Error = ex.Message });
            }
        }
        [Authorize(Roles = "เจ้าของร้าน")]
        [HttpGet("getAllEmployees")]
        public async Task<IActionResult> GetAllEmployees()
        {
            try
            {
                var employees = await _context.Employee
                    .Where(e => e.Employee_Status == "ทำงานปัจจุบัน" || e.Employee_Status == "ลาออก")
                    .OrderByDescending(e => e.Employee_Status == "ทำงานปัจจุบัน")
                    .ThenBy(e => e.Emp_id)
                    .ToListAsync();
                return Ok(employees);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "เกิดข้อผิดพลาดในการดึงข้อมูลพนักงาน", Error = ex.Message });
            }
        }
        [HttpGet("getEmployeeById")]
        public async Task<IActionResult> GetEmployeeById(int empId)
        {
            try
            {
                var employee = await _context.Employee
                    .FirstOrDefaultAsync(e => e.Emp_id == empId);
                if (employee == null)
                {
                    return NotFound(new { Message = "ไม่พบพนักงาน" });
                }
                return Ok(employee);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "เกิดข้อผิดพลาดในการดึงข้อมูลพนักงาน", Error = ex.Message });
            }

        }
        [Authorize(Roles = "เจ้าของร้าน")]
        [HttpPost("AddTable")]
        public async Task<IActionResult> AddTable([FromBody] tableDtos table, [FromServices] Cloudinary cloudinary)
        {
            try
            {
                var existingTable = await _context.Tables
                    .FirstOrDefaultAsync(t => t.Table_Number == table.Table_Number);
                if (existingTable != null)
                {
                    return BadRequest(new { Message = "โต๊ะหมายเลขนี้มีอยู่แล้ว" });
                }
                string menuUrl = $"https://buffet-restaurant-management-system.vercel.app/Customer?table={table.Table_Number}";
                string qrCodeImageUrl = "";
                using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
                {
                    QRCodeData qrCodeData = qrGenerator.CreateQrCode(menuUrl, QRCodeGenerator.ECCLevel.Q);
                    PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);
                    byte[] qrCodeBytes = qrCode.GetGraphic(20);
                    using (var stream = new MemoryStream(qrCodeBytes))
                    {
                        var uploadParams = new ImageUploadParams()
                        {
                            File = new FileDescription($"qr_table_{table.Table_Number}.png", stream),
                            Folder = "restaurant_qrcodes",
                            PublicId = $"table_{table.Table_Number}_{Guid.NewGuid()}"
                        };

                        var uploadResult = await cloudinary.UploadAsync(uploadParams);

                        if (uploadResult.Error != null)
                        {
                            return StatusCode(500, "ไม่สามารถอัปโหลด QR Code ได้: " + uploadResult.Error.Message);
                        }

                        qrCodeImageUrl = uploadResult.SecureUrl.ToString();
                    }
                }
                var newTable = new Tables
                {
                    Table_Number = table.Table_Number,
                    Table_Status = "ว่าง",
                    Table_QR_Code = qrCodeImageUrl
                };

                _context.Tables.Add(newTable);
                await _context.SaveChangesAsync();
                return Ok(new { Message = "เพิ่มโต๊ะสำเร็จ", table = newTable });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "เกิดข้อผิดพลาดในการเพิ่มโต๊ะ", Error = ex.Message });
            }
        }
        [HttpGet("getTables")]
        public async Task<IActionResult> getTables()
        {
            try
            {
                var tables = await _context.Tables.ToListAsync();
                return Ok(tables);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "เกิดข้อผิดพลาดในการดึงข้อมูลโต๊ะ", Error = ex.Message });
            }

        }
        [HttpGet("getTableById")]
        public async Task<IActionResult> GetTableById(int tableId)
        {
            try
            {
                var table = await _context.Tables
                    .FirstOrDefaultAsync(t => t.Table_id == tableId);
                if (table == null)
                {
                    return NotFound(new { Message = "ไม่พบโต๊ะ" });
                }
                return Ok(table);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "เกิดข้อผิดพลาดในการดึงข้อมูลโต๊ะ", Error = ex.Message });
            }

        }
        [HttpGet("getTableId")]
        public async Task<IActionResult> GetTableID(string tableName)
        {
            try
            {
                var tableid = await _context.Tables.FirstOrDefaultAsync(t => t.Table_Number == tableName);
                if (tableid == null)
                {
                    return NotFound(new { Message = "ไม่พบโต๊ะ" });
                }
                return Ok(tableid.Table_id);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "เกิดข้อผิดพลาดในการดึงข้อมูลโต๊ะ", Error = ex.Message });
            }
        }
        [Authorize(Roles = "เจ้าของร้าน")]
        [HttpDelete("deleteTable")]
        public async Task<IActionResult> DeleteTable(int tableId)
        {
            try
            {
                var table = await _context.Tables
                    .FirstOrDefaultAsync(t => t.Table_id == tableId);
                if (table == null)
                {
                    return NotFound(new { Message = "ไม่พบโต๊ะ" });
                }
                _context.Tables.Remove(table);
                await _context.SaveChangesAsync();
                return Ok(new { Message = "ลบโต๊ะสำเร็จ" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "เกิดข้อผิดพลาดในการลบโต๊ะ", Error = ex.Message });
            }
        }

        [HttpPut("updateTables")]
        public async Task<IActionResult> updateTables([FromBody] updateTables req)
        {
            try
            {
                var table = await _context.Tables.FirstOrDefaultAsync(t => t.Table_id == req.Table_id);
                if (table == null) return NotFound("ไม่พบข้อมูลโต๊ะ");
                table.Table_Number = req.Table_Number ?? table.Table_Number;
                await _context.SaveChangesAsync();
                return Ok(new { Message = "อัปเดตโต๊ะสำเร็จ", data = table });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "เกิดข้อผิดพลาดในการอัปเดตโต๊ะ", Error = ex.Message });
            }

        }
        [HttpPut("updateTablestatus")]
        public async Task<IActionResult> updateTableStatus([FromBody] updateTableStatus req)
        {
            try
            {
                var table = await _context.Tables.FirstOrDefaultAsync(t => t.Table_id == req.tableId);

                if (table == null) return NotFound("ไม่พบข้อมูลโต๊ะ");

                table.Table_Status = req.status ?? table.Table_Status;
                await _context.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("UpdateTable", new
                {
                    tableId = table.Table_id,
                    status = table.Table_Status
                });

                return Ok(new { message = "อัปเดตสถานะสำเร็จ", data = table });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "เกิดข้อผิดพลาดในการอัปเดตสถานะโต๊ะ", Error = ex.Message });
            }
        }
        [HttpGet("by-bill/{billId}")] // เปลี่ยนชื่อ Endpoint ให้เหมาะสม
        public async Task<IActionResult> GetTableByBillId(int billId)
        {
            // 🎯 ค้นหาใน GroupTables ทุกแถวที่มี Bill_id ตรงกับที่หน้าบ้านส่งมา
            var tables = await _context.GroupTables
                .Where(gt => gt.Bill_id == billId)
                .Join(_context.Tables,
                    gt => gt.Table_id,
                    t => t.Table_id,
                    (gt, t) => new
                    {
                        t.Table_id,
                        t.Table_Number,
                        t.Table_Status
                    })
                .ToListAsync();

            if (!tables.Any())
            {
                return NotFound(new { message = "ไม่พบข้อมูลโต๊ะสำหรับบิลนี้" });
            }

            return Ok(tables);
        }

        [HttpPut("change-table/{billId}")]
        public async Task<IActionResult> ChangeTable(int billId, [FromBody] ChangeTableDto dto)
        {
            if (dto == null || !dto.Table_ids.Any())
            {
                return BadRequest(new { message = "กรุณาระบุเลขโต๊ะใหม่อย่างน้อย 1 โต๊ะ" });
            }

            // เริ่มต้น Transaction ป้องกันข้อมูลอัปเดตครึ่งๆ กลางๆ
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // เช็คว่าบิลนี้มีตัวตนอยู่จริงไหม
                var bill = await _context.Bill.FirstOrDefaultAsync(b => b.Bill_id == billId);
                if (bill == null)
                {
                    return NotFound(new { message = "ไม่พบรหัสบิลที่ระบุ" });
                }

                // เคลียร์โต๊ะเก่ากลับเป็น "ว่าง"
                // ดึงกลุ่มโต๊ะเดิมทั้งหมดของบิลนี้ออกมา
                var oldGroupTables = await _context.GroupTables
                    .Where(gt => gt.Bill_id == billId)
                    .ToListAsync();

                if (oldGroupTables.Any())
                {
                    var oldTableIds = oldGroupTables.Select(gt => gt.Table_id).ToList();
                    // ค้นหาโต๊ะเหล่านั้นในตาราง Tables
                    var oldTables = await _context.Tables
                        .Where(t => oldTableIds.Contains(t.Table_id))
                        .ToListAsync();
                    // ปรับสถานะโต๊ะเก่าคืนเป็น "ว่าง"
                    foreach (var table in oldTables)
                    {
                        table.Table_Status = "ว่าง";
                        await _hubContext.Clients.All.SendAsync("UpdateTable", new
                        {
                            tableId = table.Table_id,
                            status = "ว่าง"
                        });
                    }
                    // ลบความสัมพันธ์เดิมใน GroupTables ออกทั้งหมด
                    _context.GroupTables.RemoveRange(oldGroupTables);
                }

                //อัปเดตโต๊ะใหม่เป็น ไม่ว่าง และบันทึกเข้ากลุ่ม
                var newTables = await _context.Tables
                    .Where(t => dto.Table_ids.Contains(t.Table_id))
                    .ToListAsync();

                //เช็คว่าโต๊ะใหม่ที่เลือกมา มีโต๊ะไหนแอบโดนคนอื่นตัดหน้าเปิดไปก่อนไหม (ยกเว้นโต๊ะเดิมของตัวเอง)
                var oldTableIdsSet = oldGroupTables.Select(gt => gt.Table_id).ToHashSet();
                foreach (var table in newTables)
                {
                    if (table.Table_Status != "ว่าง" && !oldTableIdsSet.Contains(table.Table_id))
                    {
                        return BadRequest(new { message = $"โต๊ะ {table.Table_Number} ไม่ว่างในระบบแล้ว กรุณาเลือกใหม่อีกครั้ง" });
                    }

                    //เปลี่ยนสถานะโต๊ะใหม่ให้เป็น ไม่ว่าง
                    table.Table_Status = "ไม่ว่าง";
                    // ส่งข้อมูลโต๊ะที่อัปเดตไปยัง SignalR
                    await _hubContext.Clients.All.SendAsync("UpdateTable", new
                    {
                        tableId = table.Table_id,
                        status = "ไม่ว่าง"
                    });

                    //เพิ่มแถวใหม่ลงตารางความสัมพันธ์ GroupTables
                    var newGroup = new GroupTable // เปลี่ยนชื่อตาม Entity ของคุณ
                    {
                        Bill_id = billId,
                        Table_id = table.Table_id
                    };
                    _context.GroupTables.Add(newGroup);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { message = "เปลี่ยนโต๊ะและอัปเดตสถานะสำเร็จ!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "เกิดข้อผิดพลาดในระบบ", error = ex.Message });
            }
        }

        [HttpPut("updateDepartmentEmp")]
        public async Task<IActionResult> updataDepartmentEmp([FromBody] updataDepartmentEmp req)
        {
            try
            {
                var employee = await _context.Employee.FirstOrDefaultAsync(e => e.Emp_id == req.Emp_id);

                if (employee == null) return NotFound("ไม่พบพนักงาน");

                employee.Department = req.Department ?? employee.Department;
                await _context.SaveChangesAsync();

                return Ok(new { message = "อัปเดพแผนกของพนักงานสำเร็จ", data = employee });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "เกิดข้อผิดพลาดในการอัปเดตแผนกของพนักงาน", Error = ex.Message });
            }

        }

        [HttpPut("updateStatusEmp")]
        public async Task<IActionResult> updateStatusEmp([FromBody] updateStatusEmpReq req)
        {
            try
            {
                var employee = await _context.Employee.FirstOrDefaultAsync(e => e.Emp_id == req.Emp_id);

                if (employee == null) return NotFound("ไม่พบพนักงาน");

                employee.Employee_Status = req.Employee_Status ?? employee.Employee_Status;
                await _context.SaveChangesAsync();
                return Ok(new { message = "อัปเดตสถานะพนักงานเรียบร้อย", data = employee });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "เกิดข้อผิดพลาดในการอัปเดตสถานะพนักงาน", Error = ex.Message });
            }

        }

        [HttpPut("updateTypeEmp")]
        public async Task<IActionResult> updateTypeEmp([FromBody] updateTypeEmpReq req)
        {
            try
            {
                var employee = await _context.Employee.FirstOrDefaultAsync(e => e.Emp_id == req.Emp_id);

                if (employee == null) return NotFound("ไม่พบพนักงาน");

                employee.Employee_Type = req.Employee_Type;
                await _context.SaveChangesAsync();
                return Ok(new { message = "อัปเดตประเภทของพนังงานเรียบร้อย", Data = employee });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "เกิดข้อผิดพลาดในการอัปเดตประเภทของพนักงาน", Error = ex.Message });
            }
        }
        [HttpPut("updateWageStartTimeEndTimeEmp")]
        public async Task<IActionResult> updateWageStartTimeEndTimeEmp([FromBody] updateWageStartTimeEndTimeEmpReq req)
        {
            try
            {
                var employee = await _context.Employee.FirstOrDefaultAsync(e => e.Emp_id == req.Emp_id);

                if (employee == null) return NotFound("ไม่พบพนักงาน");

                if (req.Wage.HasValue)
                {
                    if (req.Wage.Value < 0) return BadRequest("ค่าจ้างต้องไม่ติดลบ");
                    employee.Wage = (int)req.Wage.Value;
                }


                if (req.Start_Time.HasValue)
                {
                    employee.Start_Time = req.Start_Time.Value;
                }


                if (req.End_Time.HasValue)
                {
                    employee.End_Time = req.End_Time.Value;
                }
                await _context.SaveChangesAsync();
                return Ok(new
                {
                    message = "อัฟเดตสำเร็จ",
                    data = new
                    {
                        employee.Emp_id,
                        employee.Wage,
                        Start_Time = employee.Start_Time?.ToString(@"hh\:mm"),
                        End_Time = employee.End_Time?.ToString(@"hh\:mm")
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "เกิดข้อผิดพลาดในการอัปเดตข้อมูลพนักงาน", Error = ex.Message });
            }
        }

    }

}