using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Buffet_Restaurant_API.Models;
using Buffet_Restaurant_API.Dtos;
using Buffet_Restaurant_Managment_System_API.Data;
using Buffet_Restaurant_Managment_System_API.Hubs;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using QRCoder;

namespace Buffet_Restaurant_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BookingController : ControllerBase
    {
        private readonly restaurantDbContext _context;
        private readonly IHubContext<tableStatusHub> _hubContext;
        private readonly Cloudinary _cloudinary;

        public BookingController(
            restaurantDbContext context,
            IHubContext<tableStatusHub> hubContext,
            Cloudinary cloudinary)
        {
            _context = context;
            _hubContext = hubContext;
            _cloudinary = cloudinary;
        }

        private async Task<string> GenerateAndUploadQr(string qrContent, string fileName)
        {
            using var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(qrContent, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(qrCodeData);
            byte[] qrBytes = qrCode.GetGraphic(20);
            using var stream = new MemoryStream(qrBytes);
            var uploadParams = new ImageUploadParams()
            {
                File = new FileDescription($"{fileName}.png", stream),
                Folder = "restaurant_booking_qrcodes",
                PublicId = $"{fileName}_{Guid.NewGuid()}"
            };
            var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            if (uploadResult.Error != null)
                throw new Exception("อัพโหลด QR ไม่สำเร็จ: " + uploadResult.Error.Message);
            return uploadResult.SecureUrl.ToString();
        }


        [HttpGet("available-tables")]
        public async Task<IActionResult> GetAvailableTables()
        {
            var tables = await _context.Tables
                .Select(t => new { t.Table_id, t.Table_Number, t.Table_Status, t.Table_QR_Code })
                .ToListAsync();
            return Ok(tables);
        }


        [HttpGet("member/{memberId}")]
        public async Task<IActionResult> GetByMember(int memberId)
        {
            var bookings = await _context.Bookings
                .Include(b => b.Member)
                .Include(b => b.GroupTables).ThenInclude(gt => gt.Table)
                .Where(b => b.Member_id == memberId)
                .OrderByDescending(b => b.Booking_Date)
                .Select(b => new BookingResponseDto
                {
                    Booking_id = b.Booking_id,
                    Member_Name = b.Member!.Fullname,
                    Booking_Date = b.Booking_Date,
                    Booking_Time = b.Booking_Time,
                    Booking_Status = b.Booking_Status,
                    Adult_Count = b.Adult_Count,
                    Child_Count = b.Child_Count,
                    Tables_Booked = b.GroupTables.Select(gt => gt.Table!.Table_Number).ToList()
                })
                .ToListAsync();
            return Ok(bookings);
        }


        [HttpGet("{id}")]
        public async Task<IActionResult> GetBooking(int id)
        {
            var booking = await _context.Bookings
                .Include(b => b.Member)
                .Include(b => b.GroupTables).ThenInclude(gt => gt.Table)
                .FirstOrDefaultAsync(b => b.Booking_id == id);

            if (booking == null) return NotFound(new { message = "ไม่พบการจอง" });

            return Ok(new BookingResponseDto
            {
                Booking_id = booking.Booking_id,
                Member_Name = booking.Member?.Fullname ?? "",
                Member_Phone = booking.Member?.Phone ?? "",
                Booking_Date = booking.Booking_Date,
                Booking_Time = booking.Booking_Time,
                Booking_Status = booking.Booking_Status,
                Adult_Count = booking.Adult_Count,
                Child_Count = booking.Child_Count,
                Tables_Booked = booking.GroupTables.Select(gt => gt.Table?.Table_Number ?? "").ToList()
            });
        }


        [HttpPost("create")]
        public async Task<IActionResult> CreateBooking([FromBody] CreateBookingDto dto)
        {
            var availableTables = await _context.Tables
                .Where(t => dto.Table_ids.Contains(t.Table_id) && t.Table_Status == "ว่าง")
                .ToListAsync();

            if (availableTables.Count != dto.Table_ids.Count)
            {
                var unavailable = dto.Table_ids.Except(availableTables.Select(t => t.Table_id)).ToList();
                return Conflict(new { message = "บางโต๊ะไม่ว่าง", unavailable_table_ids = unavailable });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var booking = new Booking
                {
                    Member_id = dto.Member_id,
                    Booking_Date = dto.Booking_Date,
                    Booking_Time = dto.Booking_Time,
                    Adult_Count = dto.Adult_Count,
                    Child_Count = dto.Child_Count,
                    Booking_Status = "Pending"
                };
                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                var groupTables = dto.Table_ids.Select(tid => new GroupTable
                {
                    Booking_id = booking.Booking_id,
                    Table_id = tid
                }).ToList();
                _context.GroupTables.AddRange(groupTables);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return CreatedAtAction(nameof(GetBooking),
                    new { id = booking.Booking_id },
                    new { message = "สร้างการจองสำเร็จ รอชำระเงิน", booking_id = booking.Booking_id, tables = availableTables.Select(t => t.Table_Number) });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = ex.Message });
            }
        }


        [HttpPost("checkin")]
        public async Task<IActionResult> CheckIn([FromBody] CheckinDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var booking = await _context.Bookings
                    .Include(b => b.GroupTables)
                    .FirstOrDefaultAsync(b => b.Booking_id == dto.BookingId);

                if (booking == null)
                    return NotFound(new { message = "ไม่พบการจอง" });
                if (booking.Booking_Status != "Confirmed")
                    return BadRequest(new { message = $"ไม่สามารถเช็คอินได้ สถานะคือ '{booking.Booking_Status}'" });

                var hasTable = booking.GroupTables.Any(gt => gt.Table_id == dto.TableId);
                if (!hasTable)
                    return BadRequest(new { message = "โต๊ะนี้ไม่ได้อยู่ในการจองนี้" });


                var allTableIds = booking.GroupTables
                    .Where(gt => gt.Table_id.HasValue)
                    .Select(gt => gt.Table_id!.Value).ToList();

                var allTables = await _context.Tables
                    .Where(t => allTableIds.Contains(t.Table_id)).ToListAsync();

                allTables.ForEach(t => t.Table_Status = "ไม่ว่าง");
                booking.Booking_Status = "Completed";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();


                foreach (var table in allTables)
                {
                    await _hubContext.Clients.All
                        .SendAsync("UpdateTable", new { tableId = table.Table_id, status = "ไม่ว่าง" });
                }

                return Ok(new
                {
                    message = "เช็คอินสำเร็จ",
                    booking_id = dto.BookingId,
                    booking_status = "Completed",
                    tables_checked_in = allTables.Select(t => t.Table_Number).ToList()
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("mock-payment/{id}")]
        public async Task<IActionResult> MockPayment(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var booking = await _context.Bookings
                    .Include(b => b.GroupTables)
                    .FirstOrDefaultAsync(b => b.Booking_id == id);

                if (booking == null) return NotFound(new { message = "ไม่พบการจอง" });
                if (booking.Booking_Status != "Pending")
                    return BadRequest(new { message = $"สถานะปัจจุบันคือ '{booking.Booking_Status}'" });

                booking.Booking_Status = "Confirmed";

                var tableIds = booking.GroupTables
                    .Where(gt => gt.Table_id.HasValue)
                    .Select(gt => gt.Table_id!.Value).ToList();

                var tables = await _context.Tables
                    .Where(t => tableIds.Contains(t.Table_id)).ToListAsync();

                tables.ForEach(t => t.Table_Status = "ติดจอง");
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                foreach (var table in tables)
                {
                    await _hubContext.Clients.All
                        .SendAsync("UpdateTable", new { tableId = table.Table_id, status = "ติดจอง" });
                }

                var qrList = new List<object>();
                foreach (var gt in booking.GroupTables.Where(gt => gt.Table_id.HasValue))
                {
                    var qrContent = $"https://buffet-restaurant-management-system.vercel.app/Checkin?bookingId={booking.Booking_id}&tableId={gt.Table_id}";
                    var fileName = $"booking_{booking.Booking_id}_table_{gt.Table_id}";
                    var qrImageUrl = await GenerateAndUploadQr(qrContent, fileName);
                    qrList.Add(new { tableId = gt.Table_id, qrImageUrl });
                }

                return Ok(new
                {
                    message = "ชำระเงินสำเร็จ (Mock)",
                    booking_id = booking.Booking_id,
                    booking_status = "Confirmed",
                    qr_list = qrList
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = ex.Message });
            }
        }



        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateBookingStatusDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var booking = await _context.Bookings
                    .Include(b => b.GroupTables)
                    .FirstOrDefaultAsync(b => b.Booking_id == id);

                if (booking == null) return NotFound(new { message = "ไม่พบการจอง" });

                booking.Booking_Status = dto.Booking_Status;

                if (dto.Booking_Status == "Cancelled")
                {
                    var tableIds = booking.GroupTables
                        .Where(gt => gt.Table_id.HasValue)
                        .Select(gt => gt.Table_id!.Value).ToList();

                    var tables = await _context.Tables
                        .Where(t => tableIds.Contains(t.Table_id)).ToListAsync();

                    tables.ForEach(t => t.Table_Status = "ว่าง");
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    foreach (var table in tables)
                    {
                        await _hubContext.Clients.All
                            .SendAsync("UpdateTable", new { tableId = table.Table_id, status = "ว่าง" });
                    }
                }
                else
                {
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }

                return Ok(new { message = $"อัพเดตสถานะเป็น {dto.Booking_Status} สำเร็จ" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = ex.Message });
            }
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> CancelBooking(int id)
        {
            return await UpdateStatus(id, new UpdateBookingStatusDto { Booking_Status = "Cancelled" });
        }
    }
}