using Buffet_Restaurant_API.Dtos;
using Buffet_Restaurant_API.Models;
using Buffet_Restaurant_Managment_System_API.Data;
using Buffet_Restaurant_Managment_System_API.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Buffet_Restaurant_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BillController : ControllerBase
    {
        private readonly restaurantDbContext _context;
        private readonly IHubContext<tableStatusHub> _hubContext;
        public BillController(restaurantDbContext context, IHubContext<tableStatusHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        [HttpPost("walk-in")]
        public async Task<IActionResult> CreateWalkInBill([FromBody] CreateWalkInBillDto dto)
        {
            if (dto.Table_ids == null || !dto.Table_ids.Any())
            {
                return BadRequest(new { message = "กรุณาเลือกโต๊ะอย่างน้อย 1 โต๊ะ" });
            }

            var availableTables = await _context.Tables
                .Where(t => dto.Table_ids.Contains(t.Table_id) && t.Table_Status == "ว่าง")
                .ToListAsync();

            if (availableTables.Count != dto.Table_ids.Count)
            {
                var unavailable = dto.Table_ids.Except(availableTables.Select(t => t.Table_id)).ToList();
                return Conflict(new { message = "บางโต๊ะไม่ว่าง ไม่สามารถเปิดบิลได้", unavailable_table_ids = unavailable });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                dto.Discount_id = (dto.Discount_id == 0 || dto.Discount_id == null) ? null : dto.Discount_id;

                // 🎯 1. เปลี่ยนลำดับ: สร้างและบันทึกบิลหลักลงตาราง Bill ก่อน เพื่อให้ได้ Bill_id มาผูกกลุ่มโต๊ะ
                var newBill = new Bill
                {
                    Booking_id = null,
                    Config_id = dto.Config_id,
                    Emp_id = dto.Emp_id,
                    Discount_id = dto.Discount_id,
                    Created_at = DateTime.Now,
                    Closed_at = null,
                    NumAdults = dto.NumAdults,
                    NumChildren = dto.NumChildren,
                    Fine_kg = 0,
                    Total_amount = 0,
                    PaymentMethod = null
                };

                _context.Bill.Add(newBill);
                await _context.SaveChangesAsync(); // 🔥 รันเซฟจังหวะแรกเพื่อให้ได้ newBill.Bill_id มาใช้งาน

                // 🎯 2. นำ Bill_id ที่ได้ไปสร้างแถวข้อมูลลงใน GroupTables สำหรับทุกโต๊ะที่เลือกมาพร้อมๆ กัน
                var groupTables = dto.Table_ids.Select(tid => new GroupTable
                {
                    Booking_id = null,
                    Table_id = tid,
                    Bill_id = newBill.Bill_id // 👈 ผูกเข้าหากันที่ตรงนี้เรียบร้อยแล้ว!
                }).ToList();

                _context.GroupTables.AddRange(groupTables);

                // 3. เปลี่ยนสถานะของทุกโต๊ะที่เปิดเป็น "ไม่ว่าง"
                foreach (var table in availableTables)
                {
                    table.Table_Status = "ไม่ว่าง";

                    // ส่งข้อมูลโต๊ะที่อัปเดตไปยัง SignalR
                    await _hubContext.Clients.All.SendAsync("UpdateTable", new
                    {
                        tableId = table.Table_id,
                        status = "ไม่ว่าง"
                    });
                    }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    message = "เปิดบิล Walk-in และล็อกโต๊ะสำเร็จ",
                    billId = newBill.Bill_id,
                    tables = availableTables.Select(t => t.Table_Number)
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "เกิดข้อผิดพลาดในการเปิดบิล Walk-in", error = ex.Message });
            }
        }

        [HttpPost("booking/{bookingId}")]
        public async Task<IActionResult> CreateBillFromBooking(int bookingId, [FromBody] CreateBookingBillDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // ค้นหาข้อมูลการจองเพื่อตรวจสอบ
                var booking = await _context.Bookings.FindAsync(bookingId);
                if (booking == null)
                {
                    return NotFound(new { message = "ไม่พบข้อมูลการจองนี้ในระบบ" });
                }

                // 🎯 1. สร้างบิลจากข้อมูลการจองหลักก่อน
                var autoBill = new Bill
                {
                    Booking_id = bookingId,
                    Config_id = dto.Config_id,
                    Emp_id = dto.Emp_id,
                    Discount_id = dto.Discount_id,
                    Created_at = DateTime.Now,
                    NumAdults = booking.Adult_Count,
                    NumChildren = booking.Child_Count,
                    Fine_kg = 0,
                    Total_amount = booking.Deposit_Amount,
                    PaymentMethod = null
                };

                _context.Bill.Add(autoBill);
                await _context.SaveChangesAsync(); // 🔥 บันทึกเพื่อให้ได้รหัสบิลใบนี้ (autoBill.Bill_id)

                // 🎯 2. ค้นหากลุ่มโต๊ะทั้งหมดที่บันทึกไว้ตั้งแต่ตอนลูกค้าจอง (ผ่าน Booking_id) 
                // และทำการอัปเดตเอา Bill_id ตัวใหม่นี้ หยอดใส่เข้าไปทุกแถวเพื่อจับกลุ่มบิลร่วมกัน
                var groupTables = await _context.GroupTables
                    .Where(g => g.Booking_id == bookingId)
                    .ToListAsync();

                foreach (var gt in groupTables)
                {
                    gt.Bill_id = autoBill.Bill_id; // 👈 หยอดรหัสบิลอัปเดตความสัมพันธ์กลุ่ม
                }

                // อัปเดตสถานะการจองเป็นเข้าใช้บริการแล้ว (Arrived)
                booking.Booking_Status = "Arrived";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "สร้างบิลจากใบจองพร้อมผูกเลขกลุ่มโต๊ะสำเร็จ", billId = autoBill.Bill_id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "เกิดข้อผิดพลาดในการสร้างบิลจากการจอง", error = ex.Message });
            }
        }

        [HttpPut("{billId}/close")]
        public async Task<IActionResult> CloseBill(int billId, [FromBody] CloseBillDto dto) // แก้ชื่อตัวแปรพารามิเตอร์ให้ตรงกับ Route {billId}
        {
            try
            {
                var bill = await _context.Bill.FindAsync(billId);
                if (bill == null)
                {
                    return NotFound(new { message = "ไม่พบข้อมูลบิลที่ต้องการปิด" });
                }

                bill.Closed_at = DateTime.Now;
                bill.Fine_kg = dto.Fine_kg;
                bill.Total_amount = dto.Total_amount;
                bill.PaymentMethod = dto.PaymentMethod;

                await _context.SaveChangesAsync();
                return Ok(new { message = "เช็คบิลและปิดโต๊ะเรียบร้อยแล้ว" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "เกิดข้อผิดพลาดในการปิดบิล", error = ex.Message });
            }
        }

        [HttpGet("getBill")]
        public async Task<IActionResult> getBill()
        {
            var bill = await _context.Bill
                .Where(b => b.Closed_at == null)
                .OrderByDescending(b => b.Created_at)
                .ToListAsync();

            return Ok(bill);
        }

        [HttpPut("update/{billId}")]
        public async Task<IActionResult> UpdateBill(int billId, [FromBody] UpdateBillDto dto)
        {
            try
            {
                var bill = await _context.Bill.FindAsync(billId);
                if (bill == null)
                {
                    return NotFound(new { message = "ไม่พบข้อมูลบิลที่ต้องการอัปเดต" });
                }

                bill.Fine_kg = dto.Fine_kg;
                bill.NumAdults = dto.NumAdults;
                bill.NumChildren = dto.NumChildren;
                bill.Discount_id = dto.Discount_id;

                await _context.SaveChangesAsync();
                return Ok(new { message = "อัปเดตบิลเรียบร้อยแล้ว" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "เกิดข้อผิดพลาดในการอัปเดตบิล", error = ex.Message });
            }
        }
        [HttpDelete("delete/{billId}")]
        public async Task<IActionResult> DeleteBill(int billId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {

                var bill = await _context.Bill.FindAsync(billId);
                if (bill == null)
                {
                    return NotFound(new { message = "ไม่พบข้อมูลบิลที่ต้องการลบ" });
                }

                var updatedTablesForSignalR = new List<object>();

                var relatedGroupTables = await _context.GroupTables
                    .Where(gt => gt.Bill_id == billId)
                    .ToListAsync();

                if (relatedGroupTables.Any())
                {
                    var tableIds = relatedGroupTables.Select(gt => gt.Table_id).ToList();

                    var tablesToRelease = await _context.Tables
                        .Where(t => tableIds.Contains(t.Table_id))
                        .ToListAsync();

                    foreach (var table in tablesToRelease)
                    {
                        table.Table_Status = "ว่าง";

                        updatedTablesForSignalR.Add(new
                        {
                            tableId = table.Table_id,
                            status = "ว่าง"
                        });
                    }

                    _context.GroupTables.RemoveRange(relatedGroupTables);
                }

                _context.Bill.Remove(bill);

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                foreach (var tableObj in updatedTablesForSignalR)
                {
                    // 💡 ใช้ชื่อ Event เดียวกันกับตัวอัปเดตปกติของคุณคือ "UpdateTable"
                    await _hubContext.Clients.All.SendAsync("UpdateTable", tableObj);
                }

                return Ok(new { message = "ลบบิลและคืนสถานะโต๊ะแบบเรียลไทม์เรียบร้อยแล้ว" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "เกิดข้อผิดพลาดในการลบบิล", error = ex.Message });
            }
        }
    }
}