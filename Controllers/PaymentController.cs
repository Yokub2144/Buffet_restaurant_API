using System.Text.Json;
using Buffet_Restaurant_API.Dtos;
using Buffet_Restaurant_Managment_System_API.Data;
using Buffet_Restaurant_Managment_System_API.Hubs;
using Buffet_Restaurant_Managment_System_API.Models;
using Buffet_Restaurant_Managment_System_API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;




namespace Buffet_Restaurant_Managment_System_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly PromptPayService _promptPayService;
        private readonly restaurantDbContext _context;
        private readonly IHubContext<tableStatusHub> _hubContext;

        public PaymentController(
            PromptPayService promptPayService,
            restaurantDbContext context,
            IHubContext<tableStatusHub> hubContext
        )
        {
            _promptPayService = promptPayService;
            _context = context;
            _hubContext = hubContext;

        }

        [HttpPost("generate-qr")]
        public async Task<IActionResult> CreateQr([FromBody] QrRequestDto request)
        {
        
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Booking_id == request.BookingId);

            if (booking == null)
            {
                return NotFound(new { message = "ไม่พบข้อมูลการจอง" });
            }

            var qrResult = await _promptPayService.GeneratePromptPayQr(booking.Deposit_Amount);

            Console.WriteLine($"=== QR RESULT: {qrResult} ===");
            var parsed = JsonSerializer.Deserialize<JsonElement>(qrResult);
            var transactionId = parsed.GetProperty("data").GetProperty("transactionId").GetString();
            var amount = parsed.GetProperty("data").GetProperty("amount").GetString();
            return Ok(new
            {
                qr_data = qrResult,
                amount_pay = amount,
                booking_id = booking.Booking_id,
                transaction_id = transactionId,
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


        [HttpPost("generate-checkout-qr")]
    public async Task<IActionResult> CreateCheckoutQr([FromBody] CheckoutQrRequestDto request)
    {
        
        var bill = await _context.Bill
            .FirstOrDefaultAsync(b => b.Bill_id == request.BillId);

        if (bill == null)
        {
            return NotFound(new { message = "ไม่พบข้อมูลบิลที่ต้องการชำระเงิน" });
        }

        decimal amountToPay = request.TotalAmount > 0 ? request.TotalAmount : bill.Total_amount;

        if (amountToPay <= 0)
        {
            return BadRequest(new { message = "ยอดเงินที่ต้องชำระต้องมากกว่า 0 บาท" });
        }

        var qrResult = await _promptPayService.GeneratePromptPayQr(amountToPay);

        var parsed = JsonSerializer.Deserialize<JsonElement>(qrResult);
        var dataProp = parsed.GetProperty("data");
        
        var transactionId = dataProp.GetProperty("transactionId").GetString();
        string amountStr = dataProp.GetProperty("amount").ValueKind == JsonValueKind.Number
            ? dataProp.GetProperty("amount").GetDecimal().ToString("F2")
            : dataProp.GetProperty("amount").GetString();

        return Ok(new
        {
            qr_data = qrResult,
            amount_pay = amountStr,
            bill_id = bill.Bill_id,
            booking_id = bill.Booking_id,
            transaction_id = transactionId
        });
    }

    [HttpPost("verify-payment")]
    public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentRequestDto request)
    {
        var bill = await _context.Bill
            .FirstOrDefaultAsync(b => b.Bill_id == request.BillId);

        if (bill == null)
        {
            return NotFound(new { message = "ไม่พบข้อมูลบิล" });
        }

        // เรียก ตรวจสอบสถานะการโอนเงินจาก PromptPay / Bank Gateway API
        var result = await _promptPayService.CheckPaymentStatus(request.TransactionId);



        // 🟢 บันทึกข้อมูลการชำระเงินลงตาราง Payment
        var paymentRecord = new Payment
        {
            Booking_id = bill.Booking_id, // ใส่ Booking_id (ถ้ามี หรือ NULL)
            Bill_id = bill.Bill_id,       // ใส่ Bill_id
            Amount = bill.Total_amount,
            PaymentMethod = "โอน",
            Payment_Type = "หน้าร้าน",     // 'หน้าร้าน' ตาม Spec
            PaymentDateTime = DateTime.Now,
            TransactionId = request.TransactionId
        };

        _context.Payment.Add(paymentRecord);

        // อัปเดตสถานะบิล
        bill.PaymentMethod = "โอน";
        bill.Closed_at = DateTime.Now;


        await _context.SaveChangesAsync();

        // 🟢 บรอดแคสต์แจ้งเตือน SignalR Real-time ไปยังทุกหน้าจอ (BillingList / CreateBill)
        await _hubContext.Clients.All.SendAsync("UpdateBill", new
        {
            billId = bill.Bill_id,
            status = "CLOSED",
            paymentId = paymentRecord.Payment_Id
        });

        await _hubContext.Clients.All.SendAsync("UpdateTable", new
        {
            billId = bill.Bill_id,
            status = "ว่าง"
        });

        return Ok(new
        {
            api_result = result,
            message = "ชำระเงินสำเร็จ และปิดบิลเรียบร้อยแล้ว",
            payment_id = paymentRecord.Payment_Id,
            bill_id = bill.Bill_id
        });
    }

    }

}
