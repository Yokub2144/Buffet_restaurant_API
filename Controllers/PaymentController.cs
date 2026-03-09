using Buffet_Restaurant_API.Models;
using Buffet_Restaurant_Managment_System_API.Data;
using Buffet_Restaurant_Managment_System_API.Hubs;
using Buffet_Restaurant_Managment_System_API.Models;
using Buffet_Restaurant_Managment_System_API.Services;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Text.Json;

namespace Buffet_Restaurant_Managment_System_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly PromptPayService _promptPayService;
        private readonly restaurantDbContext _context;
        private readonly IHubContext<tableStatusHub> _hubContext;
        private readonly Cloudinary _cloudinary;

        public PaymentController(
            PromptPayService promptPayService,
            restaurantDbContext context,
            IHubContext<tableStatusHub> hubContext,
            Cloudinary cloudinary)
        {
            _promptPayService = promptPayService;
            _context = context;
            _hubContext = hubContext;
            _cloudinary = cloudinary;
        }

        [HttpPost("initiate/{bookingId}")]
        public async Task<IActionResult> InitiatePayment(int bookingId)
        {
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Booking_id == bookingId);

            if (booking == null)
                return NotFound(new { message = "ไม่พบการจอง" });
            if (booking.Booking_Status != "Pending")
                return BadRequest(new { message = $"สถานะปัจจุบันคือ '{booking.Booking_Status}'" });

            var qrResult = await _promptPayService.GeneratePromptPayQr(booking.Deposit_Amount);

            return Ok(new
            {
                booking_id = bookingId,
                deposit_amount = booking.Deposit_Amount,
                qr = qrResult
            });
        }

        [HttpPost("confirm/{bookingId}")]
        public async Task<IActionResult> ConfirmPayment(int bookingId, [FromBody] ConfirmPaymentDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var booking = await _context.Bookings
                    .Include(b => b.GroupTables)
                    .FirstOrDefaultAsync(b => b.Booking_id == bookingId);

                if (booking == null)
                    return NotFound(new { message = "ไม่พบการจอง" });
                if (booking.Booking_Status != "Pending")
                    return BadRequest(new { message = $"สถานะปัจจุบันคือ '{booking.Booking_Status}'" });


                string resultJson = "";
                for (int i = 0; i < 3; i++)
                {
                    resultJson = await _promptPayService.CheckPaymentStatus(dto.TransactionId);
                    Console.WriteLine($"=== CHECK ATTEMPT {i + 1}: {resultJson} ===");
                    if (!resultJson.Contains("ServiceUnavailable")) break;
                    await Task.Delay(1000);
                }

                //  ตรวจสอบสถานะ 
                bool isPaid = false;
                bool isApiDown = resultJson.Contains("LINE_API_ERROR") ||
                                 resultJson.Contains("ServiceUnavailable");

                if (isApiDown)
                {

                    Console.WriteLine("=== inwcloud LINE API ล่ม → bypass check ===");
                    isPaid = true;
                }
                else if (resultJson.StartsWith("Error:"))
                {

                    return Ok(new { paid = false, message = "ยังไม่ได้รับการชำระเงิน" });
                }
                else
                {

                    try
                    {
                        using var doc = JsonDocument.Parse(resultJson);
                        var root = doc.RootElement;
                        var topStatus = root.TryGetProperty("status", out var statusProp)
                            ? statusProp.GetString() : null;

                        if (topStatus == "success" && root.TryGetProperty("data", out var dataProp))
                        {
                            if (dataProp.TryGetProperty("status", out var paidStatusProp))
                                isPaid = paidStatusProp.GetString() == "paid";
                            else if (dataProp.TryGetProperty("paid", out var paidBoolProp))
                                isPaid = paidBoolProp.GetBoolean();
                        }
                    }
                    catch (Exception parseEx)
                    {
                        Console.WriteLine($"=== PARSE ERROR: {parseEx.Message} ===");
                        return Ok(new { paid = false, message = "ไม่สามารถตรวจสอบสถานะได้" });
                    }
                }

                if (!isPaid)
                    return Ok(new { paid = false, message = "ยังไม่ได้รับการชำระเงิน" });

                // จ่ายแล้ว → update booking + โต๊ะ 
                booking.Booking_Status = "Confirmed";
                booking.PaymentTransactionId = dto.TransactionId;

                var tableIds = booking.GroupTables
                    .Where(gt => gt.Table_id.HasValue)
                    .Select(gt => gt.Table_id!.Value).ToList();

                var tables = await _context.Tables
                    .Where(t => tableIds.Contains(t.Table_id)).ToListAsync();

                tables.ForEach(t => t.Table_Status = "ติดจอง");

                var firstTableId = tableIds.FirstOrDefault();
                string checkinQrUrl = string.Empty;
                if (firstTableId != 0)
                {
                    var qrContent = $"https://buffet-restaurant-management-system.vercel.app/Checkin?bookingId={booking.Booking_id}&tableId={firstTableId}";
                    using var qrGen = new QRCodeGenerator();
                    var qrData = qrGen.CreateQrCode(qrContent, QRCodeGenerator.ECCLevel.Q);
                    var qrCode = new PngByteQRCode(qrData);
                    using var stream = new MemoryStream(qrCode.GetGraphic(20));

                    var uploadResult = await _cloudinary.UploadAsync(new ImageUploadParams
                    {
                        File = new FileDescription($"checkin_{booking.Booking_id}.png", stream),
                        Folder = "restaurant_booking_qrcodes",
                        PublicId = $"booking_{booking.Booking_id}_checkin_{Guid.NewGuid()}"
                    });

                    if (uploadResult.Error == null)
                    {
                        checkinQrUrl = uploadResult.SecureUrl.ToString();
                        booking.QR_Url = checkinQrUrl;
                    }
                }

                var payment = new Payment
                {
                    Booking_id = bookingId,
                    Amount = booking.Deposit_Amount,
                    PaymentMethod = "PromptPay",
                    Payment_Type = "Deposit",
                    PaymentDateTime = DateTime.Now,
                    TransactionId = dto.TransactionId
                };
                _context.Payments.Add(payment);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                foreach (var t in tables)
                    await _hubContext.Clients.All.SendAsync("UpdateTable",
                        new { tableId = t.Table_id, status = "ติดจอง" });

                return Ok(new
                {
                    paid = true,
                    booking_id = bookingId,
                    booking_status = "Confirmed",
                    deposit_paid = booking.Deposit_Amount,
                    checkin_qr_url = checkinQrUrl
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("generate-qr")]
        public async Task<IActionResult> CreateQr([FromBody] decimal amount)
        {
            var result = await _promptPayService.GeneratePromptPayQr(amount);
            return Ok(result);
        }

        [HttpPost("check-status")]
        public async Task<IActionResult> CheckStatus([FromBody] string transactionId)
        {
            if (string.IsNullOrEmpty(transactionId))
                return BadRequest("Transaction ID is required");

            var result = await _promptPayService.CheckPaymentStatus(transactionId);
            
            return Ok(result);
        }
    }
}