using Buffet_Restaurant_Managment_System_API.Data;
using Buffet_Restaurant_Managment_System_API.Hubs;
using Buffet_Restaurant_Managment_System_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Buffet_Restaurant_Managment_System_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DiscountController : ControllerBase
    {
        private readonly restaurantDbContext _context;
        private readonly IHubContext<tableStatusHub> _hubContext;

        public DiscountController(restaurantDbContext context, IHubContext<tableStatusHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // GET /api/Discount
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Discount>>> GetDiscounts()
        {
            return await _context.Discounts.ToListAsync();
        }

        // GET /api/Discount/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Discount>> GetDiscount(int id)
        {
            var discount = await _context.Discounts.FindAsync(id);
            if (discount == null)
                return NotFound(new { message = "ไม่พบข้อมูลส่วนลด" });

            return Ok(discount);
        }

        // POST /api/Discount/add
        [Authorize(Roles = "เจ้าของร้าน")]
        [HttpPost("add")]
        public async Task<ActionResult<Discount>> AddDiscount([FromBody] DiscountDto request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Discount_Name))
                    return BadRequest(new { message = "กรุณากรอกชื่อส่วนลด" });

                if (request.Discount_amount <= 0)
                    return BadRequest(new { message = "มูลค่าส่วนลดต้องมากกว่า 0" });

                if (!request.StartDate.HasValue || !request.EndDate.HasValue)
                    return BadRequest(new { message = "กรุณาระบุวันเริ่มและวันสิ้นสุด" });

                if (request.StartDate > request.EndDate)
                    return BadRequest(new { message = "วันเริ่มต้องน้อยกว่าวันสิ้นสุด" });

                var newDiscount = new Discount
                {
                    Discount_Name = request.Discount_Name,
                    Discount_amount = request.Discount_amount,
                    Discount_Type = request.Discount_Type ?? "fixed",
                    StartDate = request.StartDate.Value,
                    EndDate = request.EndDate.Value,
                };

                _context.Discounts.Add(newDiscount);
                await _context.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("UpdateDiscount");

                return Ok(new { message = "เพิ่มส่วนลดสำเร็จ", data = newDiscount });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // PUT /api/Discount/update/{id}
        [Authorize(Roles = "เจ้าของร้าน")]
        [HttpPut("update/{id}")]
        public async Task<IActionResult> UpdateDiscount(int id, [FromBody] DiscountDto request)
        {
            try
            {
                var discount = await _context.Discounts.FindAsync(id);
                if (discount == null)
                    return NotFound(new { message = "ไม่พบข้อมูลส่วนลด" });

                if (request.StartDate.HasValue && request.EndDate.HasValue
                    && request.StartDate > request.EndDate)
                    return BadRequest(new { message = "วันเริ่มต้องน้อยกว่าวันสิ้นสุด" });

                discount.Discount_Name = request.Discount_Name ?? discount.Discount_Name;
                discount.Discount_amount = request.Discount_amount > 0
                                            ? request.Discount_amount
                                            : discount.Discount_amount;
                discount.Discount_Type = request.Discount_Type ?? discount.Discount_Type;
                discount.StartDate = request.StartDate.HasValue
                                            ? request.StartDate.Value
                                            : discount.StartDate;
                discount.EndDate = request.EndDate.HasValue
                                            ? request.EndDate.Value
                                            : discount.EndDate;

                await _context.SaveChangesAsync();

                // 🟢 ยิงสัญญาณ Realtime แจ้งทุก Client
                await _hubContext.Clients.All.SendAsync("UpdateDiscount");

                return Ok(new { message = "แก้ไขส่วนลดสำเร็จ", data = discount });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // DELETE /api/Discount/{id}
        [Authorize(Roles = "เจ้าของร้าน")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDiscount(int id)
        {
            try
            {
                var discount = await _context.Discounts.FindAsync(id);
                if (discount == null)
                    return NotFound(new { message = "ไม่พบข้อมูลส่วนลด" });

                _context.Discounts.Remove(discount);
                await _context.SaveChangesAsync();

                // 🟢 ยิงสัญญาณ Realtime แจ้งทุก Client
                await _hubContext.Clients.All.SendAsync("UpdateDiscount");

                return Ok(new { message = "ลบส่วนลดเรียบร้อยแล้ว" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}