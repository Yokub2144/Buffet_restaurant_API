using Buffet_Restaurant_API.Dtos;
using Buffet_Restaurant_API.Models;
using Buffet_Restaurant_Managment_System_API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Buffet_Restaurant_API.Controllers
{
    public class BillController : ControllerBase
    {
        private readonly restaurantDbContext _context;
        public BillController(restaurantDbContext context)
        {
            _context = context;
        }
        [HttpPost("walk-in")]
        public async Task<IActionResult> CreateWalkInBill([FromBody] CreateWalkInBillDto dto)
        {
            try
            {
                var newBill = new Bill
                {
                    Booking_id = 0, // Walk-in ไม่มีรหัสการจอง
                    Config_id = dto.Config_id, 
                    GroupTable_id = dto.GroupTable_id,
                    Emp_id = dto.Emp_id,
                    Discount_id = dto.Discount_id, // พนักงานคีย์ส่วนลดเข้ามาตั้งแต่ตอนเปิดโต๊ะ
                    Created_at = DateTime.Now,
                    NumAdults = dto.NumAdults,
                    NumChildren = dto.NumChildren,
                    Fine_kg = 0,       // ใส่ค่า 0 ไว้ก่อน ค่อยคีย์ตอนเช็คบิลถ้ามีกินเหลือ
                    Total_amount = 0,  // รอคำนวณตอนเช็คบิลทีเดียว
                    PaymentMethod = null
                };

                _context.Bill.Add(newBill);
                await _context.SaveChangesAsync();

                return Ok(new { message = "เปิดบิล Walk-in พร้อมบันทึกส่วนลดสำเร็จ", billId = newBill.Bill_id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "เกิดข้อผิดพลาดในการเปิดบิล", error = ex.Message });
            }
        }
        [HttpPost("booking/{bookingId}")]
        public async Task<IActionResult> CreateBillFromBooking(int bookingId, [FromBody] CreateBookingBillDto dto)
        {
            try
            {
                // ค้นหาข้อมูลการจองเพื่อตรวจสอบ
                var booking = await _context.Bookings.FindAsync(bookingId);
                if (booking == null)
                {
                    return NotFound(new { message = "ไม่พบข้อมูลการจองนี้ในระบบ" });
                }

                // ค้นหากลุ่มโต๊ะที่ผูกกับการจองนี้ใน GroupTable
                var groupTable = await _context.GroupTables.FirstOrDefaultAsync(g => g.Booking_id == bookingId);
                int groupTableId = groupTable?.GroupTable_id ?? 0;

                var autoBill = new Bill
                {
                    Booking_id = bookingId,
                    Config_id = dto.Config_id,
                    GroupTable_id = groupTableId,
                    Emp_id = dto.Emp_id,
                    Discount_id = dto.Discount_id, // พนักงานคีย์ส่วนลดเพิ่มให้ลูกค้าจองได้ตั้งแต่ตอนมาถึงร้าน
                    Created_at = DateTime.Now,
                    NumAdults = booking.Adult_Count, // ดึงจำนวนคนออโต้จากการจอง
                    NumChildren = booking.Child_Count, // ดึงจำนวนคนออโต้จากการจอง
                    Fine_kg = 0,
                    Total_amount = booking.Deposit_Amount, // นำเงินมัดจำมาตั้งต้น
                    PaymentMethod = null
                };

                _context.Bill.Add(autoBill);
                
                // อัปเดตสถานะการจองเป็นเข้าใช้บริการแล้ว (Arrived)
                booking.Booking_Status = "Arrived";

                await _context.SaveChangesAsync();

                return Ok(new { message = "สร้างบิลจากใบจองพร้อมบันทึกส่วนลดสำเร็จ", billId = autoBill.Bill_id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "เกิดข้อผิดพลาดในการสร้างบิลจากการจอง", error = ex.Message });
            }
        }
        
        [HttpPut("{billId}/close")]
        public async Task<IActionResult> CloseBill(int bill_id, [FromBody] CloseBillDto dto)
        {
            try
            {
                var bill = await _context.Bill.FindAsync(bill_id);
                if (bill == null)
                {
                    return NotFound(new { message = "ไม่พบข้อมูลบิลที่ต้องการปิด" });
                }

                bill.Closed_at = DateTime.Now;
                bill.Fine_kg = dto.Fine_kg; // รับค่าปรับกรณีทานเหลือ (ถ้ามี)
                bill.Total_amount = dto.Total_amount; // ยอดสุทธิรวมหลังจากหัก Discount_id ที่เลือกไว้ตอนแรกแล้ว
                bill.PaymentMethod = dto.PaymentMethod;

                await _context.SaveChangesAsync();
                return Ok(new { message = "เช็คบิลและปิดโต๊ะเรียบร้อยแล้ว" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "เกิดข้อผิดพลาดในการปิดบิล", error = ex.Message });
            }
        }
    }
}