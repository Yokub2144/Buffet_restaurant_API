using Buffet_Restaurant_Managment_System_API.Models;
using Buffet_Restaurant_Managment_System_API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Buffet_Restaurant_Managment_System_API.Hubs;
using CloudinaryDotNet;
using BUFFET_RESTAURANT_API.Models;
using CloudinaryDotNet.Actions;
namespace Buffet_Restaurant_Managment_System_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ResConfigController : ControllerBase
    {
        private readonly restaurantDbContext _context;
        private readonly Cloudinary _cloudinary;

        public ResConfigController(restaurantDbContext context, IHubContext<tableStatusHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }
        private readonly IHubContext<tableStatusHub> _hubContext;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Resconfig>>> GetResconfigs()
        {
            return await _context.Res_Config.ToListAsync();
        }

        [HttpPost("add-config")]
        public async Task<ActionResult<Resconfig>> PostResConfig(Resconfig config)
        {
            _context.Res_Config.Add(config);
            await _context.SaveChangesAsync();

            return Ok(new { message = "เพิ่มข้อมูลสำเร็จ", data = config });
        }

        [Authorize(Roles = "เจ้าของร้าน")]
        [HttpPut("updateConfig")]
        public async Task<IActionResult> updateConfig([FromBody] UpdateResConfigReq req)
        {
            try
            {
                // ค้นหาข้อมูลเดิมในฐานข้อมูล
                var config = await _context.Res_Config.FirstOrDefaultAsync(c => c.Config_id == req.Config_id);

                if (config == null) return NotFound("ไม่พบข้อมูลการตั้งค่า");

                // อัปเดตค่า
                config.Res_name = req.Res_name ?? config.Res_name;
                config.Res_phone = req.Res_phone ?? config.Res_phone;
                config.Price_Adult = req.Price_Adult ?? config.Price_Adult;
                config.Price_Child = req.Price_Child ?? config.Price_Child;
                config.Fine = req.Fine ?? config.Fine;

                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("UpdateResConfig", config);
                return Ok(new { Message = "อัปเดตข้อมูลสำเร็จ", data = config });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "เกิดข้อผิดพลาดในการอัปเดต", Error = ex.Message });
            }
        }

    }
}