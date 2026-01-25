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
namespace Buffet_Restaurant_Managment_System_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ManagerController : ControllerBase
    {
        private readonly restaurantDbContext _context;
        public ManagerController(restaurantDbContext context)
        {
            _context = context;
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
        [Authorize(Roles = "เจ้าของร้าน")]
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
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> AddTable([FromForm]tableDtos table, [FromServices] Cloudinary cloudinary)
        {
            var existingTable = await _context.Tables
                .FirstOrDefaultAsync(t => t.Table_Number == table.Table_Number);
            if (existingTable != null)
            {
                return BadRequest(new { Message = "โต๊ะหมายเลขนี้มีอยู่แล้ว" });
            }
            string menuUrl = $"https://your-restaurant-frontend.com/menu?table={table.Table_Number}";
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
    }
}