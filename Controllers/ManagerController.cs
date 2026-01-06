using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Buffet_Restaurant_Managment_System_API.Data;
using Buffet_Restaurant_Managment_System_API.Models;
using Buffet_Restaurant_Managment_System_API.Dtos;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Authorization;
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
                .Where(e => e.Employee_Status == "ทำงานปัจจุบัน")
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
    }
}