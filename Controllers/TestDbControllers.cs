using Microsoft.AspNetCore.Mvc;
using Buffet_Restaurant_Managment_System_API.Data;
using Microsoft.EntityFrameworkCore;
namespace Buffet_Restaurant_Managment_System_API.Controllers
{
[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly restaurantDbContext _context;

    public TestController(restaurantDbContext context)
    {
        _context = context;
    }

    [HttpGet("check-db")]
    public async Task<IActionResult> CheckConnection()
    {
        try
        {
            var employee = await _context.Employee.FirstOrDefaultAsync();
            return Ok(new { Message = "เชื่อมต่อสำเร็จ!", Data = employee });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = "เชื่อมต่อล้มเหลว", Error = ex.Message });
        }
    }
}
}