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
            var employees = await _context.Employee
                .Where(e => e.Employee_Status == "" || e.Employee_Status == null)
                .ToListAsync();
            return Ok(employees);
        }
        [Authorize(Roles = "เจ้าของร้าน")]
        [HttpPost("approveEmployee")]
        public async Task<IActionResult> ApproveEmployee(int empId)
        {
            var employee = await _context.Employee
                .FirstOrDefaultAsync(e => e.Emp_id == empId && (e.Employee_Status == "" || e.Employee_Status == null ));
            if (employee == null)
            {
                return NotFound(new { Message = "ไม่พบพนักงานที่รอการอนุมัติ" });
            }
            employee.Employee_Status = "ทำงานปัจจุบัน";
            employee.Hire_Date = DateTime.Now;
            await _context.SaveChangesAsync();
            return Ok(new { Message = "อนุมัติพนักงานสำเร็จ" });
        }

        [Authorize(Roles = "เจ้าของร้าน")]
        [HttpPost("rejectEmployee")]
        public async Task<IActionResult> RejectEmployee(int empId)
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
        [Authorize(Roles = "เจ้าของร้าน")]
        [HttpGet("getAllEmployees")]
        public async Task<IActionResult> GetAllEmployees()
        {
            var employees = await _context.Employee
                .Where(e => e.Employee_Status == "ทำงานปัจจุบัน" || e.Employee_Status == "ลาออก")
                .OrderByDescending(e => e.Employee_Status == "ทำงานปัจจุบัน")
                .ThenBy(e => e.Emp_id)
                .ToListAsync();
            return Ok(employees);
        }
        [HttpGet("getEmployeeById")]
        public async Task<IActionResult> GetEmployeeById(int empId)
        {
            var employee = await _context.Employee
                .FirstOrDefaultAsync(e => e.Emp_id == empId);
            if (employee == null)
            {
                return NotFound(new { Message = "ไม่พบพนักงาน" });
            }
            return Ok(employee);
        }
        [Authorize(Roles = "เจ้าของร้าน")]
        [HttpPost("AddTable")]
        public async Task<IActionResult> AddTable([FromBody]tableDtos table, [FromServices] Cloudinary cloudinary)
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
            
                    if(uploadResult.Error != null)
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
        [HttpGet("getTables")]
        public async Task<IActionResult> getTables()
        {
            var tables = await _context.Tables.ToListAsync();
            return Ok(tables);
        }
        [HttpGet("getTableById")]
        public async Task<IActionResult> GetTableById(int tableId)
        {
            var table = await _context.Tables
                .FirstOrDefaultAsync(t => t.Table_id == tableId);
            if (table == null)
            {
                return NotFound(new { Message = "ไม่พบโต๊ะ" });
            }
            return Ok(table);
        }
        [Authorize(Roles = "เจ้าของร้าน")]
        [HttpDelete("deleteTable")]
        public async Task<IActionResult> DeleteTable(int tableId)
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
        [HttpPut("updateTablestatus")]
        public async Task<IActionResult> updateTableStatus([FromBody] updateTableStatus req)
        {
           var table = await _context.Tables.FirstOrDefaultAsync(t => t.Table_id == req.tableId);
        
        if (table == null) return NotFound("ไม่พบข้อมูลโต๊ะ");

        table.Table_Status = req.status;
        await _context.SaveChangesAsync();

        await _hubContext.Clients.All.SendAsync("UpdateTable", new { 
            tableId = table.Table_id, 
            status = table.Table_Status
        });

        return Ok(new { message = "อัปเดตสถานะสำเร็จ", data = table });
        }
    }

}