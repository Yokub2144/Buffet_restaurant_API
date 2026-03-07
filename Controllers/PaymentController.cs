using System.Text.Json;
using Buffet_Restaurant_API.Dtos;
using Buffet_Restaurant_Managment_System_API.Data;
using Buffet_Restaurant_Managment_System_API.Models;
using Buffet_Restaurant_Managment_System_API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;




namespace Buffet_Restaurant_Managment_System_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly PromptPayService _promptPayService;
        private readonly restaurantDbContext _context;

        public PaymentController(
            PromptPayService promptPayService,
            restaurantDbContext context
        )
        {
            _promptPayService = promptPayService;
            _context = context;

        }

        [HttpPost("generate-qr")]
        public async Task<IActionResult> CreateQr([FromBody] QrRequestDto request)
        {
            // ค้นหาการจองจากฐานข้อมูล เพื่อเอาค่ามัดจำที่ระบบคำนวณไว้
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Booking_id == request.BookingId);

            if (booking == null)
            {
                return NotFound(new { message = "ไม่พบข้อมูลการจอง" });
            }

            var qrResult = await _promptPayService.GeneratePromptPayQr(booking.Deposit_Amount);

            return Ok(new
            {
                qr_data = qrResult,
                amount = booking.Deposit_Amount,
                booking_id = booking.Booking_id
            });
        }
        [HttpPost("check-status")]
        public async Task<IActionResult> CheckStatus([FromBody] string transactionId)
        {
            if (string.IsNullOrEmpty(transactionId))
                return BadRequest("Transaction ID is required");

            var result = await _promptPayService.CheckPaymentStatus(transactionId);
            return Ok(result);
        }




        // [HttpPost("generate-qr")]
        // public async Task<IActionResult> CreateQr([FromBody] decimal amount)
        // {
        //     var result = await _promptPayService.GeneratePromptPayQr(amount);
        //     return Ok(result);
        // }


    }




}